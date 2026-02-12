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
    public Slider taskSlider;
    public TextMeshProUGUI taskCountText;

    // --- NEW: Add this variable ---
    [Header("Interaction")]
    public TextMeshProUGUI interactionText;

    [Header("End Game Screens")]
    public GameObject crewmateWinScreen;
    public GameObject impostorWinScreen;

    private void Start()
    {
        if (crewmateWinScreen) crewmateWinScreen.SetActive(false);
        if (impostorWinScreen) impostorWinScreen.SetActive(false);
        if (gamePanel) gamePanel.SetActive(true);

        // --- NEW: Hide interaction text on start ---
        if (interactionText != null) interactionText.gameObject.SetActive(false);
    }

    public void UpdateInteractionText(string message)
    {
        if (interactionText == null)
        {
            Debug.LogError("⚠ Interaction Text is missing in Inspector on GameUIManager!");
            return;
        }

        if (string.IsNullOrEmpty(message))
        {
            interactionText.gameObject.SetActive(false);
        }
        else
        {
            interactionText.text = message;
            interactionText.gameObject.SetActive(true);
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // 1. Update Timer
        if (!GameManager.Instance.IsGameActive.Value)
        {
            float time = Mathf.Ceil(GameManager.Instance.CountdownTimer.Value);
            timerText.text = time > 0 ? $"Starting in: {time}" : "GO!";
        }
        else
        {
            timerText.text = "";

            // 2. Update Role
            if (NetworkManager.Singleton.LocalClient != null &&
                NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                ulong myId = NetworkManager.Singleton.LocalClientId;
                bool isImpostor = GameManager.Instance.ImpostorId.Value == myId;

                if (isImpostor)
                {
                    roleText.text = "IMPOSTOR";
                    roleText.color = Color.red;
                }
                else
                {
                    roleText.text = "CREWMATE";
                    roleText.color = Color.cyan;
                }
            }
        }

        // 3. Update Task Slider
        float current = GameManager.Instance.CompletedTasks.Value;
        float total = GameManager.Instance.TotalTasks.Value;

        if (taskSlider != null && total > 0)
        {
            taskSlider.value = current / total;
        }
        if (taskCountText != null)
        {
            taskCountText.text = $"Tasks: {current}/{total}";
        }

        // 4. Handle End Screens
        if (GameManager.Instance.GameResult.Value != 0)
        {
            gamePanel.SetActive(false);
            if (GameManager.Instance.GameResult.Value == 1) crewmateWinScreen.SetActive(true);
            if (GameManager.Instance.GameResult.Value == 2) impostorWinScreen.SetActive(true);
        }
    }
}