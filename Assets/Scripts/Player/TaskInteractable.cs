using Unity.Netcode;
using UnityEngine;

public class TaskInteractable : Interactable
{
    [Header("Task Configuration")]
    public string taskName = "Fix Wiring";
    public GameObject minigamePrefab;

    public NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(false);

    // This usually runs on the SERVER due to your Interaction system
    public override void OnInteract(ulong interactorId)
    {
        if (isCompleted.Value) return;

        OpenTaskUIClientRpc(RpcTarget.Single(interactorId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void OpenTaskUIClientRpc(RpcParams rpcParams = default)
    {
        // This now runs only on the specific client's machine
        TaskUIManager.Instance.OpenTaskUI(minigamePrefab, this);
    }

    public void CompleteTask()
    {
        SubmitCompletionServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void SubmitCompletionServerRpc(RpcParams rpcParams = default)
    {
        if (isCompleted.Value) return;

        // Anti-Cheat: Distance check
        if (!ValidateRange(rpcParams.Receive.SenderClientId)) return;

        isCompleted.Value = true;
        GameManager.Instance.CompleteTask(rpcParams.Receive.SenderClientId);
        UpdateVisualsClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateVisualsClientRpc()
    {
        GetComponent<Renderer>().material.color = Color.green;
    }
}