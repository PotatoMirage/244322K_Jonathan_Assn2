using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : NetworkBehaviour
{
    [Header("UI References")]
    public GameObject gamePanel;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI timerText;
    public GameObject timerTextPanel;
    public Slider taskSlider;
    public TextMeshProUGUI taskCountText;
    public TextMeshProUGUI interactionText;

    [Header("End Game Screens")]
    public GameObject crewmateWinScreen;
    public GameObject impostorWinScreen;

    [Header("Host Controls")]
    public Button restartButton;

    private void Start()
    {
        if (crewmateWinScreen) crewmateWinScreen.SetActive(false);
        if (impostorWinScreen) impostorWinScreen.SetActive(false);
        if (gamePanel) gamePanel.SetActive(true);
        if (timerTextPanel) timerTextPanel.SetActive(true);
        if (interactionText) interactionText.gameObject.SetActive(false);
        if (restartButton) restartButton.gameObject.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(() =>
            {
                if (IsServer) GameManager.Instance.RestartToLobby();
            });
        }
    }

    public void UpdateInteractionText(string message)
    {
        if (interactionText == null) return;
        interactionText.text = message;
        interactionText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // 1. Update Timer & Lobby State
        if (GameManager.Instance.CurrentState.Value == GameManager.GameState.Lobby)
        {
            float time = Mathf.Ceil(GameManager.Instance.StateTimer.Value);
            if (timerText) timerText.text = time > 0 ? $"Game Starting in: {time}" : "GO!";
            if (timerTextPanel) timerTextPanel.SetActive(true);
        }
        else
        {
            if (timerText) timerText.text = "";
            if (timerTextPanel) timerTextPanel.SetActive(false);

            // 2. Update Role (Only show once game starts)
            if (NetworkManager.Singleton.LocalClient != null &&
                NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                bool isImpostor = GameManager.Instance.ImpostorId.Value == NetworkManager.Singleton.LocalClientId;
                if (roleText)
                {
                    roleText.text = isImpostor ? "IMPOSTOR" : "CREWMATE";
                    roleText.color = isImpostor ? Color.red : Color.cyan;
                }
            }
        }

        // 3. Update Task Slider
        float current = GameManager.Instance.CompletedTasks.Value;
        float total = GameManager.Instance.TotalTasks.Value;
        if (taskSlider && total > 0) taskSlider.value = current / total;
        if (taskCountText) taskCountText.text = $"Tasks: {current}/{total}";

        // 4. Handle End Screens
        if (GameManager.Instance.CurrentState.Value == GameManager.GameState.Ended)
        {
            if (gamePanel) gamePanel.SetActive(false);
            int result = GameManager.Instance.GameResult.Value;
            if (crewmateWinScreen && result == 1) crewmateWinScreen.SetActive(true);
            if (impostorWinScreen && result == 2) impostorWinScreen.SetActive(true);

            // Show Restart Button only for Host
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(IsServer);
            }
        }
    }
}