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
            SetPlayerHiddenClientRpc(interactorId, true);
        }
        else if (occupantId.Value == interactorId)
        {
            occupantId.Value = ulong.MaxValue;
            SetPlayerHiddenClientRpc(interactorId, false);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetPlayerHiddenClientRpc(ulong targetId, bool isHidden)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(targetId, out NetworkClient client))
        {
            if (client.PlayerObject != null)
            {
                var playerTransform = client.PlayerObject.transform;
                var rb = client.PlayerObject.GetComponent<Rigidbody>();
                var col = client.PlayerObject.GetComponent<Collider>();
                var movement = client.PlayerObject.GetComponent<PlayerMovement>();
                var renderers = client.PlayerObject.GetComponentsInChildren<Renderer>();

                if (isHidden)
                {
                    playerTransform.position = transform.position;
                    if (rb) rb.isKinematic = true;
                    if (col) col.enabled = false;
                    if (movement) movement.enabled = false;
                    foreach (var r in renderers) r.enabled = false;
                }
                else
                {
                    playerTransform.position = exitPoint.position;
                    if (rb) rb.isKinematic = false;
                    if (col) col.enabled = true;
                    if (movement) movement.enabled = true;
                    foreach (var r in renderers) r.enabled = true;
                }
            }
        }
    }
}