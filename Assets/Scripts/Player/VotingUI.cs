using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VotingUI : MonoBehaviour
{
    public static VotingUI Instance;

    public GameObject panel;
    public Transform buttonContainer;
    public GameObject voteButtonPrefab;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI resultText;

    private Dictionary<ulong, GameObject> playerButtons = new Dictionary<ulong, GameObject>();

    private void Awake() => Instance = this;

    public void Show()
    {
        panel.SetActive(true);
        resultText.text = "";
        foreach (Transform child in buttonContainer) Destroy(child.gameObject);
        playerButtons.Clear();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null && !player.isDead.Value)
            {
                GameObject btn = Instantiate(voteButtonPrefab, buttonContainer);
                btn.GetComponentInChildren<TextMeshProUGUI>().text = $"Player {client.ClientId}";
                btn.GetComponent<Button>().onClick.AddListener(() => CastVote(client.ClientId));
                playerButtons.Add(client.ClientId, btn);
            }
        }

        GameObject skipBtn = Instantiate(voteButtonPrefab, buttonContainer);
        skipBtn.GetComponentInChildren<TextMeshProUGUI>().text = "SKIP VOTE";
        skipBtn.GetComponent<Button>().onClick.AddListener(() => CastSkip());
    }

    private void Update()
    {
        if (panel.activeSelf && VotingManager.Instance != null)
        {
            timerText.text = Mathf.Ceil(VotingManager.Instance.VoteTimer.Value).ToString();
        }
    }

    public void CastVote(ulong targetId)
    {
        VotingManager.Instance.CastVoteServerRpc(NetworkManager.Singleton.LocalClientId, targetId, false);
        DisableButtons();
    }

    public void CastSkip()
    {
        VotingManager.Instance.CastVoteServerRpc(NetworkManager.Singleton.LocalClientId, 0, true);
        DisableButtons();
    }

    private void DisableButtons()
    {
        foreach (var btn in buttonContainer.GetComponentsInChildren<Button>()) btn.interactable = false;
    }

    public void MarkVoted(ulong voterId)
    {
        if (playerButtons.ContainsKey(voterId))
        {
            playerButtons[voterId].GetComponent<Image>().color = Color.green;
        }
    }

    public void DisplayResult(ulong ejectedId, bool tie)
    {
        if (tie) resultText.text = "No one was ejected (Tie/Skip).";
        else resultText.text = $"Player {ejectedId} was ejected.";

        Invoke(nameof(Hide), 3f);
    }

    private void Hide() => panel.SetActive(false);
}