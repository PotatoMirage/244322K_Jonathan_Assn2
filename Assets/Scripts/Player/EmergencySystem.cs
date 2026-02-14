using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class EmergencySystem : NetworkBehaviour
{
    public static EmergencySystem Instance;

    public NetworkVariable<bool> IsActive = new NetworkVariable<bool>(false);
    public NetworkVariable<float> Timer = new NetworkVariable<float>(30f);
    public NetworkVariable<int> PlayersFixingCount = new NetworkVariable<int>(0);

    public TextMeshProUGUI warningText;

    private HashSet<ulong> playersHolding = new HashSet<ulong>();

    private void Awake() { Instance = this; }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            IsActive.Value = false;
            Timer.Value = 30f;
        }
    }

    private void Update()
    {
        if (IsActive.Value)
        {
            warningText.gameObject.SetActive(true);
            warningText.text = $"MELTDOWN IN: {Mathf.Ceil(Timer.Value)}s\nFix Required: {PlayersFixingCount.Value}/2";
            warningText.color = Color.red;
        }
        else
        {
            warningText.gameObject.SetActive(false);
        }

        if (!IsServer) return;

        Timer.Value -= Time.deltaTime;

        if (IsActive.Value)
        {
            if (Timer.Value <= 0)
            {
                GameManager.Instance.EndGame(2);
            }

            if (PlayersFixingCount.Value >= 2)
            {
                ResolveEmergency();
            }
        }
        else
        {
            if (Timer.Value <= 0)
            {
                StartEmergency();
            }
        }
    }

    private void StartEmergency()
    {
        IsActive.Value = true;
        Timer.Value = 60f;
        playersHolding.Clear();
        PlayersFixingCount.Value = 0;
    }

    private void ResolveEmergency()
    {
        IsActive.Value = false;
        Timer.Value = 30f;
        playersHolding.Clear();
        PlayersFixingCount.Value = 0;
    }

    public void SetFixingState(ulong playerId, bool isFixing)
    {
        if (!IsServer) return;

        if (isFixing)
        {
            playersHolding.Add(playerId);
        }
        else
        {
            playersHolding.Remove(playerId);
        }

        PlayersFixingCount.Value = playersHolding.Count;
    }
}