using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public NetworkVariable<float> CountdownTimer = new NetworkVariable<float>(5.0f);
    public NetworkVariable<bool> IsGameActive = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> ImpostorId = new NetworkVariable<ulong>(ulong.MaxValue);

    public NetworkVariable<int> TotalTasks = new NetworkVariable<int>(0);
    public NetworkVariable<int> CompletedTasks = new NetworkVariable<int>(0);
    public NetworkVariable<int> CrewmatesAlive = new NetworkVariable<int>(0);

    public NetworkVariable<int> GameResult = new NetworkVariable<int>(0);

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
            IsGameActive.Value = false;
            CountdownTimer.Value = 5.0f;
            GameResult.Value = 0;
        }
    }

    void Update()
    {
        if (IsServer)
        {
            if (!IsGameActive.Value && GameResult.Value == 0)
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

        var clients = NetworkManager.Singleton.ConnectedClientsIds;
        CrewmatesAlive.Value = clients.Count - 1;

        PickImpostor();
    }

    void PickImpostor()
    {
        if (NetworkManager.Singleton.ConnectedClientsIds.Count > 0)
        {
            List<ulong> clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            int randomIndex = Random.Range(0, clientIds.Count);
            ImpostorId.Value = clientIds[randomIndex];
        }
    }

    public void RegisterTask()
    {
        if (IsServer) TotalTasks.Value++;
    }

    public void CompleteTask()
    {
        if (!IsServer || !IsGameActive.Value) return;

        CompletedTasks.Value++;
        CheckWinCondition();
    }

    public void OnPlayerDied(ulong clientId)
    {
        if (!IsServer || !IsGameActive.Value) return;

        if (clientId != ImpostorId.Value)
        {
            CrewmatesAlive.Value--;
        }

        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        if (CompletedTasks.Value >= TotalTasks.Value && TotalTasks.Value > 0)
        {
            EndGame(1);
        }
        else if (CrewmatesAlive.Value <= 0)
        {
            EndGame(2);
        }
    }

    private void EndGame(int result)
    {
        IsGameActive.Value = false;
        GameResult.Value = result;
        EndGameClientRpc(result);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EndGameClientRpc(int result)
    {
        Debug.Log(result == 1 ? "Crewmates Win!" : "Impostor Wins!");
    }
}