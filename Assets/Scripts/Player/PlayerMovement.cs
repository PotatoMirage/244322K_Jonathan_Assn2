using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float turnSpeed = 20f;
    public float moveSpeed = 5f;
    public float ghostSpeed = 8f;

    [Header("Impostor Settings")]
    public float killRange = 2.0f;

    [Header("References")]
    public GameObject deadBodyPrefab;
    public Renderer[] playerRenderers;
    public Collider playerCollider;
    public Material ghostMaterial;

    private Animator m_Animator;
    private Rigidbody m_Rigidbody;
    private Vector3 m_Movement;
    private Quaternion m_Rotation = Quaternion.identity;
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    public NetworkVariable<int> PlayerNum = new NetworkVariable<int>();
    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> netIsWalking = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

    public override void OnNetworkSpawn()
    {
        if (IsServer) PlayerNum.Value = (int)OwnerClientId;
        if (IsOwner) FindMyCamera();

        isDead.OnValueChanged += OnDeathStateChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;

        StoreOriginalMaterials();
    }

    public override void OnNetworkDespawn()
    {
        isDead.OnValueChanged -= OnDeathStateChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsOwner) FindMyCamera();
    }
    private void StoreOriginalMaterials()
    {
        foreach (var r in playerRenderers)
        {
            if (r != null) originalMaterials[r] = r.sharedMaterials;
        }
    }
    private void FindMyCamera()
    {
        var virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        if (virtualCamera != null)
        {
            virtualCamera.Follow = transform;
            virtualCamera.LookAt = transform;
        }
    }

    private void Start()
    {
        m_Animator = GetComponent<Animator>();
        m_Rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // FIX: Call this OUTSIDE the IsOwner check so all clients see the animation
        UpdateAnimationState();

        if (!IsOwner) return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState.Value != GameManager.GameState.Gameplay && !isDead.Value)
        {
            // Reset walking state if gameplay is paused
            if (netIsWalking.Value) netIsWalking.Value = false;
            if (m_Animator) m_Animator.SetBool(IsWalkingHash, false);
            return;
        }

        HandleInput();

        if (Input.GetKeyDown(KeyCode.Space) && IsImpostor() && !isDead.Value) TryKillTarget();
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState.Value != GameManager.GameState.Gameplay && !isDead.Value) return;

        if (isDead.Value)
        {
            // Ghost Movement (Remains Manual / Flying)
            Vector3 desiredMove = m_Movement * ghostSpeed * Time.fixedDeltaTime;
            m_Rigidbody.MovePosition(m_Rigidbody.position + desiredMove);
            m_Rigidbody.linearVelocity = Vector3.zero;
        }
        else
        {
            // Living Movement (Root Motion Logic)

            // 1. ROTATION: Handled manually for snappy control
            if (m_Movement.sqrMagnitude > 0)
            {
                Vector3 desiredForward = Vector3.RotateTowards(transform.forward, m_Movement, turnSpeed * Time.fixedDeltaTime, 0f);
                m_Rotation = Quaternion.LookRotation(desiredForward);
                m_Rigidbody.MoveRotation(m_Rotation);
            }

            // 2. STOP SLIDING: Explicitly zero out physics momentum.
            // This prevents "ice sliding" after collisions or spawning.
            // We preserve 'y' so gravity still works, but kill x/z velocity.
            Vector3 currentVel = m_Rigidbody.linearVelocity;
            m_Rigidbody.linearVelocity = new Vector3(0f, currentVel.y, 0f);
            m_Rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void HandleInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        m_Movement.Set(h, 0f, v);
        m_Movement.Normalize();

        bool moving = m_Movement.sqrMagnitude > 0.01f;
        if (netIsWalking.Value != moving) netIsWalking.Value = moving;
    }

    private void UpdateAnimationState()
    {
        if (isDead.Value)
        {
            m_Animator.SetBool(IsWalkingHash, false);
            return;
        }
        m_Animator.SetBool(IsWalkingHash, netIsWalking.Value);
    }

    private bool IsImpostor()
    {
        return GameManager.Instance != null && GameManager.Instance.ImpostorId.Value == OwnerClientId;
    }

    private void TryKillTarget()
    {
        int layerMask = LayerMask.GetMask("Player");
        Collider[] hits = Physics.OverlapSphere(transform.position, killRange);

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<PlayerMovement>(out var target))
            {
                if (target != this && !target.isDead.Value)
                {
                    KillPlayerServerRpc(target.OwnerClientId);
                    return;
                }
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void KillPlayerServerRpc(ulong targetId)
    {
        if (!IsImpostor() || GameManager.Instance.CurrentState.Value != GameManager.GameState.Gameplay) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(targetId, out NetworkClient client))
        {
            var target = client.PlayerObject.GetComponent<PlayerMovement>();
            if (target != null && !target.isDead.Value)
            {
                if (Vector3.Distance(transform.position, target.transform.position) <= killRange + 1f)
                {
                    target.isDead.Value = true;
                    GameManager.Instance.OnPlayerDied(targetId);
                }
            }
        }
    }

    private void OnDeathStateChanged(bool prev, bool current)
    {
        if (current) HandleDeath();

        foreach (var p in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            p.RefreshVisibility();
        }
    }

    private void HandleDeath()
    {
        if (m_Animator) m_Animator.SetBool(IsWalkingHash, false);
        if (playerCollider != null) playerCollider.enabled = false;

        if (m_Rigidbody)
        {
            m_Rigidbody.useGravity = false;
            m_Rigidbody.isKinematic = true;
            m_Rigidbody.linearVelocity = Vector3.zero;
        }

        if (IsServer && deadBodyPrefab != null)
        {
            GameObject body = Instantiate(deadBodyPrefab, transform.position, transform.rotation);
            body.transform.Rotate(-90, 0, 0);
            body.GetComponent<NetworkObject>().Spawn();
            body.GetComponent<DeadBody>().SetupBody(PlayerNum.Value);
        }
    }

    public void TeleportTo(Vector3 pos)
    {
        if (IsServer)
        {
            transform.position = pos;
            TeleportClientRpc(pos);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TeleportClientRpc(Vector3 pos)
    {
        transform.position = pos;
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    public void RefreshVisibility()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null || NetworkManager.Singleton.LocalClient.PlayerObject == null) return;

        var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        bool amILocal = IsOwner;
        bool isLocalDead = localPlayer.isDead.Value;
        bool thisPlayerIsDead = isDead.Value;

        if (!thisPlayerIsDead)
        {
            SetRenderersVisible(true, false);
        }
        else
        {
            bool shouldSee = amILocal || isLocalDead;
            SetRenderersVisible(shouldSee, true);
        }
    }
    private void SetRenderersVisible(bool visible, bool isGhostMaterial)
    {
        foreach (var r in playerRenderers)
        {
            if (r == null) continue;

            r.enabled = visible;

            if (visible && isGhostMaterial && ghostMaterial != null)
            {
                Material[] ghostMats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < ghostMats.Length; i++) ghostMats[i] = ghostMaterial;
                r.materials = ghostMats;
            }
            else if (visible && !isGhostMaterial && originalMaterials.ContainsKey(r))
            {
                r.materials = originalMaterials[r];
            }
        }
    }
    private void OnAnimatorMove()
    {
        // Only the Owner applies Root Motion to the Rigidbody
        if (!IsOwner) return;

        // Ghosts do not use Root Motion (they fly manually)
        if (isDead.Value) return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState.Value != GameManager.GameState.Gameplay) return;

        if (m_Animator != null && m_Rigidbody != null)
        {
            // Apply Root Motion from the Animation to the Rigidbody.
            // This ensures the movement matches the feet exactly.
            m_Rigidbody.MovePosition(m_Rigidbody.position + m_Animator.deltaPosition);

            // We do NOT apply Root Rotation here because we handle it manually 
            // in FixedUpdate for better gameplay responsiveness.
        }
    }
}