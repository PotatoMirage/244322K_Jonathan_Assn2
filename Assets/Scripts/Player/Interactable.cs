using Unity.Netcode;
using UnityEngine;

public abstract class Interactable : NetworkBehaviour
{
    public string promptMessage = "Press E to Interact";

    public abstract void OnInteract(ulong interactorId);

    protected bool CheckRange(Vector3 playerPos, float range = 3.0f)
    {
        return Vector3.Distance(transform.position, playerPos) <= range;
    }
}