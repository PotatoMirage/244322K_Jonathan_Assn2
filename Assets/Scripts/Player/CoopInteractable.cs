using Unity.Netcode;
using UnityEngine;

public class CoopInteractable : Interactable
{
    private bool isHolding = false;

    public override void OnInteract(ulong interactorId)
    {
        // Toggle holding state
        if (interactorId == NetworkManager.Singleton.LocalClientId)
        {
            isHolding = !isHolding;
            SetHoldStateServerRpc(isHolding);
        }
    }

    [Rpc(SendTo.Server)]
    private void SetHoldStateServerRpc(bool holding, RpcParams rpcParams = default)
    {
        if (!EmergencySystem.Instance.IsActive.Value) return;

        ulong senderId = rpcParams.Receive.SenderClientId; // Get exact ID

        // Pass ID to manager
        EmergencySystem.Instance.SetFixingState(senderId, holding);

        UpdateVisualsClientRpc(holding);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateVisualsClientRpc(bool active)
    {
        GetComponent<Renderer>().material.color = active ? Color.yellow : Color.red;
    }

    // Logic to auto-release if player walks away
    private void Update()
    {
        if (IsServer && isHolding)
        {
            // If player walks away while holding, force release
            // (You would need to track WHO is holding to check distance)
        }
    }
}