using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerMovement : NetworkBehaviour
{
    public float turnSpeed = 20f;
    public GameObject bombPrefab;
    public float bombRange = 5f;

    Animator m_Animator;
    Rigidbody m_Rigidbody;
    AudioSource m_AudioSource;
    Vector3 m_Movement;
    Quaternion m_Rotation = Quaternion.identity;

    private NetworkVariable<bool> netIsWalking = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public NetworkVariable<int> PlayerNum = new();

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
                new(-9.8f, 0f, -3f),
                new(-9.8f, 0f, -5f),
                new(-7.8f, 0f, -3f),
                new(-7.8f, 0f, -5f)
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

        if (IsOwner && Input.GetKeyDown(KeyCode.Space))
        {
            SpawnBombServerRpc(transform.position);
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

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
        if (!IsOwner) return;

        if (m_Rigidbody != null)
        {
            m_Rigidbody.MovePosition(m_Rigidbody.position + m_Movement * m_Animator.deltaPosition.magnitude);
            m_Rigidbody.MoveRotation(m_Rotation);
        }
    }

    [Rpc(SendTo.Server)]
    void SpawnBombServerRpc(Vector3 spawnPos)
    {
        SpawnBombClientRpc(spawnPos);
        StartCoroutine(ServerBombLogic(spawnPos));
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SpawnBombClientRpc(Vector3 spawnPos)
    {
        if (bombPrefab != null) Instantiate(bombPrefab, spawnPos, Quaternion.identity);
    }

    System.Collections.IEnumerator ServerBombLogic(Vector3 explosionPos)
    {
        yield return new WaitForSeconds(5f);
        GhostRPC[] ghosts = FindObjectsByType<GhostRPC>(FindObjectsSortMode.None);
        foreach (GhostRPC ghost in ghosts)
        {
            if (ghost == null) continue;
            if (Vector3.Distance(explosionPos, ghost.transform.position) <= bombRange) ghost.Die();
        }
    }

[Rpc(SendTo.ClientsAndHost)]
    public void CallCaughtPlayerClientRPC()
    {
        if (IsOwner)
        {
            var gameEndingObj = GameObject.Find("GameEnding");
            if (gameEndingObj != null && gameEndingObj.TryGetComponent<GameEnding>(out var gameEnding))
            {
                gameEnding.CaughtPlayer();
            }
        }
    }
}