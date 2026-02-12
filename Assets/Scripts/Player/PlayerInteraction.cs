using Unity.Netcode;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    public float interactRange = 2.5f;
    public LayerMask interactLayer;

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactRange, interactLayer);
        foreach (var collider in colliders)
        {
            Interactable interactable = collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                InteractServerRpc(interactable.GetComponent<NetworkObject>().NetworkObjectId);
                return;
            }
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