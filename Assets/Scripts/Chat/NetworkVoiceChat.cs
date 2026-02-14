using System.Collections;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

public class NetworkVoiceChat : NetworkBehaviour
{
    // SETTINGS: Lower frequency to 8000 to save network bandwidth (Critical!)
    private int frequency = 8000;
    private int recordTime = 1; // Short buffer

    private string microphoneDevice;
    private AudioClip recordingClip;
    private int lastSamplePosition = 0;
    private bool isRecording = false;

    public AudioSource audioSource;

    public bool IsSpeaking { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        audioSource.spatialBlend = 1.0f;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 20f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;

        if (IsOwner)
        {
            InitializeMicrophone();
        }
    }

    private void InitializeMicrophone()
    {
        StartCoroutine(WaitForMicrophone());
    }

    private IEnumerator WaitForMicrophone()
    {
        float timeout = 3f;
        while (Microphone.devices.Length <= 0 && timeout > 0)
        {
            yield return new WaitForSeconds(0.2f);
            timeout -= 0.2f;
        }

        if (Microphone.devices.Length > 0)
        {
            microphoneDevice = Microphone.devices[0];
            Debug.Log($"[Voice] Mic Found: {microphoneDevice}");

            recordingClip = Microphone.Start(microphoneDevice, true, 10, frequency);
            isRecording = true;
        }
        else
        {
            Debug.LogError("[Voice] No Microphone detected!");
        }
    }

    private void Update()
    {
        if (!IsOwner || !isRecording) return;

        int currentPosition = Microphone.GetPosition(microphoneDevice);

        if (currentPosition < 0 || lastSamplePosition == currentPosition) return;

        int sampleCount = (currentPosition - lastSamplePosition + recordingClip.samples) % recordingClip.samples;

        if (sampleCount > frequency / 10)
        {
            float[] samples = new float[sampleCount];
            recordingClip.GetData(samples, lastSamplePosition);

            if (samples.Max(x => Mathf.Abs(x)) > 0.01f)
            {
                SendVoiceDataServerRpc(samples);
            }

            lastSamplePosition = currentPosition;
        }
    }

    [Rpc(SendTo.Server)]
    private void SendVoiceDataServerRpc(float[] data)
    {
        // Server forwards to all OTHER clients
        PlayVoiceDataClientRpc(data);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayVoiceDataClientRpc(float[] data)
    {
        if (!IsOwner)
        {
            PlayVoiceData(data);
        }
    }

    private void PlayVoiceData(float[] data)
    {
        AudioClip clip = AudioClip.Create("VoiceChunk", data.Length, 1, frequency, false);
        clip.SetData(data, 0);

        audioSource.PlayOneShot(clip);

        IsSpeaking = true;
        StopCoroutine(nameof(ResetSpeaking));
        StartCoroutine(nameof(ResetSpeaking));
    }

    private IEnumerator ResetSpeaking()
    {
        yield return new WaitForSeconds(0.2f);
        IsSpeaking = false;
    }
}