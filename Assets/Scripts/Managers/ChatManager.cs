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

    public void SendChatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (message.Length > maxMessageLength) message = message.Substring(0, maxMessageLength);

        if (GameManager.Instance.CurrentState.Value == GameManager.GameState.Gameplay)
        {
            return;
        }

        SubmitMessageServerRpc(message);
    }

    [Rpc(SendTo.Server)]
    private void SubmitMessageServerRpc(string message, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out NetworkClient client)) return;

        PlayerMovement playerMove = client.PlayerObject.GetComponent<PlayerMovement>();
        PlayerPlayerData playerData = client.PlayerObject.GetComponent<PlayerPlayerData>();

        bool isSenderDead = playerMove != null && playerMove.isDead.Value;
        string senderName = playerData != null ? playerData.PlayerName.Value.ToString() : $"Player {senderId}";
        Color senderColor = playerData != null ? playerData.PlayerColor.Value : Color.white;

        ReceiveMessageClientRpc(message, senderId, senderName, senderColor, isSenderDead);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ReceiveMessageClientRpc(string message, ulong senderId, string name, Color color, bool isSenderDead)
    {
        NetworkClient localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null) return;

        PlayerMovement localPlayer = localClient.PlayerObject.GetComponent<PlayerMovement>();
        bool amIDead = localPlayer != null && localPlayer.isDead.Value;

        if (isSenderDead && !amIDead)
        {
            return;
        }

        if (ChatUI.Instance != null)
        {
            ChatUI.Instance.AddMessage(name, message, color, isSenderDead);
        }
    }
}
