using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections;

public class ChatUI : MonoBehaviour
{
    public static ChatUI Instance;

    [Header("References")]
    public GameObject chatPanel;      
    public Transform contentContainer;
    public TMP_InputField inputField; 
    public GameObject messagePrefab;  

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
            GameManager.Instance.CurrentState.OnValueChanged += OnGameStateChanged;

            UpdateChatState(GameManager.Instance.CurrentState.Value);
        }
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
        if (chatPanel.activeSelf && Input.GetKeyDown(KeyCode.Return))
        {
            if (!inputField.isFocused)
            {
                inputField.ActivateInputField();
            }
        }
    }
    private void OnGameStateChanged(GameManager.GameState oldState, GameManager.GameState newState)
    {
        UpdateChatState(newState);
    }

    private void UpdateChatState(GameManager.GameState state)
    {
        bool showChat = state == GameManager.GameState.Meeting ||
                        state == GameManager.GameState.Voting ||
                        state == GameManager.GameState.Ended;

        chatPanel.SetActive(showChat);

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