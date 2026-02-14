using TMPro;
using UnityEngine;
using Unity.Netcode;

public class PlayerNameTag : NetworkBehaviour
{
    public TextMeshProUGUI nameText;
    public PlayerPlayerData playerData;

    public override void OnNetworkSpawn()
    {
        playerData.PlayerName.OnValueChanged += (prev, next) => UpdateName(next.ToString());

        UpdateName(playerData.PlayerName.Value.ToString());
    }

    private void UpdateName(string newName)
    {
        nameText.text = newName;
    }

    private void LateUpdate()
    {
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}