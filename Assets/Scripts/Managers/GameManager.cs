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
    public NetworkVariable<int> GameResult = new NetworkVariable<int>(0);

    [Header("Game Settings")]
    public float votingTime = 30f;
    public Transform[] spawnPoints;
    public int tasksPerPlayer = 1;

    public NetworkVariable<int> TotalTasks = new NetworkVariable<int>(0);
    public NetworkVariable<int> CompletedTasks = new NetworkVariable<int>(0);
    public NetworkVariable<int> CrewmatesAlive = new NetworkVariable<int>(0);

    private Dictionary<ulong, int> playerTaskCounts = new Dictionary<ulong, int>();

    private void Awake() { Instance = this; }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene == "Lobby" || currentScene == "Menu")
            {
                CurrentState.Value = GameState.Lobby;
            }
        }

        CurrentState.OnValueChanged += OnStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        CurrentState.OnValueChanged -= OnStateChanged;
    }

    private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;

        if (sceneName == "MainScene" || sceneName == "mainscene")
        {
            StartCoroutine(StartGameRoutine());
        }
    }

    private System.Collections.IEnumerator StartGameRoutine()
    {
        float countdownDuration = 5f;
        StateTimer.Value = countdownDuration;

        while (StateTimer.Value > 0)
        {
            yield return null;
            StateTimer.Value -= Time.deltaTime;
        }

        StateTimer.Value = 0;

        List<ulong> clients = NetworkManager.Singleton.ConnectedClientsIds.ToList();
        ImpostorId.Value = clients[Random.Range(0, clients.Count)];
        CrewmatesAlive.Value = clients.Count - 1;

        TotalTasks.Value = CrewmatesAlive.Value * tasksPerPlayer;
        CompletedTasks.Value = 0;

        TeleportAllToSpawn();

        CurrentState.Value = GameState.Gameplay;
    }

    private void OnStateChanged(GameState oldState, GameState newState)
    {

    }

    private void Update()
    {
        if (!IsServer) return;

        switch (CurrentState.Value)
        {
            case GameState.Lobby:
                break;

            case GameState.Gameplay:
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

        List<ulong> clients = NetworkManager.Singleton.ConnectedClientsIds.ToList();
        ImpostorId.Value = clients[Random.Range(0, clients.Count)];
        CrewmatesAlive.Value = clients.Count - 1;

        TotalTasks.Value = CrewmatesAlive.Value * tasksPerPlayer;
        CompletedTasks.Value = 0;

        TeleportAllToSpawn();

        CurrentState.Value = GameState.Gameplay;
    }

    public void RestartGameMatch()
    {
        if (!IsServer) return;

        CurrentState.Value = GameState.Lobby;
        ImpostorId.Value = ulong.MaxValue;
        CompletedTasks.Value = 0;
        GameResult.Value = 0;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                PlayerMovement player = client.PlayerObject.GetComponent<PlayerMovement>();
                if (player != null)
                {
                    player.isDead.Value = false;
                }
            }
        }

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

        CompletedTasks.Value++;
        CheckWinCondition();
    }

    public void OnPlayerDied(ulong clientId)
    {
        if (!IsServer) return;

        if (clientId == ImpostorId.Value)
        {
            EndGame(1);
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
            EndGame(2);
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
            return;
        }

        int i = 0;
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            PlayerMovement player = client.PlayerObject.GetComponent<PlayerMovement>();

            if (player != null)
            {
                Transform spawn = spawnPoints[i % spawnPoints.Length];
                player.TeleportClientRpc(spawn.position);
                i++;
            }
        }
    }

    private void CleanupBodies()
    {
        DeadBody[] bodies = FindObjectsByType<DeadBody>(FindObjectsSortMode.None);
        foreach (DeadBody body in bodies)
        {
            NetworkObject netObj = body.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn();
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerMeetingAlertClientRpc(ulong reporterId)
    {
    }
}