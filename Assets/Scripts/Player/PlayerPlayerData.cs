using Unity.Netcode;
using UnityEngine;
using Unity.Collections; // Required for FixedString

public class PlayerPlayerData : NetworkBehaviour
{
    // Sync Name and Color
    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>();
    public NetworkVariable<Color> PlayerColor = new NetworkVariable<Color>();

    [SerializeField] private Renderer playerRenderer;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 1. Assign Random Color based on ID
            Color[] colors = new Color[]
            {
                Color.red, Color.blue, Color.green, Color.yellow,
                Color.cyan, Color.magenta, new Color(1, 0.5f, 0), Color.white
            };
            PlayerColor.Value = colors[OwnerClientId % (ulong)colors.Length];

            // 2. Assign Name (You can replace this with a Lobby name later)
            PlayerName.Value = $"Player {OwnerClientId}";
        }

        // Apply Color immediately
        ApplyColor(PlayerColor.Value);

        // Listen for changes
        PlayerColor.OnValueChanged += (prev, next) => ApplyColor(next);
    }

    private void ApplyColor(Color c)
    {
        if (playerRenderer != null) playerRenderer.material.color = c;
    }
    [Rpc(SendTo.Server)]
    public void SetPlayerNameServerRpc(string newName)
    {
        PlayerName.Value = newName;
    }
}