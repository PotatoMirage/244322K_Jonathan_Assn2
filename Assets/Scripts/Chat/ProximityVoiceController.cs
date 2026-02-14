using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ProximityVoiceController : NetworkBehaviour
{
    private AudioSource voiceSource;

    [Header("Proximity Settings")]
    public float minDistance = 2f;
    public float maxDistance = 15f; // Radius for hearing voice

    public override void OnNetworkSpawn()
    {
        voiceSource = GetComponent<AudioSource>();

        // Setup 3D Sound settings
        voiceSource.spatialBlend = 1.0f; // 1.0 = Full 3D
        voiceSource.rolloffMode = AudioRolloffMode.Linear;
        voiceSource.minDistance = minDistance;
        voiceSource.maxDistance = maxDistance;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentState.OnValueChanged += OnStateChanged;
            ApplyVoiceRules(GameManager.Instance.CurrentState.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentState.OnValueChanged -= OnStateChanged;
        }
    }

    private void OnStateChanged(GameManager.GameState prev, GameManager.GameState next)
    {
        ApplyVoiceRules(next);
    }

    private void ApplyVoiceRules(GameManager.GameState state)
    {
        // Rule 3: "Only during gameplay should there be proximity voice chat"

        if (state == GameManager.GameState.Gameplay)
        {
            // Enable Proximity
            voiceSource.mute = false;
            voiceSource.spatialBlend = 1.0f; // 3D Sound (Proximity)
            voiceSource.enabled = true;
        }
        else if (state == GameManager.GameState.Voting || state == GameManager.GameState.Meeting)
        {
            // The prompt says "Only during gameplay should there be proximity". 
            // This implies voice is either OFF or GLOBAL in meetings.
            // Usually, standard logic is Global Voice in meetings.
            // If you want Global Voice in meetings:
            // voiceSource.spatialBlend = 0.0f; // 2D Sound (Global)

            // If you want NO Voice in meetings (Text only per your emphasis):
            voiceSource.mute = true;
        }
        else
        {
            voiceSource.mute = true; // Silence in lobby/end screen if desired
        }
    }
}