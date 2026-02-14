using UnityEngine;
using Unity.Netcode;

public class TrapObject : NetworkBehaviour
{
    [Header("Trap Settings")]
    public bool isOneTimeUse = false;
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (hasTriggered && isOneTimeUse) return;

        if (other.TryGetComponent<PlayerMovement>(out PlayerMovement player))
        {
            if (!player.isDead.Value)
            {
                if (GlobalEventManager.Instance != null)
                {
                    GlobalEventManager.Instance.TriggerLightsSabotage();
                    hasTriggered = true;

                    TriggerVisualsClientRpc();
                }
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerVisualsClientRpc()
    {
    }
}