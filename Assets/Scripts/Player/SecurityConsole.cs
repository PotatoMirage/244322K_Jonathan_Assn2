using Unity.Netcode;
using UnityEngine;

public class SecurityConsole : Interactable
{
    [Header("Security System")]
    public GameObject securityCameraUI; // Assign the Canvas Panel with RawImage
    public NetworkVariable<bool> IsInUse = new NetworkVariable<bool>(false);

    // Track who is currently using it
    private ulong currentUserId = ulong.MaxValue;

    public override void OnNetworkSpawn()
    {
        if (securityCameraUI != null) securityCameraUI.SetActive(false);
        IsInUse.OnValueChanged += OnUsageStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        IsInUse.OnValueChanged -= OnUsageStateChanged;
    }

    public override void OnInteract(ulong interactorId)
    {
        // Logic Flow: Client Request -> Server Validation
        RequestUseServerRpc(interactorId);
    }

    [Rpc(SendTo.Server)]
    private void RequestUseServerRpc(ulong interactorId)
    {
        // 1. If nobody is using it, let this player in
        if (!IsInUse.Value)
        {
            IsInUse.Value = true;
            currentUserId = interactorId;
            ToggleUIClientRpc(interactorId, true);
        }
        // 2. If THIS player is already using it, let them exit
        else if (IsInUse.Value && currentUserId == interactorId)
        {
            IsInUse.Value = false;
            currentUserId = ulong.MaxValue;
            ToggleUIClientRpc(interactorId, false);
        }
        // 3. If someone else is using it, ignore (or play "Occupied" sound)
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ToggleUIClientRpc(ulong targetId, bool isOpen)
    {
        // Only open the UI for the specific player interacting
        if (NetworkManager.Singleton.LocalClientId == targetId)
        {
            if (securityCameraUI != null) securityCameraUI.SetActive(isOpen);

            // Optional: Disable player movement while viewing cameras
            var player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.enabled = !isOpen; // Or create a dedicated input lock method
            }
        }
    }

    private void OnUsageStateChanged(bool prev, bool current)
    {
        // Visual feedback for ALL players (e.g., turn monitor screen green/red)
        // This makes it a "Networked" feature because others see the station is busy.
        GetComponent<Renderer>().material.color = current ? Color.green : Color.white;
    }
}