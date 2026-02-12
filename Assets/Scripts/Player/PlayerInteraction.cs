using Unity.Netcode;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    public float interactRange = 2.5f;
    public LayerMask interactLayer;

    private GameUIManager uiManager;
    private Interactable currentInteractable;

    public override void OnNetworkSpawn()
    {
        uiManager = FindFirstObjectByType<GameUIManager>();
        if (uiManager == null) Debug.LogError("⚠ PLAYER CANNOT FIND GAMEUIManager!");
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<GameUIManager>();
        }

        HandleInteractionHighlight();

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    private void HandleInteractionHighlight()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactRange, interactLayer);
        Interactable closestInteractable = null;
        float closestDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            // Try to find the script on the object or its parent
            Interactable interactable = collider.GetComponent<Interactable>();
            if (interactable == null) interactable = collider.GetComponentInParent<Interactable>();

            if (interactable != null)
            {
                float distance = Vector3.Distance(transform.position, interactable.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestInteractable = interactable;
                }
            }
        }

        // Only update if something changed
        if (closestInteractable != currentInteractable)
        {
            currentInteractable = closestInteractable;

            if (uiManager != null)
            {
                string msg = currentInteractable != null ? currentInteractable.promptMessage : "";
                uiManager.UpdateInteractionText(msg);
            }
        }
    }

    private void TryInteract()
    {
        if (currentInteractable != null)
        {
            InteractServerRpc(currentInteractable.GetComponent<NetworkObject>().NetworkObjectId);
        }
    }

    [Rpc(SendTo.Server)]
    private void InteractServerRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            Interactable interactable = netObj.GetComponent<Interactable>();
            if (interactable != null)
            {
                interactable.OnInteract(OwnerClientId);
            }
        }
    }
}