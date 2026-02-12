using Unity.Netcode;
using UnityEngine;

public class Vent : Interactable
{
    [Header("Connections")]
    public Vent connectedVent;
    public Transform spawnLocation; // Assign a child empty object where player appears

    [Header("Settings")]
    public float ventCooldown = 1.0f;
    private float lastVentTime;

    public override void OnInteract(ulong interactorId)
    {
        // 1. Check Cooldown
        if (Time.time - lastVentTime < ventCooldown) return;

        // 2. Server-side check: Is this player the Impostor?
        if (IsServer)
        {
            if (GameManager.Instance.ImpostorId.Value != interactorId)
            {
                // Optional: Play "Access Denied" sound for Crewmates
                return;
            }

            // 3. Teleport logic
            if (connectedVent != null && connectedVent.spawnLocation != null)
            {
                TeleportPlayer(interactorId, connectedVent.spawnLocation.position);
                lastVentTime = Time.time;
            }
        }
    }

    private void TeleportPlayer(ulong playerId, Vector3 targetPos)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out NetworkClient client))
        {
            var playerTransform = client.PlayerObject.transform;

            // Disable CharacterController/Physics momentarily to prevent glitches
            client.PlayerObject.transform.position = targetPos;

            // Sync with client
            TeleportClientRpc(playerId, targetPos);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TeleportClientRpc(ulong playerId, Vector3 targetPos)
    {
        if (NetworkManager.Singleton.LocalClientId == playerId)
        {
            transform.position = targetPos;
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
    }
}