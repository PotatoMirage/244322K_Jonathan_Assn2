using Unity.Netcode;
using UnityEngine;

public class DeadBody : Interactable
{
    public SkinnedMeshRenderer bodyRenderer;
    public NetworkVariable<int> BodyColorIndex = new NetworkVariable<int>(0);

    public override void OnNetworkSpawn()
    {
        BodyColorIndex.OnValueChanged += OnColorChanged;
        ApplyColor(BodyColorIndex.Value);
    }

    public void SetupBody(int colorIndex)
    {
        if (IsServer) BodyColorIndex.Value = colorIndex;
    }

    public override void OnInteract(ulong interactorId)
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(interactorId, out NetworkClient client))
        {
            var player = client.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null && !player.isDead.Value)
            {
                GetComponent<NetworkObject>().Despawn();
                GameManager.Instance.TriggerMeeting(interactorId);
            }
        }
    }

    private void OnColorChanged(int prev, int current)
    {
        ApplyColor(current);
    }

    private void ApplyColor(int index)
    {
        if (bodyRenderer == null) return;
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