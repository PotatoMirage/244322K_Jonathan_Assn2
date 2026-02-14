using TMPro;
using UnityEngine;
using Unity.Netcode;

public class PlayerNameTag : NetworkBehaviour
{
    public TextMeshProUGUI nameText;
    public PlayerPlayerData playerData;

    public override void OnNetworkSpawn()
    {
        // 1. Listen for name changes
        playerData.PlayerName.OnValueChanged += (prev, next) => UpdateName(next.ToString());

        // 2. Initial Set
        UpdateName(playerData.PlayerName.Value.ToString());
    }

    private void UpdateName(string newName)
    {
        nameText.text = newName;
    }

    // FIX: Use LateUpdate to override the parent's rotation
    private void LateUpdate()
    {
        if (Camera.main != null)
        {
            // Rotate the text to look AT the camera
            // We use transform.rotation = Camera.rotation to make it flat against the screen
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}