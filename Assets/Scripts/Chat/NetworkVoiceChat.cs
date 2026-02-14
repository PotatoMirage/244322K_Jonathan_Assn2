using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class NetworkVoiceChat : NetworkBehaviour
{
    [Header("Settings")]
    public int frequency = 16000;
    public int chunkLength = 200;
    public KeyCode pushToTalkKey = KeyCode.V;
    public bool usePushToTalk = true;

    private AudioSource audioSource;
    private AudioClip recordingClip;
    private string microphoneDevice;

    private int lastSamplePosition = 0;
    private int clipTotalSamples;
    private Queue<float> audioBuffer = new Queue<float>();

    public override void OnNetworkSpawn()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f;

        if (IsOwner)
        {
            InitializeMicrophone();
        }
    }

    private void InitializeMicrophone()
    {
        if (Microphone.devices.Length > 0)
        {
            microphoneDevice = Microphone.devices[0];
            recordingClip = Microphone.Start(microphoneDevice, true, 10, frequency);
            clipTotalSamples = recordingClip.samples;
        }
    }

    private void Update()
    {
        if (IsOwner && recordingClip != null)
        {
            HandleVoiceTransmission();
        }

        if (!IsOwner)
        {
            ProcessPlaybackQueue();
        }
    }

    private void HandleVoiceTransmission()
    {
        bool isTalking = usePushToTalk ? Input.GetKey(pushToTalkKey) : true;
        int currentPosition = Microphone.GetPosition(microphoneDevice);

        int samplesAvailable = GetSampleDifference(lastSamplePosition, currentPosition);

        if (!isTalking)
        {
            lastSamplePosition = currentPosition;
            return;
        }

        if (samplesAvailable > frequency)
        {
            lastSamplePosition = currentPosition;
            return;
        }

        if (samplesAvailable >= chunkLength)
        {
            int chunksToSend = samplesAvailable / chunkLength;

            for (int i = 0; i < chunksToSend; i++)
            {
                float[] chunk = new float[chunkLength];
                ReadSafe(chunk, lastSamplePosition);
                SendAudioServerRpc(chunk);
                lastSamplePosition = (lastSamplePosition + chunkLength) % clipTotalSamples;
            }
        }
    }

    private int GetSampleDifference(int start, int end)
    {
        if (end >= start) return end - start;
        return (clipTotalSamples - start) + end;
    }

    private void ReadSafe(float[] targetArray, int offset)
    {
        int samplesToEnd = clipTotalSamples - offset;

        if (samplesToEnd >= targetArray.Length)
        {
            recordingClip.GetData(targetArray, offset);
        }
        else
        {
            float[] part1 = new float[samplesToEnd];
            recordingClip.GetData(part1, offset);

            int rest = targetArray.Length - samplesToEnd;
            float[] part2 = new float[rest];
            recordingClip.GetData(part2, 0);

            System.Array.Copy(part1, 0, targetArray, 0, part1.Length);
            System.Array.Copy(part2, 0, targetArray, part1.Length, part2.Length);
        }
    }

    [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
    private void SendAudioServerRpc(float[] audioData)
    {
        ReceiveAudioClientRpc(audioData);
    }

    [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]
    private void ReceiveAudioClientRpc(float[] audioData)
    {
        if (IsOwner) return;

        foreach (float sample in audioData)
        {
            audioBuffer.Enqueue(sample);
        }
    }

    private void ProcessPlaybackQueue()
    {
        if (audioBuffer.Count > 0 && !audioSource.isPlaying)
        {
            if (audioBuffer.Count < chunkLength * 3) return;

            int length = audioBuffer.Count;
            float[] playData = new float[length];
            for (int i = 0; i < length; i++)
            {
                playData[i] = audioBuffer.Dequeue();
            }

            AudioClip clip = AudioClip.Create("VoIP", length, 1, frequency, false);
            clip.SetData(playData, 0);

            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}