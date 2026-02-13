using Unity.Netcode;
using UnityEngine;

public class EmergencyConsole : Interactable
{
    public EmergencyManager manager;
    public NetworkVariable<bool> IsBeingHeld = new NetworkVariable<bool>(false);

    // --- NEW VARIABLE ---
    public ulong InteractorId = ulong.MaxValue; // Track who is holding this

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

    // --- UPDATED METHOD ---
    public override void OnInteract(ulong interactorId)
    {
        if (!IsServer) return;

        if (!manager.IsEmergencyActive.Value) return;

        // Check if this player is already holding the OTHER console
        if (manager.IsPlayerBusyWithOtherConsole(interactorId, this)) return;

        IsBeingHeld.Value = !IsBeingHeld.Value;

        if (IsBeingHeld.Value)
        {
            // Lock this console to this player
            InteractorId = interactorId;
            manager.CheckConsoles();
            Invoke(nameof(ResetConsole), 5.0f);
        }
        else
        {
            // Player cancelled manually
            InteractorId = ulong.MaxValue;
            manager.CheckConsoles();
        }
    }

    // --- UPDATED METHOD ---
    private void ResetConsole()
    {
        IsBeingHeld.Value = false;
        InteractorId = ulong.MaxValue; // Reset the ID so they can interact again later
    }

    private void OnStateChanged(bool prev, bool current)
    {
        if (consoleRenderer != null)
        {
            consoleRenderer.material = current ? activeMat : inactiveMat;
        }
    }
}