using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;

public class RoleRevealUI : MonoBehaviour
{
    public static RoleRevealUI Instance;

    [Header("UI References")]
    public GameObject panel;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI goalText;
    public Color crewmateColor = Color.cyan;
    public Color impostorColor = Color.red;

    private void Awake() { Instance = this; }

    private void Start()
    {
        panel.SetActive(false);
        // Subscribe to Game State changes to trigger reveal
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentState.OnValueChanged += OnGameStateChanged;
        }
    }

    private void OnGameStateChanged(GameManager.GameState oldState, GameManager.GameState newState)
    {
        if (newState == GameManager.GameState.Gameplay && oldState == GameManager.GameState.Lobby)
        {
            ShowRole();
        }
    }

    public void ShowRole()
    {
        panel.SetActive(true);
        ulong localId = NetworkManager.Singleton.LocalClientId;
        ulong impostorId = GameManager.Instance.ImpostorId.Value;

        if (localId == impostorId)
        {
            roleText.text = "IMPOSTOR";
            roleText.color = impostorColor;
            goalText.text = "Kill everyone. Sabotage the ship.";
        }
        else
        {
            roleText.text = "CREWMATE";
            roleText.color = crewmateColor;
            goalText.text = "Complete tasks. Discover the Impostor.";
        }

        StartCoroutine(HideDelay());
    }

    private IEnumerator HideDelay()
    {
        yield return new WaitForSeconds(3f);
        panel.SetActive(false);
    }
}