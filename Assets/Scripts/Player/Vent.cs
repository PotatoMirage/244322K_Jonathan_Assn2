using Unity.Netcode;
using UnityEngine;

public class Vent : Interactable
{
    [Header("Connections")]
    public Vent connectedVent;
    public Transform spawnLocation;

    [Header("Settings")]
    public float ventCooldown = 1.0f;
    private float lastVentTime;

    public override void OnInteract(ulong interactorId)
    {
        if (Time.time - lastVentTime < ventCooldown) return;

        if (IsServer)
        {
            if (GameManager.Instance.ImpostorId.Value != interactorId) return;

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
            client.PlayerObject.transform.position = targetPos;
            TeleportClientRpc(playerId, targetPos);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TeleportClientRpc(ulong playerId, Vector3 targetPos)
    {
        if (NetworkManager.Singleton.LocalClientId == playerId)
        {
            if (NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var playerTransform = NetworkManager.Singleton.LocalClient.PlayerObject.transform;
                playerTransform.position = targetPos;

                if (playerTransform.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }
}