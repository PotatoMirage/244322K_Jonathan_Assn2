using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class EmergencySystem : NetworkBehaviour
{
    public static EmergencySystem Instance;

    public NetworkVariable<bool> IsActive = new NetworkVariable<bool>(false);
    public NetworkVariable<float> Timer = new NetworkVariable<float>(30f); // Used for both cooldown and countdown
    public NetworkVariable<int> PlayersFixingCount = new NetworkVariable<int>(0);

    public TextMeshProUGUI warningText;

    // Track WHO is holding the button (Server Only)
    private HashSet<ulong> playersHolding = new HashSet<ulong>();

    private void Awake() { Instance = this; }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            IsActive.Value = false;
            Timer.Value = 30f; // Start with 30s cooldown
        }
    }

    private void Update()
    {
        // UI Updates (Client)
        if (IsActive.Value)
        {
            warningText.gameObject.SetActive(true);
            warningText.text = $"MELTDOWN IN: {Mathf.Ceil(Timer.Value)}s\nFix Required: {PlayersFixingCount.Value}/2";
            warningText.color = Color.red;
        }
        else
        {
            warningText.gameObject.SetActive(false);
            // Optional: Show "Next Emergency in X" for debug
        }

        // Logic (Server)
        if (!IsServer) return;

        Timer.Value -= Time.deltaTime;

        if (IsActive.Value)
        {
            // EMERGENCY PHASE (60s Limit)
            if (Timer.Value <= 0)
            {
                GameManager.Instance.EndGame(2); // Impostors Win (Time ran out)
            }

            // Check Win Condition (2 players fixing)
            if (PlayersFixingCount.Value >= 2)
            {
                ResolveEmergency();
            }
        }
        else
        {
            // COOLDOWN PHASE (30s Wait)
            if (Timer.Value <= 0)
            {
                StartEmergency();
            }
        }
    }

    private void StartEmergency()
    {
        IsActive.Value = true;
        Timer.Value = 60f; // 60s to fix it
        playersHolding.Clear();
        PlayersFixingCount.Value = 0;
    }

    private void ResolveEmergency()
    {
        IsActive.Value = false;
        Timer.Value = 30f; // Reset 30s cooldown
        playersHolding.Clear();
        PlayersFixingCount.Value = 0;
    }

    public void SetFixingState(ulong playerId, bool isFixing)
    {
        if (!IsServer) return;

        if (isFixing)
        {
            // Add player to Set (Set automatically handles uniqueness)
            playersHolding.Add(playerId);
        }
        else
        {
            playersHolding.Remove(playerId);
        }

        // Update the NetworkVariable count
        PlayersFixingCount.Value = playersHolding.Count;
    }
}