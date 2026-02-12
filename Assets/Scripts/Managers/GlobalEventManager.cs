using Unity.Netcode;
using UnityEngine;

public class GlobalEventManager : NetworkBehaviour
{
    public static GlobalEventManager Instance;

    [Header("Ambient Light Settings")]
    public Color normalAmbient = new Color(1.0f, 1.0f, 1.0f);
    public Color sabotageAmbient = Color.black;

    public NetworkVariable<bool> IsLightsSabotaged = new NetworkVariable<bool>(false);

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // Store the initial lighting so we can revert to it
        if (IsServer)
        {
            // Optional: Capture current scene settings on start
            // normalAmbient = RenderSettings.ambientLight; 
        }

        IsLightsSabotaged.OnValueChanged += OnLightsChanged;

        // Apply immediate state for late-joiners
        ApplyLighting(IsLightsSabotaged.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsLightsSabotaged.OnValueChanged -= OnLightsChanged;
    }

    // Called by Impostor (UI Button or Keybind)
    public void TriggerLightsSabotage()
    {
        if (IsServer) IsLightsSabotaged.Value = true;
    }

    // Called by Crewmates (Fixing the fuse box)
    public void FixLights()
    {
        if (IsServer) IsLightsSabotaged.Value = false;
    }

    private void OnLightsChanged(bool prev, bool isSabotaged)
    {
        ApplyLighting(isSabotaged);
    }

    private void ApplyLighting(bool isSabotaged)
    {
        RenderSettings.ambientLight = isSabotaged ? sabotageAmbient : normalAmbient;

        // Optional: If you use Fog, you might want to darken it too
        // RenderSettings.fogColor = isSabotaged ? Color.black : normalFogColor;
    }
}