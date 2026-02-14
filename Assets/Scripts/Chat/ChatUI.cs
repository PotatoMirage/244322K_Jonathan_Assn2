using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections;

public class ChatUI : MonoBehaviour
{
    public static ChatUI Instance;

    [Header("References")]
    public GameObject chatPanel;       // The entire chat window (Panel)
    public Transform contentContainer; // The ScrollView Content
    public TMP_InputField inputField;  // The typing box
    public GameObject messagePrefab;   // Prefab containing a TextMeshProUGUI component

    [Header("Settings")]
    public Color deadChatColor = Color.gray;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            // Subscribe to state changes
            GameManager.Instance.CurrentState.OnValueChanged += OnGameStateChanged;

            // Run logic immediately for the current state
            UpdateChatState(GameManager.Instance.CurrentState.Value);
        }

        // Input Field Listeners
        inputField.onSelect.AddListener(delegate { SetInputMode(true); });
        inputField.onDeselect.AddListener(delegate { SetInputMode(false); });
        inputField.onSubmit.AddListener(OnSubmit);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentState.OnValueChanged -= OnGameStateChanged;
        }
    }

    private void Update()
    {
        // Optional: Press Enter to focus chat IF the panel is actually active
        if (chatPanel.activeSelf && Input.GetKeyDown(KeyCode.Return))
        {
            if (!inputField.isFocused)
            {
                inputField.ActivateInputField();
            }
        }
    }

    // Listener for Netcode variable change
    private void OnGameStateChanged(GameManager.GameState oldState, GameManager.GameState newState)
    {
        UpdateChatState(newState);
    }

    // CORE LOGIC YOU REQUESTED
    private void UpdateChatState(GameManager.GameState state)
    {
        // "Make it so if state == Meeting then set gameobject active true"
        // We also include Voting, Lobby, and Ended so players can talk outside of gameplay.
        bool showChat = state == GameManager.GameState.Meeting ||
                        state == GameManager.GameState.Voting ||
                        state == GameManager.GameState.Ended;

        // Toggle the entire panel
        chatPanel.SetActive(showChat);

        // If we just hid the chat, force movement back on immediately
        if (!showChat)
        {
            SetInputMode(false);
        }
    }

    public void OnSubmit(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            ChatManager.Instance.SendChatMessage(text);
        }

        inputField.text = "";

        // Remove focus after sending to let player vote/move immediately
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        SetInputMode(false);
    }

    public void AddMessage(string playerName, string message, Color nameColor, bool isDead)
    {
        GameObject newMsg = Instantiate(messagePrefab, contentContainer);
        TextMeshProUGUI tmp = newMsg.GetComponent<TextMeshProUGUI>();

        string prefix = isDead ? "[DEAD] " : "";
        string hexColor = ColorUtility.ToHtmlStringRGB(isDead ? deadChatColor : nameColor);

        tmp.text = $"<color=#{hexColor}>{prefix}<b>{playerName}</b></color>: {message}";
    }

    // Disable PlayerMovement when typing
    private void SetInputMode(bool isTyping)
    {
        if (NetworkManager.Singleton.LocalClient == null ||
            NetworkManager.Singleton.LocalClient.PlayerObject == null) return;

        var playerMove = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();

        if (playerMove != null)
        {
            playerMove.enabled = !isTyping;
        }
    }
}