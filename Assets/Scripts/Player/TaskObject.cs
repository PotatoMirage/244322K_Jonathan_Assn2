using Unity.Netcode;
using UnityEngine;

public class TaskObject : Interactable
{
    public NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(false);
    public MeshRenderer taskRenderer;
    public Material completeMat;
    public Material incompleteMat;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            GameManager.Instance.RegisterTask();
        }
        isCompleted.OnValueChanged += OnStateChanged;
        UpdateVisuals(isCompleted.Value);
    }

    public override void OnInteract(ulong interactorId)
    {
        if (!IsServer || isCompleted.Value) return;

        if (GameManager.Instance.ImpostorId.Value != interactorId)
        {
            isCompleted.Value = true;
            GameManager.Instance.CompleteTask();
        }
    }

    private void OnStateChanged(bool prev, bool current)
    {
        UpdateVisuals(current);
    }

    private void UpdateVisuals(bool complete)
    {
        if (taskRenderer != null)
        {
            taskRenderer.material = complete ? completeMat : incompleteMat;
        }
    }
}