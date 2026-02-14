using Unity.Netcode;
using UnityEngine;

public class ProximityVoiceController : NetworkBehaviour
{
    [SerializeField] private GameObject speakingIndicator;

    private NetworkVoiceChat voiceChat;

    private void Awake()
    {
        voiceChat = GetComponent<NetworkVoiceChat>();
        if (speakingIndicator != null) speakingIndicator.SetActive(false);
    }

    private void Update()
    {
        if (voiceChat == null) return;

        if (speakingIndicator != null)
        {
            speakingIndicator.SetActive(voiceChat.IsSpeaking);

            if (Camera.main != null)
            {
                speakingIndicator.transform.LookAt(Camera.main.transform);
            }
        }
    }
}