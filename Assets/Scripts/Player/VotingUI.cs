using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VotingUI : MonoBehaviour
{
    public static VotingUI Instance;

    [Header("UI References")]
    public GameObject votingPanel;
    public Transform buttonContainer;
    public GameObject playerButtonPrefab;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI resultText;

    private Dictionary<ulong, Button> playerButtons = new();

    private void Awake() { Instance = this; }

    private void Start()
    {
        votingPanel.SetActive(false);
        resultText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (votingPanel.activeSelf && VotingManager.Instance.IsVotingOpen.Value)
        {
            timerText.text = $"Voting Ends: {Mathf.Ceil(VotingManager.Instance.VoteTimer.Value)}";
        }
    }

    public void Show()
    {
        votingPanel.SetActive(true);
        resultText.gameObject.SetActive(false);
        GenerateButtons();
    }

    private void GenerateButtons()
    {
        foreach (Transform child in buttonContainer) Destroy(child.gameObject);
        playerButtons.Clear();

        bool amIDead = false;
        if (NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            PlayerMovement myPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
            if (myPlayer != null && myPlayer.isDead.Value) amIDead = true;
        }

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            ulong clientId = client.ClientId;
            GameObject btnObj = Instantiate(playerButtonPrefab, buttonContainer);
            Button btn = btnObj.GetComponent<Button>();
            TextMeshProUGUI txt = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            Image btnImage = btnObj.GetComponent<Image>();

            PlayerPlayerData playerData = client.PlayerObject.GetComponent<PlayerPlayerData>();
            PlayerMovement playerMove = client.PlayerObject.GetComponent<PlayerMovement>();

            string pName = playerData != null ? playerData.PlayerName.Value.ToString() : $"Player {clientId}";
            Color pColor = playerData != null ? playerData.PlayerColor.Value : Color.white;

            txt.text = pName;
            btnImage.color = pColor;
            txt.color = (pColor == Color.black || pColor == Color.blue) ? Color.white : Color.black;

            if ((playerMove != null && playerMove.isDead.Value))
            {
                btn.interactable = false;
                txt.text += " (DEAD)";
                btnImage.color = Color.gray;
            }
            else if (amIDead)
            {
                btn.interactable = false;
            }
            else
            {
                btn.onClick.AddListener(() => OnVoteClicked(clientId));
            }

            playerButtons.Add(clientId, btn);
        }

        GameObject skipBtnObj = Instantiate(playerButtonPrefab, buttonContainer);
        Button skipBtn = skipBtnObj.GetComponent<Button>();
        TextMeshProUGUI skipTxt = skipBtnObj.GetComponentInChildren<TextMeshProUGUI>();
        Image skipBg = skipBtnObj.GetComponent<Image>();

        skipTxt.text = "SKIP VOTE";
        skipBg.color = Color.white;
        skipTxt.color = Color.black;

        if (amIDead)
        {
            skipBtn.interactable = false;
        }
        else
        {
            skipBtn.onClick.AddListener(OnSkipClicked);
        }

        playerButtons.Add(ulong.MaxValue, skipBtn);
    }

    public void OnVoteClicked(ulong targetId)
    {
        foreach (Button btn in playerButtons.Values) btn.interactable = false;

        VotingManager.Instance.CastVoteServerRpc(NetworkManager.Singleton.LocalClientId, targetId, false);
    }

    public void OnSkipClicked()
    {
        foreach (Button btn in playerButtons.Values) btn.interactable = false;
        VotingManager.Instance.CastVoteServerRpc(NetworkManager.Singleton.LocalClientId, ulong.MaxValue, true);
    }

    public void MarkVoted(ulong voterId)
    {
        if (playerButtons.ContainsKey(voterId))
        {
            TextMeshProUGUI txt = playerButtons[voterId].GetComponentInChildren<TextMeshProUGUI>();
            txt.text += " [VOTED]";
        }
    }

    public void DisplayResult(ulong ejectedId, bool isTie)
    {
        votingPanel.SetActive(false);
        resultText.gameObject.SetActive(true);

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
