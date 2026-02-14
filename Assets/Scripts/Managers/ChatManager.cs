using Unity.Netcode;
using UnityEngine;
using Unity.Collections;

public class ChatManager : NetworkBehaviour
{
    public static ChatManager Instance;

    [Header("Settings")]
    public int maxMessageLength = 128;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    // 1. Client attempts to send a message
    public void SendChatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (message.Length > maxMessageLength) message = message.Substring(0, maxMessageLength);

        // Rule: No text chat during gameplay (Only Voting or Lobby)
        if (GameManager.Instance.CurrentState.Value == GameManager.GameState.Gameplay)
        {
            Debug.LogWarning("Text chat is disabled during Gameplay.");
            return;
        }

        SubmitMessageServerRpc(message);
    }

    [Rpc(SendTo.Server)]
    private void SubmitMessageServerRpc(string message, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        // Fetch Sender Data
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out NetworkClient client)) return;

        var playerMove = client.PlayerObject.GetComponent<PlayerMovement>();
        var playerData = client.PlayerObject.GetComponent<PlayerPlayerData>();

        bool isSenderDead = playerMove != null && playerMove.isDead.Value;
        string senderName = playerData != null ? playerData.PlayerName.Value.ToString() : $"Player {senderId}";
        Color senderColor = playerData != null ? playerData.PlayerColor.Value : Color.white;

        // Distribute to all clients with metadata
        ReceiveMessageClientRpc(message, senderId, senderName, senderColor, isSenderDead);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ReceiveMessageClientRpc(string message, ulong senderId, string name, Color color, bool isSenderDead)
    {
        // 1. Get Local Player State
        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null) return;

        var localPlayer = localClient.PlayerObject.GetComponent<PlayerMovement>();
        bool amIDead = localPlayer != null && localPlayer.isDead.Value;

        // 2. Dead Chat Filtering Rule
        // If Sender is Dead, ONLY Dead people can see it.
        // If Sender is Alive, EVERYONE can see it.
        if (isSenderDead && !amIDead)
        {
            return; // Living players cannot see dead chat
        }

        // 3. Send to UI
        if (ChatUI.Instance != null)
        {
            ChatUI.Instance.AddMessage(name, message, color, isSenderDead);
        }
    }
}