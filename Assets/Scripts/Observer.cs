using UnityEngine;
using Unity.Netcode;

public class Observer : NetworkBehaviour
{
    public Transform player;
    bool m_IsPlayerInRange;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerMovement>())
        {
            player = other.transform;
            m_IsPlayerInRange = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerMovement>()) m_IsPlayerInRange = false;
    }

    void Update()
    {
        if (!IsServer || !m_IsPlayerInRange || player == null) return;

        Vector3 direction = player.position - transform.position + Vector3.up;
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit))
        {
            if (hit.collider.transform == player)
            {
                if (player.TryGetComponent<PlayerMovement>(out var script) && !script.isDead.Value)
                {
                    script.isDead.Value = true; // Auto-triggers death
                    GameManager.Instance.OnPlayerDied(script.OwnerClientId);
                }
            }
        }
    }
}