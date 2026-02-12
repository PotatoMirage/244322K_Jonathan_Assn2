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

    [Header("Task Settings")]
    public int tasksPerPlayer = 3;

    public NetworkVariable<int> TotalTasks = new NetworkVariable<int>(0);
    public NetworkVariable<int> CompletedTasks = new NetworkVariable<int>(0);
    public NetworkVariable<int> CrewmatesAlive = new NetworkVariable<int>(0);
    public NetworkVariable<int> GameResult = new NetworkVariable<int>(0);

    private List<TaskObject> allTasks = new List<TaskObject>();
    private Dictionary<ulong, int> playerTaskProgress = new Dictionary<ulong, int>();

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
            CurrentState.Value = GameState.Lobby;
            StateTimer.Value = 5.0f;
            GameResult.Value = 0;
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        switch (CurrentState.Value)
        {
            case GameState.Lobby:
                if (GameResult.Value == 0)
                {
                    StateTimer.Value -= Time.deltaTime;
                    if (StateTimer.Value <= 0f) StartGame();
                }
                break;

            case GameState.Meeting:
                StateTimer.Value -= Time.deltaTime;
                if (StateTimer.Value <= 0f) StartVoting();
                break;
        }
    }

    private void StartGame()
    {
        CurrentState.Value = GameState.Gameplay;
        var clients = NetworkManager.Singleton.ConnectedClientsIds;
        CrewmatesAlive.Value = clients.Count - 1;

        List<ulong> clientIds = new List<ulong>(clients);
        ImpostorId.Value = clientIds[Random.Range(0, clientIds.Count)];

        SetupTasks(clientIds);
        TeleportPlayersToSpawn();
    }

    private void SetupTasks(List<ulong> playerIds)
    {
        playerTaskProgress.Clear();
        foreach (var id in playerIds)
        {
            if (id != ImpostorId.Value)
            {
                playerTaskProgress[id] = 0;
            }
        }

        foreach (var task in allTasks)
        {
            task.ResetTask();
        }

        int actualTasksPerPlayer = Mathf.Min(tasksPerPlayer, allTasks.Count);
        TotalTasks.Value = actualTasksPerPlayer * CrewmatesAlive.Value;
        CompletedTasks.Value = 0;
    }

    public void RegisterTask(TaskObject task)
    {
        if (IsServer && !allTasks.Contains(task))
        {
            allTasks.Add(task);
        }
    }

    public void CompleteTask(ulong playerId)
    {
        if (!IsServer || CurrentState.Value != GameState.Gameplay) return;

        if (playerTaskProgress.ContainsKey(playerId))
        {
            int current = playerTaskProgress[playerId];
            int max = Mathf.Min(tasksPerPlayer, allTasks.Count);

            if (current < max)
            {
                playerTaskProgress[playerId]++;
                CompletedTasks.Value++;
                CheckWinCondition();
            }
        }
    }

    public void OnPlayerDied(ulong clientId)
    {
        if (!IsServer || CurrentState.Value != GameState.Gameplay) return;

        if (clientId != ImpostorId.Value)
        {
            CrewmatesAlive.Value--;
        }
        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        if (CrewmatesAlive.Value <= 0)
        {
            EndGame(2);
            return;
        }

        bool allFinished = true;
        int max = Mathf.Min(tasksPerPlayer, allTasks.Count);

        foreach (var kvp in playerTaskProgress)
        {
            if (kvp.Value < max)
            {
                allFinished = false;
                break;
            }
        }

        if (allFinished && TotalTasks.Value > 0) EndGame(1);
    }

    public void EndGame(int result)
    {
        if (!IsServer) return;
        CurrentState.Value = GameState.Ended;
        GameResult.Value = result;
    }

    public void RestartToLobby()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.SceneManager.LoadScene("Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void TriggerMeeting(ulong reporterId)
    {
        if (!IsServer) return;

        CurrentState.Value = GameState.Meeting;
        StateTimer.Value = 5.0f;

        foreach (var body in FindObjectsByType<DeadBody>(FindObjectsSortMode.None))
        {
            body.GetComponent<NetworkObject>().Despawn();
        }

        TeleportPlayersToSpawn();
    }

    private void StartVoting()
    {
        CurrentState.Value = GameState.Voting;
        VotingManager.Instance.StartVotingSession();
    }

    private void TeleportPlayersToSpawn()
    {
        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None).OrderBy(p => p.gameObject.name).ToArray();
        if (points.Length == 0) return;

        int i = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null && !player.isDead.Value)
            {
                player.TeleportTo(points[i % points.Length].transform.position);
                i++;
            }
        }
    }
}