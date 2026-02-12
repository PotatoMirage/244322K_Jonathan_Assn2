using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class GlobalEventManager : NetworkBehaviour
{
    public static GlobalEventManager Instance { get; private set; }

    public NetworkVariable<bool> IsLightsSabotaged = new NetworkVariable<bool>(false);

    [Header("Ambient Settings")]
    public Color normalAmbient = Color.white;
    public Color sabotageAmbient = new Color(0.1f, 0.1f, 0.1f, 1f); // Very Dark/Red

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        // Listen for changes
        IsLightsSabotaged.OnValueChanged += OnSabotageChanged;

        // Initialize
        UpdateAmbientLight(IsLightsSabotaged.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsLightsSabotaged.OnValueChanged -= OnSabotageChanged;
    }

    public void TriggerLightsSabotage()
    {
        if (IsServer)
        {
            IsLightsSabotaged.Value = true;
            Debug.Log("Sabotage Triggered!");
            StartCoroutine(RestoreLightsRoutine());
        }
    }

    private IEnumerator RestoreLightsRoutine()
    {
        yield return new WaitForSeconds(10f); // 10 seconds of darkness
        IsLightsSabotaged.Value = false;
    }

    private void OnSabotageChanged(bool prev, bool current)
    {
        UpdateAmbientLight(current);
    }

    private void UpdateAmbientLight(bool isSabotaged)
    {
        // This changes the lighting for EVERYONE locally based on the NetworkVariable
        RenderSettings.ambientLight = isSabotaged ? sabotageAmbient : normalAmbient;

        // Optional: Force update if using Realtime GI
        DynamicGI.UpdateEnvironment();
    }
}