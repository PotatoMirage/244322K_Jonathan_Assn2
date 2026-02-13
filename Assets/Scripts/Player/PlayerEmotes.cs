using Unity.Netcode;
using UnityEngine;

public class PlayerEmotes : NetworkBehaviour
{
    [Header("Emote Settings")]
    public GameObject[] emotePrefabs; // Assign your emote prefabs here in Inspector
    public float emoteCooldown = 2.0f;
    public Transform emoteSpawnPoint; // Assign a transform above the player's head

    private float lastEmoteTime;

    private void Update()
    {
        if (!IsOwner) return;

        // Prevent emotes if dead or game ended
        if (GetComponent<PlayerMovement>().isDead.Value) return;
        if (Time.time - lastEmoteTime < emoteCooldown) return;

        // Input Handling (Keys 1, 2, 3, 4)
        if (Input.GetKeyDown(KeyCode.Alpha1)) RequestEmote(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) RequestEmote(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) RequestEmote(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) RequestEmote(3);
    }

    private void RequestEmote(int index)
    {
        if (index >= 0 && index < emotePrefabs.Length)
        {
            lastEmoteTime = Time.time;
            SpawnEmoteServerRpc(index);
        }
    }

    [Rpc(SendTo.Server)]
    private void SpawnEmoteServerRpc(int index)
    {
        // Server validation (optional: check if player is allowed to emote)
        SpawnEmoteClientRpc(index);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SpawnEmoteClientRpc(int index)
    {
        if (emotePrefabs[index] != null && emoteSpawnPoint != null)
        {
            Instantiate(emotePrefabs[index], emoteSpawnPoint.position, Quaternion.identity, transform);
        }
    }
}