using Unity.Netcode;
using UnityEngine;
using TMPro;

public class EmergencyManager : NetworkBehaviour
{
    [Header("References")]
    public EmergencyConsole consoleA;
    public EmergencyConsole consoleB;
    public TextMeshProUGUI warningText;

    [Header("Settings")]
    public float emergencyDuration = 30f;
    public float cooldownBetweenEmergencies = 20f;

    public NetworkVariable<bool> IsEmergencyActive = new NetworkVariable<bool>(false);
    public NetworkVariable<float> TimeRemaining = new NetworkVariable<float>(30f);

    private float cooldownTimer;

    public override void OnNetworkSpawn()
    {
        if (IsServer) cooldownTimer = cooldownBetweenEmergencies;
    }

    private void Update()
    {
        UpdateUI();

        if (!IsServer || GameManager.Instance == null) return;

        // Only run during Gameplay
        if (GameManager.Instance.CurrentState.Value != GameManager.GameState.Gameplay)
        {
            IsEmergencyActive.Value = false;
            return;
        }

        if (IsEmergencyActive.Value)
        {
            TimeRemaining.Value -= Time.deltaTime;
            if (TimeRemaining.Value <= 0) TriggerFailure();
        }
        else
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0) StartEmergency();
        }
    }

    private void StartEmergency()
    {
        IsEmergencyActive.Value = true;
        TimeRemaining.Value = emergencyDuration;
        if (consoleA) consoleA.IsBeingHeld.Value = false;
        if (consoleB) consoleB.IsBeingHeld.Value = false;
    }

    public void CheckConsoles()
    {
        if (!IsEmergencyActive.Value) return;
        bool a = consoleA != null && consoleA.IsBeingHeld.Value;
        bool b = consoleB != null && consoleB.IsBeingHeld.Value;

        if (a && b) ResolveEmergency();
    }

    private void ResolveEmergency()
    {
        IsEmergencyActive.Value = false;
        cooldownTimer = cooldownBetweenEmergencies;
    }

    private void TriggerFailure()
    {
        IsEmergencyActive.Value = false;
        GameManager.Instance.EndGame(2); // Impostor Win
    }

    private void UpdateUI()
    {
        if (warningText == null) return;
        bool active = IsEmergencyActive.Value;
        warningText.gameObject.SetActive(active);

        if (active)
        {
            float time = Mathf.Ceil(TimeRemaining.Value);
            warningText.text = $"CRITICAL FAILURE!\nRESET BOTH CONSOLES: {time}";
            warningText.color = time <= 10f ? Color.red : Color.yellow;
        }
    }
}