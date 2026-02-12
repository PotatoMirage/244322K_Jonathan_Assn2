using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;

public class RoleRevealUI : NetworkBehaviour
{
    public TextMeshProUGUI roleText;
    public float displayDuration = 5f;
    private bool hasRevealed = false;

    private void Update()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.CurrentState.Value == GameManager.GameState.Gameplay && !hasRevealed)
        {
            ShowRole();
        }

        if (GameManager.Instance.CurrentState.Value == GameManager.GameState.Lobby)
        {
            roleText.gameObject.SetActive(false);
            hasRevealed = false;
        }
    }

    private void ShowRole()
    {
        hasRevealed = true;
        roleText.gameObject.SetActive(true);

        bool isImpostor = GameManager.Instance.ImpostorId.Value == NetworkManager.Singleton.LocalClientId;
        roleText.text = isImpostor
            ? "YOU ARE THE <color=red>IMPOSTOR</color>\n\nKILL EVERYONE"
            : "YOU ARE A <color=#00FFFF>CREWMATE</color>\n\nCOMPLETE TASKS";

        StartCoroutine(HideTextRoutine());
    }

    private IEnumerator HideTextRoutine()
    {
        yield return new WaitForSeconds(displayDuration);
        roleText.gameObject.SetActive(false);
    }
}