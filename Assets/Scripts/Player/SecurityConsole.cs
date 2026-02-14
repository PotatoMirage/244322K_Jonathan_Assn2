using Unity.Netcode;
using UnityEngine;

public class SecurityConsole : Interactable
{
    [Header("Security System")]
    public GameObject securityCameraUI;
    public NetworkVariable<bool> IsInUse = new(false);

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
        RequestUseServerRpc(interactorId);
    }

    [Rpc(SendTo.Server)]
    private void RequestUseServerRpc(ulong interactorId)
    {
        if (!IsInUse.Value)
        {
            IsInUse.Value = true;
            currentUserId = interactorId;
            ToggleUIClientRpc(interactorId, true);
        }
        else if (IsInUse.Value && currentUserId == interactorId)
        {
            IsInUse.Value = false;
            currentUserId = ulong.MaxValue;
            ToggleUIClientRpc(interactorId, false);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ToggleUIClientRpc(ulong targetId, bool isOpen)
    {
        if (NetworkManager.Singleton.LocalClientId == targetId)
        {
            if (securityCameraUI != null) securityCameraUI.SetActive(isOpen);

            PlayerMovement player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.enabled = !isOpen;
            }
        }
    }

    private void OnUsageStateChanged(bool prev, bool current)
    {
        GetComponent<Renderer>().material.color = current ? Color.green : Color.white;
    }
}