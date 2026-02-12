using Unity.Netcode;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    public float interactRange = 2.5f;
    public LayerMask interactLayer;
    private GameUIManager uiManager;
    private Interactable currentInteractable;

    private void Update()
    {
        if (!IsOwner) return;

        if (uiManager == null) uiManager = FindFirstObjectByType<GameUIManager>();

        HandleHighlight();

        if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
        {
            InteractServerRpc(currentInteractable.NetworkObjectId);
        }
    }

    private void HandleHighlight()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, interactLayer);
        Interactable closest = null;
        float closeDist = float.MaxValue;

        foreach (var hit in hits)
        {
            Interactable interactable = hit.GetComponent<Interactable>() ?? hit.GetComponentInParent<Interactable>();
            if (interactable != null)
            {
                float d = Vector3.Distance(transform.position, interactable.transform.position);
                if (d < closeDist)
                {
                    closeDist = d;
                    closest = interactable;
                }
            }
        }

        if (closest != currentInteractable)
        {
            currentInteractable = closest;
            if (uiManager != null) uiManager.UpdateInteractionText(currentInteractable != null ? currentInteractable.promptMessage : "");
        }
    }

    [Rpc(SendTo.Server)]
    private void InteractServerRpc(ulong objId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objId, out NetworkObject obj))
        {
            if (obj.TryGetComponent<Interactable>(out var interactable))
            {
                interactable.OnInteract(OwnerClientId);
            }
        }
    }
}