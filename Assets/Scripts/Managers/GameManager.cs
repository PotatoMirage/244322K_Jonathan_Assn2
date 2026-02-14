using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Lobby, Gameplay, Meeting, Voting, Ended }
    public NetworkVariable<GameState> CurrentState = new NetworkVariable<GameState>(GameState.Lobby);

    public NetworkVariable<float> StateTimer = new NetworkVariable<float>(0f);
    public NetworkVariable<ulong> ImpostorId = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<int> GameResult = new NetworkVariable<int>(0); // 1: Crew, 2: Impostor

    [Header("Game Settings")]
    public float votingTime = 30f;
    public Transform[] spawnPoints;
    public int tasksPerPlayer = 1;

    // Trackers
    public NetworkVariable<int> TotalTasks = new NetworkVariable<int>(0);
    public NetworkVariable<int> CompletedTasks = new NetworkVariable<int>(0);
    public NetworkVariable<int> CrewmatesAlive = new NetworkVariable<int>(0);

    private Dictionary<ulong, int> playerTaskCounts = new Dictionary<ulong, int>();

    private void Awake() { Instance = this; }

    // Inside GameManager.cs

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 1. Listen for Scene Changes
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;

            // 2. Handle initial state
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene == "Lobby" || currentScene == "Menu")
            {
                CurrentState.Value = GameState.Lobby;
            }
        }

        // Subscribe to state changes for everyone
        CurrentState.OnValueChanged += OnStateChanged;
    }

    // Good practice: Clean up event subscription
    public override void OnNetworkDespawn()
    {
        CurrentState.OnValueChanged -= OnStateChanged;
    }
    private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;

        if (sceneName == "MainScene" || sceneName == "mainscene")
        {
            // FIX: Don't run immediately. Wait for the engine to settle.
            StartCoroutine(StartGameRoutine());
        }
    }
    // Replace your existing StartGameRoutine with this:
    private System.Collections.IEnumerator StartGameRoutine()
    {
        // 1. Set the initial countdown time
        float countdownDuration = 5f;
        StateTimer.Value = countdownDuration;

        // 2. Count down manually
        while (StateTimer.Value > 0)
        {
            yield return null; // Wait for the next frame
            StateTimer.Value -= Time.deltaTime;
        }

        // 3. Ensure it hits exactly 0 before continuing
        StateTimer.Value = 0;

        // --- The rest of your existing logic stays the same ---

        // Setup Roles
        var clients = NetworkManager.Singleton.ConnectedClientsIds.ToList();
        ImpostorId.Value = clients[Random.Range(0, clients.Count)];
        CrewmatesAlive.Value = clients.Count - 1;

        TotalTasks.Value = CrewmatesAlive.Value * tasksPerPlayer;
        CompletedTasks.Value = 0;

        // Teleport
        TeleportAllToSpawn();

        // Open Gameplay
        CurrentState.Value = GameState.Gameplay;
    }
    private void OnStateChanged(GameState oldState, GameState newState)
    {
        Debug.Log($"Game State Changed: {newState}");
        // You can trigger UI updates here automatically!
    }

    private void Update()
    {
        if (!IsServer) return;

        switch (CurrentState.Value)
        {
            case GameState.Lobby:
                // Logic handled manually or via LobbyManager
                break;

            case GameState.Gameplay:
                // Win conditions handled by events
                break;

            case GameState.Meeting:
                StateTimer.Value -= Time.deltaTime;
                if (StateTimer.Value <= 0)
                {
                    StartVoting();
                }
                break;

            case GameState.Voting:
                StateTimer.Value -= Time.deltaTime;
                if (StateTimer.Value <= 0)
                {
                    VotingManager.Instance.ConcludeVoting();
                }
                break;
        }
    }

    public void StartGame()
    {
        if (!IsServer) return;

        // 1. Setup Roles
        var clients = NetworkManager.Singleton.ConnectedClientsIds.ToList();
        ImpostorId.Value = clients[Random.Range(0, clients.Count)];
        CrewmatesAlive.Value = clients.Count - 1;

        // 2. Setup Tasks
        TotalTasks.Value = CrewmatesAlive.Value * tasksPerPlayer;
        CompletedTasks.Value = 0;

        // 3. Teleport Players (Now they are guaranteed to be in the scene)
        TeleportAllToSpawn();

        // 4. Set State
        CurrentState.Value = GameState.Gameplay;
    }

    public void RestartGameMatch()
    {
        if (!IsServer) return;

        // 1. Reset Game State
        CurrentState.Value = GameState.Lobby;
        ImpostorId.Value = ulong.MaxValue;
        CompletedTasks.Value = 0;
        GameResult.Value = 0;

        // 2. Revive all players so they appear alive in the Lobby
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                var player = client.PlayerObject.GetComponent<PlayerMovement>();
                if (player != null)
                {
                    player.isDead.Value = false;
                }
            }
        }

        // 3. Load the Lobby Scene (Must match the Scene Name in Build Settings)
        NetworkManager.Singleton.SceneManager.LoadScene("Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void ReportBody(ulong reporterId)
    {
        if (!IsServer || CurrentState.Value != GameState.Gameplay) return;

        CurrentState.Value = GameState.Meeting;
        StateTimer.Value = 0.0f;

        TeleportAllToSpawn();
        CleanupBodies();
        TriggerMeetingAlertClientRpc(reporterId);
    }

    private void StartVoting()
    {
        CurrentState.Value = GameState.Voting;
        StateTimer.Value = votingTime;
        VotingManager.Instance.StartVotingSession();
    }

    public void CompleteTask(ulong playerId)
    {
        if (!IsServer) return;
        if (playerId == ImpostorId.Value) return;

        if (!playerTaskCounts.ContainsKey(playerId)) playerTaskCounts[playerId] = 0;

        // Simple global count check
        CompletedTasks.Value++;
        CheckWinCondition();
    }

    public void OnPlayerDied(ulong clientId)
    {
        if (!IsServer) return;

        if (clientId == ImpostorId.Value)
        {
            EndGame(1); // Crew Win
            return;
        }

        CrewmatesAlive.Value--;
        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        if (CompletedTasks.Value >= TotalTasks.Value && TotalTasks.Value > 0)
        {
            EndGame(1);
            return;
        }
        if (CrewmatesAlive.Value <= 0)
        {
            EndGame(2); // Impostor Win
        }
    }

    public void EndGame(int winner)
    {
        if (!IsServer) return;
        GameResult.Value = winner;
        CurrentState.Value = GameState.Ended;
    }

    private void TeleportAllToSpawn()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("No Spawn Points assigned in GameManager!");
            return;
        }

        int i = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject.GetComponent<PlayerMovement>();

            if (player != null)
            {
                // Pick spawn point
                Transform spawn = spawnPoints[i % spawnPoints.Length];

                Debug.Log($"Teleporting Player {client.ClientId} to {spawn.position}");

                // Send command
                player.TeleportClientRpc(spawn.position);

                i++;
            }
            else
            {
                Debug.LogError($"Player Object for Client {client.ClientId} is NULL! They will not spawn.");
            }
        }
    }

    private void CleanupBodies()
    {
        var bodies = FindObjectsByType<DeadBody>(FindObjectsSortMode.None);
        foreach (var body in bodies)
        {
            var netObj = body.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn();
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerMeetingAlertClientRpc(ulong reporterId)
    {
        // Visual cue logic
    }
}