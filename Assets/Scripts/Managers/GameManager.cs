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

    // Meeting State
    public NetworkVariable<bool> IsMeetingActive = new NetworkVariable<bool>(false);
    public NetworkVariable<float> MeetingTimer = new NetworkVariable<float>(0f);

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
            IsMeetingActive.Value = false;
        }
    }

    void Update()
    {
        if (!IsServer) return;

        // Game Start Timer
        if (!IsGameActive.Value && GameResult.Value == 0)
        {
            CountdownTimer.Value -= Time.deltaTime;
            if (CountdownTimer.Value <= 0f)
            {
                StartGame();
            }
        }

        // Meeting Timer
        if (IsMeetingActive.Value)
        {
            MeetingTimer.Value -= Time.deltaTime;
            if (MeetingTimer.Value <= 0f)
            {
                EndMeeting();
            }
        }
    }

    void StartGame()
    {
        IsGameActive.Value = true;

        var clients = NetworkManager.Singleton.ConnectedClientsIds;
        CrewmatesAlive.Value = clients.Count - 1;

        PickImpostor();
        TeleportPlayersToSpawn(); // Ensure everyone starts at unique spots
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
            EndGame(1); // Crewmates Win (Task completion)
        }
        else if (CrewmatesAlive.Value <= 0)
        {
            EndGame(2); // Impostor Wins (Kill all)
        }
    }

    // --- UPDATED: Made Public for EmergencyManager ---
    public void EndGame(int result)
    {
        if (!IsServer) return;

        IsGameActive.Value = false;
        GameResult.Value = result;
        EndGameClientRpc(result);
    }

    // --- Meeting Logic ---
    public void TriggerMeeting(ulong reporterId)
    {
        if (!IsServer) return;

        IsMeetingActive.Value = true;
        MeetingTimer.Value = 15.0f;

        // Remove bodies
        var bodies = FindObjectsByType<DeadBody>(FindObjectsSortMode.None);
        foreach (var body in bodies)
        {
            body.GetComponent<NetworkObject>().Despawn();
        }

        TeleportPlayersToSpawn();
        TriggerMeetingClientRpc(reporterId);
    }

    private void EndMeeting()
    {
        IsMeetingActive.Value = false;
    }

    // --- UPDATED: Teleport Logic to prevent overlapping ---
    private void TeleportPlayersToSpawn()
    {
        // 1. Sort points by name to ensure all clients/server agree on the order
        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None)
                              .OrderBy(p => p.gameObject.name).ToArray();

        if (points.Length == 0) return;

        // 2. Get all living players
        List<NetworkClient> livingClients = new List<NetworkClient>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null && !player.isDead.Value)
            {
                livingClients.Add(client);
            }
        }

        // 3. Assign 1-to-1
        for (int i = 0; i < livingClients.Count; i++)
        {
            // Wrap around if more players than points
            int pointIndex = i % points.Length;
            Vector3 targetPos = points[pointIndex].transform.position;

            // Server moves the player transform (NetworkTransform handles sync)
            livingClients[i].PlayerObject.transform.position = targetPos;

            // Also force physics reset via RPC
            TeleportClientRpc(livingClients[i].ClientId, targetPos);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TeleportClientRpc(ulong clientId, Vector3 pos)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            var player = NetworkManager.Singleton.LocalClient.PlayerObject;
            player.transform.position = pos; // Force position update on client side too

            // Kill momentum
            if (player.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerMeetingClientRpc(ulong reporterId)
    {
        Debug.Log($"Meeting called by {reporterId}!");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EndGameClientRpc(int result)
    {
        Debug.Log(result == 1 ? "Crewmates Win!" : "Impostor Wins!");
    }
}