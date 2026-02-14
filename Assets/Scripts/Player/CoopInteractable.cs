using Unity.Netcode;
using UnityEngine;

public class CoopInteractable : Interactable
{
    private NetworkVariable<ulong> currentUser = new NetworkVariable<ulong>(ulong.MaxValue);

    public override void OnNetworkSpawn()
    {
        currentUser.OnValueChanged += OnStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        currentUser.OnValueChanged -= OnStateChanged;
    }

    public override void OnInteract(ulong interactorId)
    {
        ToggleInteractionServerRpc(interactorId);
    }

    [Rpc(SendTo.Server)]
    private void ToggleInteractionServerRpc(ulong interactorId)
    {
        if (EmergencySystem.Instance == null || !EmergencySystem.Instance.IsActive.Value) return;

        if (currentUser.Value == ulong.MaxValue)
        {
            currentUser.Value = interactorId;
            EmergencySystem.Instance.SetFixingState(interactorId, true);
        }
        else if (currentUser.Value == interactorId)
        {
            ResetButton();
        }
    }

    private void Update()
    {
        if (IsServer && currentUser.Value != ulong.MaxValue)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(currentUser.Value, out NetworkClient client))
            {
                PlayerMovement player = client.PlayerObject.GetComponent<PlayerMovement>();

                if (player == null || player.isDead.Value ||
                    Vector3.Distance(transform.position, player.transform.position) > 4.0f)
                {
                    ResetButton();
                }
            }
            else
            {
                ResetButton();
            }
        }
    }

    private void ResetButton()
    {
        if (currentUser.Value != ulong.MaxValue)
        {
            EmergencySystem.Instance.SetFixingState(currentUser.Value, false);
            currentUser.Value = ulong.MaxValue;
        }
    }

    private void OnStateChanged(ulong previous, ulong current)
    {
        bool isActive = current != ulong.MaxValue;

        if (TryGetComponent<Renderer>(out Renderer r))
        {
            r.material.color = isActive ? Color.green : Color.red;
        }
    }
}