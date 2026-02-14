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
        ReportBodyServerRpc(interactorId);
    }
    [Rpc(SendTo.Server)]
    private void ReportBodyServerRpc(ulong interactorId)
    {
        PlayerMovement player = NetworkManager.Singleton.ConnectedClients[interactorId].PlayerObject.GetComponent<PlayerMovement>();
        if (!player.isDead.Value)
        {
            GameManager.Instance.ReportBody(interactorId);
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