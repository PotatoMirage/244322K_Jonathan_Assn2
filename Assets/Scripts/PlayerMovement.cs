using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerMovement : NetworkBehaviour
{
    public float turnSpeed = 20f;
    public GameObject bombPrefab; // Kept to prevent missing reference errors

    // --- KILL SETTINGS ---
    public float killRange = 2.0f;

    Animator m_Animator;
    Rigidbody m_Rigidbody;
    AudioSource m_AudioSource;
    Vector3 m_Movement;
    Quaternion m_Rotation = Quaternion.identity;

    // Restored variable for compatibility with your other scripts
    public NetworkVariable<int> PlayerNum = new NetworkVariable<int>();

    // State to track if this player is dead
    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(false);

    private NetworkVariable<bool> netIsWalking = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        if (IsServer) PlayerNum.Value = (int)OwnerClientId;
        if (IsOwner) FindMyCamera();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public override void OnNetworkDespawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsOwner) FindMyCamera();
    }

    private void FindMyCamera()
    {
        if (!IsOwner) return;

        var virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        if (virtualCamera != null)
        {
            virtualCamera.Follow = transform;
            virtualCamera.LookAt = transform;
        }

        if (SceneManager.GetActiveScene().name == "MainScene")
        {
            Vector3[] spawnSlots = new Vector3[]
            {
                new Vector3(-9.8f, 0f, -3f),
                new Vector3(-9.8f, 0f, -5f),
                new Vector3(-7.8f, 0f, -3f),
                new Vector3(-7.8f, 0f, -5f)
            };
            int spawnIndex = (int)OwnerClientId % spawnSlots.Length;
            transform.SetPositionAndRotation(spawnSlots[spawnIndex], Quaternion.identity);
            if (m_Rigidbody != null) m_Rigidbody.linearVelocity = Vector3.zero;
        }
    }

    void Start()
    {
        m_Animator = GetComponent<Animator>();
        m_Rigidbody = GetComponent<Rigidbody>();
        m_AudioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        // 1. FREEZE if Game has not started (Countdown Phase)
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive.Value)
        {
            // Ensure animations/audio are stopped
            m_Animator.SetBool("IsWalking", false);
            if (m_AudioSource.isPlaying) m_AudioSource.Stop();
            return;
        }

        // 2. If dead, stop everything
        if (isDead.Value)
        {
            m_Animator.SetBool("IsWalking", false);
            m_AudioSource.Stop();
            return;
        }

        bool isWalking = netIsWalking.Value;
        m_Animator.SetBool("IsWalking", isWalking);

        if (isWalking)
        {
            if (!m_AudioSource.isPlaying) m_AudioSource.Play();
        }
        else
        {
            m_AudioSource.Stop();
        }

        if (IsOwner)
        {
            // 3. Kill Input (Spacebar) - Only if Impostor
            if (Input.GetKeyDown(KeyCode.Space) && IsImpostor())
            {
                TryKillTarget();
            }
        }
    }

    // Helper to check role
    bool IsImpostor()
    {
        if (GameManager.Instance == null) return false;
        return GameManager.Instance.ImpostorId.Value == OwnerClientId;
    }

    void TryKillTarget()
    {
        // Client-side check to find the closest target
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, killRange);
        foreach (var hitCollider in hitColliders)
        {
            PlayerMovement target = hitCollider.GetComponent<PlayerMovement>();

            // If it is a valid target
            if (target != null && target != this && !target.isDead.Value)
            {
                // Send request to server
                KillPlayerServerRpc(target.OwnerClientId);
                return; // Only kill one person at a time
            }
        }
    }

    [Rpc(SendTo.Server)]
    void KillPlayerServerRpc(ulong targetId)
    {
        // SERVER SIDE SECURITY CHECKS

        // 1. Am I actually the impostor?
        if (!IsImpostor()) return;

        // 2. Is the game active?
        if (!GameManager.Instance.IsGameActive.Value) return;

        // 3. Find the target object
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(targetId, out NetworkClient client))
        {
            PlayerMovement targetScript = client.PlayerObject.GetComponent<PlayerMovement>();

            if (targetScript != null && !targetScript.isDead.Value)
            {
                // 4. RANGE CHECK
                // Calculate distance between ME (Server version) and TARGET (Server version)
                float distance = Vector3.Distance(transform.position, targetScript.transform.position);

                // Allow a small buffer (0.5f) for network lag/latency
                if (distance <= killRange + 0.5f)
                {
                    targetScript.isDead.Value = true;
                    targetScript.HandleDeathClientRpc();
                }
                else
                {
                    Debug.LogWarning($"Kill rejected: Target too far ({distance}m)");
                }
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void HandleDeathClientRpc()
    {
        m_Animator.SetBool("IsWalking", false);

        // Rotate to show death
        transform.Rotate(90, 0, 0);

        if (m_Rigidbody != null) m_Rigidbody.isKinematic = true;
        if (GetComponent<Collider>() != null) GetComponent<Collider>().enabled = false;
    }

    void FixedUpdate()
    {
        // Freeze checks for Physics
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive.Value) return;
        if (!IsOwner || isDead.Value) return;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        m_Movement.Set(horizontal, 0f, vertical);
        m_Movement.Normalize();
        bool moving = m_Movement.magnitude > 0.01f;

        if (netIsWalking.Value != moving)
        {
            netIsWalking.Value = moving;
        }

        Vector3 desiredForward = Vector3.RotateTowards(transform.forward, m_Movement, turnSpeed * Time.deltaTime, 0f);
        m_Rotation = Quaternion.LookRotation(desiredForward);
    }

    void OnAnimatorMove()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive.Value) return;
        if (!IsOwner || isDead.Value) return;

        if (m_Rigidbody != null)
        {
            m_Rigidbody.MovePosition(m_Rigidbody.position + m_Movement * m_Animator.deltaPosition.magnitude);
            m_Rigidbody.MoveRotation(m_Rotation);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void CallCaughtPlayerClientRPC()
    {
        if (IsServer)
        {
            isDead.Value = true;
            HandleDeathClientRpc();
        }
    }

    // UI Display
    void OnGUI()
    {
        if (!IsOwner) return;
        if (GameManager.Instance == null) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.white;

        // COUNTDOWN UI
        if (!GameManager.Instance.IsGameActive.Value)
        {
            float timeLeft = Mathf.Ceil(GameManager.Instance.CountdownTimer.Value);
            string message = timeLeft > 0 ? "STARTING IN: " + timeLeft : "GO!";
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 50, 300, 100), message, style);
        }
        // GAMEPLAY UI
        else
        {
            string role = IsImpostor() ? "IMPOSTOR" : "CREWMATE";
            style.normal.textColor = IsImpostor() ? Color.red : Color.cyan;

            GUI.Label(new Rect(20, 20, 300, 100), "Role: " + role, style);

            if (IsImpostor())
            {
                GUI.Label(new Rect(20, 70, 300, 50), "Press SPACE to Kill", style);
            }

            if (isDead.Value)
            {
                style.normal.textColor = Color.red;
                GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2, 400, 100), "YOU ARE DEAD", style);
            }
        }
    }
}