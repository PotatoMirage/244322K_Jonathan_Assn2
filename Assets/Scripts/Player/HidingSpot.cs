using Unity.Netcode;
using UnityEngine;

public class HidingSpot : Interactable
{
    public Transform exitPoint;
    private NetworkVariable<ulong> occupantId = new NetworkVariable<ulong>(ulong.MaxValue);

    public override void OnInteract(ulong interactorId)
    {
        if (!IsServer) return;

        if (occupantId.Value == ulong.MaxValue)
        {
            occupantId.Value = interactorId;
            SetPlayerHiddenClientRpc(interactorId, true, transform.position);
        }
        else if (occupantId.Value == interactorId)
        {
            occupantId.Value = ulong.MaxValue;
            SetPlayerHiddenClientRpc(interactorId, false, exitPoint.position);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetPlayerHiddenClientRpc(ulong targetId, bool isHidden, Vector3 position)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(targetId, out NetworkClient client))
        {
            var playerObj = client.PlayerObject;
            if (playerObj != null)
            {
                playerObj.transform.position = position;

                var renderers = playerObj.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers) r.enabled = !isHidden;

                var col = playerObj.GetComponent<Collider>();
                if (col != null) col.enabled = !isHidden;

                var rb = playerObj.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = isHidden;

                var movement = playerObj.GetComponent<PlayerMovement>();
                if (movement != null) movement.enabled = !isHidden;
            }
        }
    }
}