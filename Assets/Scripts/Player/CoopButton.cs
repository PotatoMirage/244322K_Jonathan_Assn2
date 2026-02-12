using Unity.Netcode;
using UnityEngine;

public class CoopButton : Interactable
{
    public NetworkVariable<bool> IsPressed = new NetworkVariable<bool>(false);
    public MeshRenderer buttonRenderer;
    public Material pressedMaterial;
    public Material unpressedMaterial;

    public override void OnNetworkSpawn()
    {
        IsPressed.OnValueChanged += OnPressStateChanged;
        UpdateVisuals(IsPressed.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsPressed.OnValueChanged -= OnPressStateChanged;
    }

    public override void OnInteract(ulong interactorId)
    {
        if (!IsServer) return;
        IsPressed.Value = !IsPressed.Value;
    }

    private void OnPressStateChanged(bool prev, bool current)
    {
        UpdateVisuals(current);
    }

    private void UpdateVisuals(bool isPressed)
    {
        if (buttonRenderer != null)
        {
            buttonRenderer.material = isPressed ? pressedMaterial : unpressedMaterial;
        }
    }
}