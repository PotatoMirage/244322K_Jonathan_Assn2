using Unity.Netcode;
using UnityEngine;

public abstract class Interactable : NetworkBehaviour
{
    public string promptMessage = "Press E";

    public abstract void OnInteract(ulong interactorId);

    protected bool ValidateRange(ulong interactorId, float range = 5.0f)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(interactorId, out NetworkClient client))
        {
            return Vector3.Distance(transform.position, client.PlayerObject.transform.position) <= range;
        }
        return false;
    }
}