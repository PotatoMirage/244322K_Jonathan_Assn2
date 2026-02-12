using UnityEngine;
using Unity.Netcode;

public class TrapObject : NetworkBehaviour
{
    [Header("Trap Settings")]
    public bool isOneTimeUse = false;
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        // 1. Server Authority Only
        if (!IsServer) return;

        if (hasTriggered && isOneTimeUse) return;

        // 2. Logic: Did a player touch me?
        if (other.TryGetComponent<PlayerMovement>(out var player))
        {
            // 3. Logic: Is that player capable of triggering traps? (Alive?)
            if (!player.isDead.Value)
            {
                if (GlobalEventManager.Instance != null)
                {
                    GlobalEventManager.Instance.TriggerLightsSabotage();
                    hasTriggered = true;

                    // Send visual/audio feedback to all clients
                    TriggerVisualsClientRpc();
                }
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerVisualsClientRpc()
    {
        Debug.Log("Trap Visuals Playing!");
        // Add AudioSource.Play() or ParticleSystem.Play() here
    }
}