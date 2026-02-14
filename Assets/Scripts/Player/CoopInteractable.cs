using Unity.Netcode;
using UnityEngine;

public class CoopInteractable : Interactable
{
    // Tracks WHO is currently holding this button. ulong.MaxValue = Empty.
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
        // Client requests to toggle the button
        ToggleInteractionServerRpc(interactorId);
    }

    [Rpc(SendTo.Server)]
    private void ToggleInteractionServerRpc(ulong interactorId)
    {
        // 1. If System isn't active, do nothing
        if (EmergencySystem.Instance == null || !EmergencySystem.Instance.IsActive.Value) return;

        // 2. LOGIC: Toggle State
        if (currentUser.Value == ulong.MaxValue)
        {
            // Case A: Button is empty -> Player claims it
            currentUser.Value = interactorId;
            EmergencySystem.Instance.SetFixingState(interactorId, true);
        }
        else if (currentUser.Value == interactorId)
        {
            // Case B: Player is already holding it -> Player releases it
            ResetButton();
        }
        // Case C: Someone else is holding it -> Do nothing
    }

    private void Update()
    {
        // SERVER ONLY: Monitor the player who pressed the button
        if (IsServer && currentUser.Value != ulong.MaxValue)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(currentUser.Value, out NetworkClient client))
            {
                var player = client.PlayerObject.GetComponent<PlayerMovement>();

                // If player disconnected, died, or walked too far away -> Reset
                if (player == null || player.isDead.Value ||
                    Vector3.Distance(transform.position, player.transform.position) > 4.0f)
                {
                    ResetButton();
                }
            }
            else
            {
                // Client likely disconnected
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

    // React to state changes (Visuals)
    private void OnStateChanged(ulong previous, ulong current)
    {
        bool isActive = current != ulong.MaxValue;

        // Visual Feedback: Green if pressed, Red if waiting
        if (TryGetComponent<Renderer>(out var r))
        {
            r.material.color = isActive ? Color.green : Color.red;
        }
    }
}