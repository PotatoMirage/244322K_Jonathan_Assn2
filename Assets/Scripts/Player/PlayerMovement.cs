using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
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
    private PlayerMovement _cachedLocalPlayer;
    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>("");

    private PlayerMovement LocalPlayer
    {
        get
        {
            if (_cachedLocalPlayer == null && NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                _cachedLocalPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
            }
            return _cachedLocalPlayer;
        }
    }
    public override void OnNetworkSpawn()
    {
        if (IsServer) PlayerNum.Value = (int)OwnerClientId;
        if (IsOwner)
        {
            FindMyCamera();

            string myName = $"Player {OwnerClientId}";
            try
            {
                if (AuthenticationService.Instance.IsSignedIn && !string.IsNullOrEmpty(AuthenticationService.Instance.PlayerName))
                {
                    myName = AuthenticationService.Instance.PlayerName;

                    if (myName.Contains("#"))
                    {
                        myName = myName.Split('#')[0];
                    }
                }
            }
            catch { }

            // --- FIX STARTS HERE ---
            // Instead of setting it on this script, set it on the PlayerPlayerData component
            var playerData = GetComponent<PlayerPlayerData>();
            if (playerData != null)
            {
                playerData.SetPlayerNameServerRpc(myName);
            }
            // You can keep this if you want PlayerMovement to also have the name, 
            // but PlayerNameTag looks at PlayerPlayerData.
            SetPlayerNameServerRpc(myName);
            // --- FIX ENDS HERE ---
        }

        isDead.OnValueChanged += OnDeathStateChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;

        StoreOriginalMaterials();

        UpdateMaterialState(isDead.Value);
    }

    public override void OnNetworkDespawn()
    {
        isDead.OnValueChanged -= OnDeathStateChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CancelInvoke(nameof(RefreshVisibility));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsOwner) FindMyCamera();
    }
    [Rpc(SendTo.Server)]
    public void SetPlayerNameServerRpc(FixedString32Bytes name)
    {
        PlayerName.Value = name;
    }
    private void StoreOriginalMaterials()
    {
        foreach (var r in playerRenderers)
        {
            if (r != null && !originalMaterials.ContainsKey(r))
            {
                originalMaterials[r] = r.sharedMaterials;
            }
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
        UpdateAnimationState();

        if (!IsOwner) return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState.Value != GameManager.GameState.Gameplay && !isDead.Value)
        {
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
            Vector3 desiredMove = m_Movement * ghostSpeed * Time.fixedDeltaTime;
            m_Rigidbody.MovePosition(m_Rigidbody.position + desiredMove);
            m_Rigidbody.linearVelocity = Vector3.zero;
        }
        else
        {
            if (m_Movement.sqrMagnitude > 0)
            {
                Vector3 desiredForward = Vector3.RotateTowards(transform.forward, m_Movement, turnSpeed * Time.fixedDeltaTime, 0f);
                m_Rotation = Quaternion.LookRotation(desiredForward);
                m_Rigidbody.MoveRotation(m_Rotation);
            }

            Vector3 currentVel = m_Rigidbody.linearVelocity;
            m_Rigidbody.linearVelocity = new Vector3(0f, currentVel.y, 0f);
            m_Rigidbody.angularVelocity = Vector3.zero;
        }
    }
    private void LateUpdate()
    {
        // Safety check: If game isn't running or local player isn't ready, do nothing
        if (NetworkManager.Singleton == null || LocalPlayer == null) return;

        bool amILocal = IsOwner;
        bool isLocalViewerDead = LocalPlayer.isDead.Value;
        bool thisPlayerIsDead = isDead.Value;

        // LOGIC:
        // 1. If this player is ALIVE, everyone sees them.
        // 2. If this player is DEAD, they are visible ONLY if:
        //    a. I am the owner (I see my own ghost)
        //    b. I am also dead (Ghosts see ghosts)
        bool shouldBeVisible = !thisPlayerIsDead || (amILocal || isLocalViewerDead);

        // Enforce visibility on all renderers
        foreach (var r in playerRenderers)
        {
            if (r != null && r.enabled != shouldBeVisible)
            {
                r.enabled = shouldBeVisible;
            }
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

    public void Revive()
    {
        if (IsServer) isDead.Value = false;
        HandleRevive();
        StartCoroutine(ClientRefreshVisibilityCoroutine());
    }

    private void OnDeathStateChanged(bool prev, bool current)
    {
        UpdateMaterialState(current);

        if (current) HandleDeath();
        else HandleRevive();
    }

    private void UpdateMaterialState(bool isDeadState)
    {
        foreach (var r in playerRenderers)
        {
            if (r == null) continue;

            if (isDeadState)
            {
                // Switch to Ghost Material
                if (ghostMaterial != null)
                {
                    // Check to avoid redundant array allocations
                    if (r.sharedMaterial != ghostMaterial)
                    {
                        Material[] ghostMats = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < ghostMats.Length; i++) ghostMats[i] = ghostMaterial;
                        r.materials = ghostMats;
                    }
                }
            }
            else
            {
                // Revert to Original Materials
                if (originalMaterials.ContainsKey(r))
                {
                    r.materials = originalMaterials[r];
                }
            }
        }
    }

    public void TriggerVisibilityCheck()
    {
        StartCoroutine(ClientRefreshVisibilityCoroutine());
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

    private void HandleRevive()
    {
        if (playerCollider != null) playerCollider.enabled = true;
        if (m_Rigidbody)
        {
            m_Rigidbody.useGravity = true;
            m_Rigidbody.isKinematic = false;
        }
        // Material reset is handled by UpdateMaterialState
    }

    public void TeleportTo(Vector3 pos)
    {
        if (IsServer)
        {
            transform.position = pos;
            TeleportClientRpc(pos);
        }
    }

    [Rpc(SendTo.Owner)]
    public void TeleportClientRpc(Vector3 newPos)
    {
        // 1. Handle CharacterController (Existing Logic)
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // 2. Update Transform
        transform.position = newPos;

        // 3. FIX: Handle Rigidbody explicitly
        if (m_Rigidbody != null)
        {
            m_Rigidbody.position = newPos;
            m_Rigidbody.linearVelocity = Vector3.zero; // Stop any momentum
        }
        // Alternatively, if m_Rigidbody is private, use GetComponent:
        // if (TryGetComponent<Rigidbody>(out Rigidbody rb)) { rb.position = newPos; rb.linearVelocity = Vector3.zero; }

        // 4. Re-enable CharacterController
        if (cc != null) cc.enabled = true;
    }

    private IEnumerator ClientRefreshVisibilityCoroutine()
    {
        int attempts = 0;
        while (attempts < 10)
        {
            RefreshVisibility();
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
    }

    public void RefreshVisibility()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null || NetworkManager.Singleton.LocalClient.PlayerObject == null) return;

        var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        if (localPlayer == null) return;

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
        if (!IsOwner || isDead.Value) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState.Value != GameManager.GameState.Gameplay) return;

        if (m_Animator != null && m_Rigidbody != null)
        {
            m_Rigidbody.MovePosition(m_Rigidbody.position + m_Animator.deltaPosition);
        }
    }
}