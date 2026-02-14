using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class VotingManager : NetworkBehaviour
{
    public static VotingManager Instance { get; private set; }

    public NetworkVariable<bool> IsVotingOpen = new NetworkVariable<bool>(false);

    public NetworkVariable<float> VoteTimer = new NetworkVariable<float>(30f);

    private Dictionary<ulong, ulong> votes = new Dictionary<ulong, ulong>();
    private HashSet<ulong> skipVotes = new HashSet<ulong>();

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (IsServer && IsVotingOpen.Value)
        {
            VoteTimer.Value -= Time.deltaTime;
        }
    }

    public void StartVotingSession()
    {
        if (!IsServer) return;
        ResetVotes();
        IsVotingOpen.Value = true;
        VoteTimer.Value = GameManager.Instance.votingTime;

        TriggerVotingUIClientRpc();
    }

    public void ResetVotes()
    {
        votes.Clear();
        skipVotes.Clear();
    }

    public void ConcludeVoting()
    {
        if (!IsServer) return;

        IsVotingOpen.Value = false;

        Dictionary<ulong, int> voteCounts = new Dictionary<ulong, int>();
        foreach (ulong target in votes.Values)
        {
            if (!voteCounts.ContainsKey(target)) voteCounts[target] = 0;
            voteCounts[target]++;
        }

        int skipCount = skipVotes.Count;
        ulong ejectedId = ulong.MaxValue;
        int maxVotes = 0;
        bool isTie = false;

        foreach (KeyValuePair<ulong, int> kvp in voteCounts)
        {
            if (kvp.Value > maxVotes)
            {
                maxVotes = kvp.Value;
                ejectedId = kvp.Key;
                isTie = false;
            }
            else if (kvp.Value == maxVotes) isTie = true;
        }

        if (skipCount >= maxVotes)
        {
            isTie = true;
            ejectedId = ulong.MaxValue;
        }

        if (!isTie && ejectedId != ulong.MaxValue)
        {
            EjectPlayer(ejectedId);
        }

        CloseVotingUIClientRpc(ejectedId, isTie);
        Invoke(nameof(EndMeetingDelay), 5f);
    }

    private void EndMeetingDelay()
    {
        if (GameManager.Instance.CurrentState.Value != GameManager.GameState.Ended)
        {
            GameManager.Instance.CurrentState.Value = GameManager.GameState.Gameplay;
        }
    }

    [Rpc(SendTo.Server)]
    public void CastVoteServerRpc(ulong voterId, ulong targetId, bool isSkip)
    {
        if (!IsVotingOpen.Value) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(voterId, out NetworkClient client))
        {
            PlayerMovement player = client.PlayerObject.GetComponent<PlayerMovement>();
            if (player == null || player.isDead.Value) return;
        }

        if (votes.ContainsKey(voterId) || skipVotes.Contains(voterId)) return;

        if (isSkip) skipVotes.Add(voterId);
        else votes[voterId] = targetId;

        UpdateVoteStatusClientRpc(voterId);

        int actualLiving = 0;
        foreach (NetworkClient c in NetworkManager.Singleton.ConnectedClientsList)
        {
            PlayerMovement p = c.PlayerObject.GetComponent<PlayerMovement>();
            if (p != null && !p.isDead.Value) actualLiving++;
        }

        if ((votes.Count + skipVotes.Count) >= actualLiving)
        {
            VoteTimer.Value = 3.0f;
            GameManager.Instance.StateTimer.Value = 3.0f;
        }
    }

    private void EjectPlayer(ulong id)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(id, out NetworkClient client))
        {
            PlayerMovement player = client.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.isDead.Value = true;
                GameManager.Instance.OnPlayerDied(id);
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerVotingUIClientRpc() { if (VotingUI.Instance != null) VotingUI.Instance.Show(); }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateVoteStatusClientRpc(ulong voterId) { if (VotingUI.Instance != null) VotingUI.Instance.MarkVoted(voterId); }

    [Rpc(SendTo.ClientsAndHost)]
    private void CloseVotingUIClientRpc(ulong ejectedId, bool tie) { if (VotingUI.Instance != null) VotingUI.Instance.DisplayResult(ejectedId, tie); }
}