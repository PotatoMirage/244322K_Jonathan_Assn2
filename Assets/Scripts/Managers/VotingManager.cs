using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class VotingManager : NetworkBehaviour
{
    public static VotingManager Instance { get; private set; }

    public NetworkVariable<float> VoteTimer = new NetworkVariable<float>(30f);
    public NetworkVariable<bool> IsVotingOpen = new NetworkVariable<bool>(false);

    private Dictionary<ulong, ulong> votes = new Dictionary<ulong, ulong>();
    private List<ulong> skipVotes = new List<ulong>();

    private void Awake()
    {
        Instance = this;
    }

    public void StartVotingSession()
    {
        if (!IsServer) return;

        VoteTimer.Value = 30f;
        IsVotingOpen.Value = true;
        votes.Clear();
        skipVotes.Clear();

        TriggerVotingUIClientRpc();
    }

    private void Update()
    {
        if (!IsServer || !IsVotingOpen.Value) return;

        VoteTimer.Value -= Time.deltaTime;
        if (VoteTimer.Value <= 0) ConcludeVoting();
    }

    [Rpc(SendTo.Server)]
    public void CastVoteServerRpc(ulong voterId, ulong targetId, bool isSkip)
    {
        if (!IsVotingOpen.Value) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(voterId, out NetworkClient voterClient))
        {
            var voter = voterClient.PlayerObject.GetComponent<PlayerMovement>();
            if (voter == null || voter.isDead.Value) return;
        }
        else
        {
            return;
        }

        if (!isSkip)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(targetId, out NetworkClient targetClient))
            {
                var target = targetClient.PlayerObject.GetComponent<PlayerMovement>();
                if (target == null || target.isDead.Value) return;
            }
            else
            {
                return;
            }
        }

        if (votes.ContainsKey(voterId) || skipVotes.Contains(voterId)) return;

        if (isSkip) skipVotes.Add(voterId);
        else votes[voterId] = targetId;

        UpdateVoteStatusClientRpc(voterId);

        int livingPlayers = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null && !player.isDead.Value)
            {
                livingPlayers++;
            }
        }

        if ((votes.Count + skipVotes.Count) >= livingPlayers)
        {
            VoteTimer.Value = 3f;
        }
    }

    private void ConcludeVoting()
    {
        IsVotingOpen.Value = false;

        Dictionary<ulong, int> tallies = new Dictionary<ulong, int>();
        foreach (var vote in votes.Values)
        {
            if (!tallies.ContainsKey(vote)) tallies[vote] = 0;
            tallies[vote]++;
        }

        ulong ejectedId = ulong.MaxValue;
        int maxVotes = 0;
        bool tie = false;

        foreach (var kvp in tallies)
        {
            if (kvp.Value > maxVotes)
            {
                maxVotes = kvp.Value;
                ejectedId = kvp.Key;
                tie = false;
            }
            else if (kvp.Value == maxVotes)
            {
                tie = true;
            }
        }

        if (skipVotes.Count >= maxVotes) tie = true;

        if (!tie && ejectedId != ulong.MaxValue)
        {
            EjectPlayer(ejectedId);
        }

        CloseVotingUIClientRpc(ejectedId, tie);
        Invoke(nameof(ReturnToGame), 4f);
    }

    private void EjectPlayer(ulong id)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(id, out NetworkClient client))
        {
            var player = client.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null) player.isDead.Value = true;
            GameManager.Instance.OnPlayerDied(id);
        }
    }

    private void ReturnToGame()
    {
        // FIX: If the game ended because the Impostor was ejected, do not switch back to Gameplay.
        if (GameManager.Instance.CurrentState.Value == GameManager.GameState.Ended) return;

        GameManager.Instance.CurrentState.Value = GameManager.GameState.Gameplay;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerVotingUIClientRpc() { VotingUI.Instance.Show(); }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateVoteStatusClientRpc(ulong voterId) { VotingUI.Instance.MarkVoted(voterId); }

    [Rpc(SendTo.ClientsAndHost)]
    private void CloseVotingUIClientRpc(ulong ejectedId, bool tie) { VotingUI.Instance.DisplayResult(ejectedId, tie); }
}