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
        if (!IsServer) return;

        if (!manager.IsEmergencyActive.Value) return;

        IsBeingHeld.Value = !IsBeingHeld.Value;
        manager.CheckConsoles();

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