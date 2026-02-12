using Unity.Netcode;
using UnityEngine;

public class EmergencyConsole : Interactable
{
    public EmergencyManager manager;
    public NetworkVariable<bool> IsBeingHeld = new NetworkVariable<bool>(false);
    public MeshRenderer consoleRenderer;
    public Material activeMat;
    public Material inactiveMat;

    public override void OnNetworkSpawn()
    {
        IsBeingHeld.OnValueChanged += OnStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        IsBeingHeld.OnValueChanged -= OnStateChanged;
    }

    public override void OnInteract(ulong interactorId)
    {
        InteractServerRpc(interactorId);
    }

    [Rpc(SendTo.Server)]
    private void InteractServerRpc(ulong interactorId)
    {
        if (!manager.IsEmergencyActive.Value) return;

        IsBeingHeld.Value = !IsBeingHeld.Value;
        manager.CheckConsoles();

        // Auto-release after 5 seconds if not synced (optional game design choice)
        if (IsBeingHeld.Value)
        {
            Invoke(nameof(ResetConsole), 5.0f);
        }
    }

    private void ResetConsole()
    {
        IsBeingHeld.Value = false;
    }

    private void OnStateChanged(bool prev, bool current)
    {
        if (consoleRenderer != null)
        {
            consoleRenderer.material = current ? activeMat : inactiveMat;
        }
    }
}