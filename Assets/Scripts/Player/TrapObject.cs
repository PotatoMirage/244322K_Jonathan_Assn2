using Unity.Netcode;
using UnityEngine;

public class TrapObject : Interactable
{
    public float trapRadius = 4f;
    public ParticleSystem trapVFX;
    private bool isCooldown = false;

    public override void OnInteract(ulong interactorId)
    {
        if (!IsServer || isCooldown) return;

        if (GameManager.Instance.ImpostorId.Value == interactorId)
        {
            TriggerTrap();
        }
    }

    private void TriggerTrap()
    {
        isCooldown = true;
        TriggerTrapClientRpc();

        Collider[] hits = Physics.OverlapSphere(transform.position, trapRadius);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<PlayerMovement>(out var player) && !player.isDead.Value)
            {
                if (player.OwnerClientId != GameManager.Instance.ImpostorId.Value)
                {
                    player.isDead.Value = true; // This auto-triggers death logic
                    GameManager.Instance.OnPlayerDied(player.OwnerClientId);
                }
            }
        }
        Invoke(nameof(ResetTrap), 10f);
    }

    private void ResetTrap() => isCooldown = false;

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerTrapClientRpc()
    {
        if (trapVFX != null) trapVFX.Play();
    }
}