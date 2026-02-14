using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class NetworkVoiceChat : NetworkBehaviour
{
    [Header("Settings")]
    [Tooltip("Lower quality (16000) is required for smooth Netcode transmission.")]
    public int frequency = 16000;

    [Tooltip("Size of one packet. Must be < 300 to fit in UDP MTU.")]
    public int chunkLength = 200; // 200 floats = 800 bytes (Safe)

    public KeyCode pushToTalkKey = KeyCode.V;
    public bool usePushToTalk = true;

    [Header("References")]
    private AudioSource audioSource;
    private AudioClip recordingClip;
    private string microphoneDevice;

    private int lastSamplePosition = 0;
    private int clipTotalSamples;

    // Playback Buffer
    private Queue<float> audioBuffer = new Queue<float>();

    public override void OnNetworkSpawn()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f; // 3D Audio

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
            // Record 1 second looping. 16000 samples total.
            recordingClip = Microphone.Start(microphoneDevice, true, 1, frequency);
            clipTotalSamples = recordingClip.samples;
            Debug.Log($"Mic Init: {microphoneDevice} | Freq: {frequency}");
        }
        else
        {
            Debug.LogError("No Microphone detected!");
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

        // 1. Calculate how many samples exist between Last and Current
        int samplesAvailable = GetSampleDifference(lastSamplePosition, currentPosition);

        // 2. STOP TALKING LOGIC
        if (!isTalking)
        {
            // Just move the pointer forward so we don't send old audio later
            lastSamplePosition = currentPosition;
            return;
        }

        // 3. LAG SPIKE SAFETY (The Fix for Overflow)
        // If we have > 1000 samples (approx 60ms) buffered, it means we lagged.
        // attempting to send 1000+ samples will crash the RPC.
        // Solution: Skip the old audio and jump to now.
        if (samplesAvailable > 1000)
        {
            lastSamplePosition = currentPosition;
            return;
        }

        // 4. SEND DATA IN CHUNKS
        // Only send if we have a full chunk ready
        if (samplesAvailable >= chunkLength)
        {
            float[] chunk = new float[chunkLength];

            // Read data safely handling wrap-around
            ReadSafe(chunk, lastSamplePosition);

            // Send RPC
            SendAudioServerRpc(chunk);

            // Advance pointer exactly by chunk size
            lastSamplePosition = (lastSamplePosition + chunkLength) % clipTotalSamples;
        }
    }

    // Helper to handle the "Loop" math of the microphone buffer
    private int GetSampleDifference(int start, int end)
    {
        if (end >= start) return end - start;
        return (clipTotalSamples - start) + end;
    }

    // Helper to read wrapping data without crashing
    private void ReadSafe(float[] targetArray, int offset)
    {
        int samplesToEnd = clipTotalSamples - offset;

        if (samplesToEnd >= targetArray.Length)
        {
            // Easy case: No wrapping needed
            recordingClip.GetData(targetArray, offset);
        }
        else
        {
            // Hard case: We are at the end of the buffer, need to wrap to start
            // 1. Read to the end
            float[] part1 = new float[samplesToEnd];
            recordingClip.GetData(part1, offset);

            // 2. Read the rest from the beginning
            int rest = targetArray.Length - samplesToEnd;
            float[] part2 = new float[rest];
            recordingClip.GetData(part2, 0);

            // 3. Combine
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
            // buffer at least 3 chunks before playing to prevent stutter
            if (audioBuffer.Count < chunkLength * 3) return;

            // Consume buffer
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