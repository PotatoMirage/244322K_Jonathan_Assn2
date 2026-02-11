using UnityEngine;
using Unity.Netcode;

public class GhostRPC : NetworkBehaviour
{
    public void Die()
    {
        gameObject.SetActive(false);
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }
}