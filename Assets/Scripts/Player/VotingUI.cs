using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VotingUI : MonoBehaviour
{
    public static VotingUI Instance;

    [Header("UI References")]
    public GameObject votingPanel;      // The whole screen
    public Transform buttonContainer;   // Where we spawn player buttons
    public GameObject playerButtonPrefab; // Prefab with Text + Button
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI resultText;  // "Red was Ejected"

    private Dictionary<ulong, Button> playerButtons = new Dictionary<ulong, Button>();

    private void Awake() { Instance = this; }

    private void Start()
    {
        votingPanel.SetActive(false);
        resultText.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Update the timer visual from the Manager
        if (votingPanel.activeSelf && VotingManager.Instance.IsVotingOpen.Value)
        {
            timerText.text = $"Voting Ends: {Mathf.Ceil(VotingManager.Instance.VoteTimer.Value)}";
        }
    }

    // Called by VotingManager via RPC
    public void Show()
    {
        votingPanel.SetActive(true);
        resultText.gameObject.SetActive(false);
        GenerateButtons();
    }

    private void GenerateButtons()
    {
        // Clear old buttons
        foreach (Transform child in buttonContainer) Destroy(child.gameObject);
        playerButtons.Clear();

        // 1. Check if *I* (Local Player) am dead
        bool amIDead = false;
        if (NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            var myPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
            if (myPlayer != null && myPlayer.isDead.Value) amIDead = true;
        }

        // 2. Generate Player Buttons
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            ulong clientId = client.ClientId;
            GameObject btnObj = Instantiate(playerButtonPrefab, buttonContainer);
            Button btn = btnObj.GetComponent<Button>();
            TextMeshProUGUI txt = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            Image btnImage = btnObj.GetComponent<Image>();

            var playerData = client.PlayerObject.GetComponent<PlayerPlayerData>();
            var playerMove = client.PlayerObject.GetComponent<PlayerMovement>();

            string pName = playerData != null ? playerData.PlayerName.Value.ToString() : $"Player {clientId}";
            Color pColor = playerData != null ? playerData.PlayerColor.Value : Color.white;

            txt.text = pName;
            btnImage.color = pColor;
            txt.color = (pColor == Color.black || pColor == Color.blue) ? Color.white : Color.black;

            // If the TARGET is dead, we can't vote for them (gray out)
            // If *I* am dead, I can't vote for anyone (disable interactable)
            if ((playerMove != null && playerMove.isDead.Value))
            {
                btn.interactable = false;
                txt.text += " (DEAD)";
                btnImage.color = Color.gray;
            }
            else if (amIDead)
            {
                btn.interactable = false; // I am dead, so I can't click
            }
            else
            {
                btn.onClick.AddListener(() => OnVoteClicked(clientId));
            }

            playerButtons.Add(clientId, btn);
        }

        // 3. --- FIX: Add Skip Button ---
        GameObject skipBtnObj = Instantiate(playerButtonPrefab, buttonContainer);
        Button skipBtn = skipBtnObj.GetComponent<Button>();
        TextMeshProUGUI skipTxt = skipBtnObj.GetComponentInChildren<TextMeshProUGUI>();
        Image skipBg = skipBtnObj.GetComponent<Image>();

        skipTxt.text = "SKIP VOTE";
        skipBg.color = Color.white; // Or any distinct color
        skipTxt.color = Color.black;

        if (amIDead)
        {
            skipBtn.interactable = false;
        }
        else
        {
            skipBtn.onClick.AddListener(OnSkipClicked);
        }

        // Add to dictionary with a special key so we can disable it later
        playerButtons.Add(ulong.MaxValue, skipBtn);
    }

    public void OnVoteClicked(ulong targetId)
    {
        // Disable all buttons so we can't vote twice
        foreach (var btn in playerButtons.Values) btn.interactable = false;

        // Send vote to server
        VotingManager.Instance.CastVoteServerRpc(NetworkManager.Singleton.LocalClientId, targetId, false);
    }

    public void OnSkipClicked()
    {
        foreach (var btn in playerButtons.Values) btn.interactable = false;
        VotingManager.Instance.CastVoteServerRpc(NetworkManager.Singleton.LocalClientId, ulong.MaxValue, true);
    }

    // Called by VotingManager when someone votes
    public void MarkVoted(ulong voterId)
    {
        if (playerButtons.ContainsKey(voterId))
        {
            // Visual feedback: Add a little "Voted" icon or change text
            var txt = playerButtons[voterId].GetComponentInChildren<TextMeshProUGUI>();
            txt.text += " [VOTED]";
        }
    }

    // Called by VotingManager when done
    public void DisplayResult(ulong ejectedId, bool isTie)
    {
        votingPanel.SetActive(false); // Hide buttons
        resultText.gameObject.SetActive(true); // Show big text

        if (isTie)
        {
            resultText.text = "No one was ejected. (Tie)";
        }
        else if (ejectedId == ulong.MaxValue)
        {
            resultText.text = "No one was ejected. (Skipped)";
        }
        else
        {
            resultText.text = $"Player {ejectedId} was ejected.";
        }

        Invoke(nameof(HideResult), 4f);
    }

    private void HideResult()
    {
        resultText.gameObject.SetActive(false);
    }
}