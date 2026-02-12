using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    // Time in seconds before the game starts
    public NetworkVariable<float> CountdownTimer = new NetworkVariable<float>(5.0f);

    // Tracks if the match has officially begun
    public NetworkVariable<bool> IsGameActive = new NetworkVariable<bool>(false);

    // Syncs the Impostor's ID to all players (Initialized to max value = no impostor)
    public NetworkVariable<ulong> ImpostorId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Initialize game state on the server
            IsGameActive.Value = false;
            CountdownTimer.Value = 5.0f; // 5 Seconds countdown
        }
    }

    void Update()
    {
        // Only the Server updates the timer
        if (IsServer)
        {
            if (!IsGameActive.Value)
            {
                CountdownTimer.Value -= Time.deltaTime;
                if (CountdownTimer.Value <= 0f)
                {
                    StartGame();
                }
            }
        }
    }

    void StartGame()
    {
        IsGameActive.Value = true;
        PickImpostor();
    }

    void PickImpostor()
    {
        if (NetworkManager.Singleton.ConnectedClientsIds.Count > 0)
        {
            List<ulong> clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            int randomIndex = Random.Range(0, clientIds.Count);
            ImpostorId.Value = clientIds[randomIndex];
            Debug.Log($"Game Started! Player {ImpostorId.Value} is the Impostor.");
        }
    }
}