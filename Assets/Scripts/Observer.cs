using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Observer : MonoBehaviour
{
    public Transform player;
    public GameEnding gameEnding;

    bool m_IsPlayerInRange;

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "JohnLemon(Clone)")
        {
            player = other.transform;
            m_IsPlayerInRange = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name == "JohnLemon(Clone)")
        {
            m_IsPlayerInRange = false;
        }
    }

    void Update()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (m_IsPlayerInRange)
        {
            Vector3 direction = player.position - transform.position + Vector3.up;
            Ray ray = new(transform.position, direction);

            if (Physics.Raycast(ray, out RaycastHit raycastHit))
            {
                if (raycastHit.collider.transform == player)
                {
                    if (player.TryGetComponent<PlayerMovement>(out PlayerMovement playerScript))
                    {
                        playerScript.CallCaughtPlayerClientRPC();
                    }
                }
            }
        }
    }
}