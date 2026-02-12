using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class TaskObject : Interactable
{
    public MeshRenderer taskRenderer;
    public Material completeMat;
    public Material incompleteMat;

    private HashSet<ulong> completedPlayers = new HashSet<ulong>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            GameManager.Instance.RegisterTask(this);
        }
        // Default visual state
        if (taskRenderer != null) taskRenderer.material = incompleteMat;
    }

    public void ResetTask()
    {
        completedPlayers.Clear();
        ResetVisualsClientRpc();
    }

    public override void OnInteract(ulong interactorId)
    {
        if (!IsServer) return;

        if (GameManager.Instance.CurrentState.Value != GameManager.GameState.Gameplay) return;

        // Impostors cannot do tasks (or fake them, but they don't count)
        if (GameManager.Instance.ImpostorId.Value == interactorId) return;

        // If this specific player hasn't done this task yet
        if (!completedPlayers.Contains(interactorId))
        {
            completedPlayers.Add(interactorId);
            GameManager.Instance.CompleteTask(interactorId);

            // Tell ONLY this client to update their visual to "Done"
            SetCompleteClientRpc(RpcTarget.Single(interactorId, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SetCompleteClientRpc(RpcParams rpcParams = default)
    {
        if (taskRenderer != null) taskRenderer.material = completeMat;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ResetVisualsClientRpc()
    {
        if (taskRenderer != null) taskRenderer.material = incompleteMat;
    }
}