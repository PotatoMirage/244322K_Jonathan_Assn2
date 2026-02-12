using Unity.Netcode;
using UnityEngine;

public class DeadBody : Interactable
{
    public SkinnedMeshRenderer bodyRenderer;
    public Material defaultMat;

    // Store the color index to sync visual appearance of the corpse
    public NetworkVariable<int> BodyColorIndex = new NetworkVariable<int>(0);

    public override void OnNetworkSpawn()
    {
        BodyColorIndex.OnValueChanged += OnColorChanged;
        ApplyColor(BodyColorIndex.Value);
    }

    public void SetupBody(int colorIndex)
    {
        if (IsServer)
        {
            BodyColorIndex.Value = colorIndex;
        }
    }

    public override void OnInteract(ulong interactorId)
    {
        // Only living players can report
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(interactorId, out NetworkClient client))
        {
            var player = client.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null && !player.isDead.Value)
            {
                ReportBodyServerRpc(interactorId);
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void ReportBodyServerRpc(ulong reporterId)
    {
        // Destroy this body
        GetComponent<NetworkObject>().Despawn();

        // Trigger the meeting in GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerMeeting(reporterId);
        }
    }

    private void OnColorChanged(int prev, int current)
    {
        ApplyColor(current);
    }

    private void ApplyColor(int index)
    {
        if (bodyRenderer == null) return;

        // This assumes you have a way to get the color, similar to CubeColour.cs
        // For simplicity, we re-use the hardcoded array logic or you can link it to a manager
        Color[] colors = new[] {
            new Color(1.0f, 0f, 0f),
            new Color(0f, 1.0f, 0f),
            new Color(0f, 0f, 1.0f),
            new Color(1.0f, 1.0f, 0f)
        };

        int safeIndex = index % colors.Length;
        bodyRenderer.material.color = colors[safeIndex];
    }
}