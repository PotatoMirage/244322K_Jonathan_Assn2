using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // Needed for referencing TMP components

public class PlayerMovement : NetworkBehaviour
{
    public float turnSpeed = 20f;
    public float moveSpeed = 5f;
    public float ghostSpeed = 8f;

    public float killRange = 2.0f;

    Animator m_Animator;
    Rigidbody m_Rigidbody;
    AudioSource m_AudioSource;
    Vector3 m_Movement;
    Quaternion m_Rotation = Quaternion.identity;

    public NetworkVariable<int> PlayerNum = new NetworkVariable<int>();
    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(false);

    private NetworkVariable<bool> netIsWalking = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        if (IsServer) PlayerNum.Value = (int)OwnerClientId;
        if (IsOwner) FindMyCamera();

        isDead.OnValueChanged += OnDeathStateChanged;
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
            SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);

            if (points.Length > 0)
            {
                int index = (int)OwnerClientId % points.Length;
                transform.position = points[index].transform.position;
                transform.rotation = points[index].transform.rotation;
            }
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
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive.Value && !isDead.Value)
        {
            m_Animator.SetBool("IsWalking", false);
            return;
        }

        if (IsOwner)
        {
            HandleInput();
            HandleEmotes();
        }

        UpdateAnimationState();
    }

    void HandleInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        m_Movement.Set(horizontal, 0f, vertical);
        m_Movement.Normalize();

        bool moving = m_Movement.magnitude > 0.01f;
        if (netIsWalking.Value != moving) netIsWalking.Value = moving;

        if (Input.GetKeyDown(KeyCode.Space) && IsImpostor() && !isDead.Value)
        {
            TryKillTarget();
        }
    }

    void HandleEmotes()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) PlayEmoteServerRpc("😀");
        if (Input.GetKeyDown(KeyCode.Alpha2)) PlayEmoteServerRpc("😂");
    }

    void UpdateAnimationState()
    {
        if (isDead.Value)
        {
            m_Animator.SetBool("IsWalking", false);
            return;
        }
        m_Animator.SetBool("IsWalking", netIsWalking.Value);
    }
    void OnAnimatorMove()
    {
        if (!IsOwner) return;

        if (m_Rigidbody != null)
        {
            m_Rigidbody.MovePosition(m_Rigidbody.position + m_Movement * m_Animator.deltaPosition.magnitude);
            m_Rigidbody.MoveRotation(m_Rotation);
        }
    }

    void FixedUpdate()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive.Value && !isDead.Value) return;
        if (!IsOwner) return;

        float currentSpeed = isDead.Value ? ghostSpeed : moveSpeed;
        Vector3 desiredMove = m_Movement * currentSpeed * Time.deltaTime;

        if (m_Rigidbody != null && !m_Rigidbody.isKinematic)
        {
            m_Rigidbody.MovePosition(m_Rigidbody.position + desiredMove);
            if (m_Movement.magnitude > 0)
            {
                Vector3 desiredForward = Vector3.RotateTowards(transform.forward, m_Movement, turnSpeed * Time.deltaTime, 0f);
                m_Rotation = Quaternion.LookRotation(desiredForward);
                m_Rigidbody.MoveRotation(m_Rotation);
            }
        }
        else if (isDead.Value)
        {
            transform.position += desiredMove;
        }
    }

    [Rpc(SendTo.Server)]
    void PlayEmoteServerRpc(string emoteName)
    {
        PlayEmoteClientRpc(emoteName);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlayEmoteClientRpc(string emoteName)
    {
        GameObject prefab = Resources.Load<GameObject>("EmoteBubble");
        if (prefab != null)
        {
            GameObject emoteInstance = Instantiate(prefab, transform.position + Vector3.up * 2.5f, Quaternion.identity);
            var textComp = emoteInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null) textComp.text = emoteName;
        }
    }

    bool IsImpostor()
    {
        if (GameManager.Instance == null) return false;
        return GameManager.Instance.ImpostorId.Value == OwnerClientId;
    }

    void TryKillTarget()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, killRange);
        foreach (var hitCollider in hitColliders)
        {
            PlayerMovement target = hitCollider.GetComponent<PlayerMovement>();
            if (target != null && target != this && !target.isDead.Value)
            {
                KillPlayerServerRpc(target.OwnerClientId);
                return;
            }
        }
    }

    [Rpc(SendTo.Server)]
    void KillPlayerServerRpc(ulong targetId)
    {
        if (!IsImpostor() || !GameManager.Instance.IsGameActive.Value) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(targetId, out NetworkClient client))
        {
            PlayerMovement targetScript = client.PlayerObject.GetComponent<PlayerMovement>();
            if (targetScript != null && !targetScript.isDead.Value)
            {
                float distance = Vector3.Distance(transform.position, targetScript.transform.position);
                if (distance <= killRange + 1.0f)
                {
                    targetScript.isDead.Value = true;
                    targetScript.CallCaughtPlayerClientRPC();
                    GameManager.Instance.OnPlayerDied(targetId);
                }
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void CallCaughtPlayerClientRPC()
    {
        // Handled by OnDeathStateChanged
    }

    private void OnDeathStateChanged(bool previous, bool current)
    {
        if (current)
        {
            HandleDeath();

            if (IsServer && GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerDied(OwnerClientId);
            }
        }
    }

    private void HandleDeath()
    {
        m_Animator.SetBool("IsWalking", false);

        if (GetComponent<Collider>()) GetComponent<Collider>().enabled = false;
        if (m_Rigidbody) m_Rigidbody.isKinematic = true;

        if (!IsOwner)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                Color c = r.material.color;
                c.a = 0.3f;
                r.material.color = c;
            }
        }
    }
}