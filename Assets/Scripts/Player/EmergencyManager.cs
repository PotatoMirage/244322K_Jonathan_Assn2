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
    public float cooldownBetweenEmergencies = 20f; // Time before it starts randomly

    public NetworkVariable<bool> IsEmergencyActive = new NetworkVariable<bool>(false);
    public NetworkVariable<float> TimeRemaining = new NetworkVariable<float>(30f);

    private float cooldownTimer;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            cooldownTimer = cooldownBetweenEmergencies;
        }
    }

    private void Update()
    {
        UpdateUI();

        if (!IsServer) return;

        // Don't run emergency if game hasn't started or is over
        if (GameManager.Instance == null || !GameManager.Instance.IsGameActive.Value) return;
        // Don't run emergency during meetings
        if (GameManager.Instance.IsMeetingActive.Value)
        {
            IsEmergencyActive.Value = false;
            return;
        }

        if (IsEmergencyActive.Value)
        {
            TimeRemaining.Value -= Time.deltaTime;

            // --- FIX: End game if time runs out ---
            if (TimeRemaining.Value <= 0)
            {
                TriggerFailure();
            }
        }
        else
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                StartEmergency();
            }
        }
    }

    private void StartEmergency()
    {
        IsEmergencyActive.Value = true;
        TimeRemaining.Value = emergencyDuration;

        // Reset console states
        if (consoleA != null) consoleA.IsBeingHeld.Value = false;
        if (consoleB != null) consoleB.IsBeingHeld.Value = false;
    }

    public void CheckConsoles()
    {
        if (!IsEmergencyActive.Value) return;

        bool a = consoleA != null && consoleA.IsBeingHeld.Value;
        bool b = consoleB != null && consoleB.IsBeingHeld.Value;

        // If both are held simultaneously
        if (a && b)
        {
            ResolveEmergency();
        }
    }

    private void ResolveEmergency()
    {
        IsEmergencyActive.Value = false;
        cooldownTimer = cooldownBetweenEmergencies;
    }

    private void TriggerFailure()
    {
        IsEmergencyActive.Value = false;

        Debug.Log("Crisis Failed! Impostors Win.");

        // --- FIX: Call GameManager to End the Game ---
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndGame(2); // 2 = Impostor Win
        }
    }

    private void UpdateUI()
    {
        if (warningText == null) return;

        if (IsEmergencyActive.Value)
        {
            warningText.gameObject.SetActive(true);
            float time = Mathf.Ceil(TimeRemaining.Value);
            warningText.text = $"CRITICAL FAILURE DETECTED!\nRESET BOTH CONSOLES: {time}";
            warningText.color = (time <= 10f) ? Color.red : Color.yellow;
        }
        else
        {
            warningText.gameObject.SetActive(false);
        }
    }
}