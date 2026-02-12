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

        // Check if game just started and we haven't shown role yet
        if (GameManager.Instance.IsGameActive.Value && !hasRevealed)
        {
            ShowRole();
        }

        // Hide if game is not active
        if (!GameManager.Instance.IsGameActive.Value)
        {
            roleText.gameObject.SetActive(false);
            hasRevealed = false; // Reset for next round
        }
    }

    private void ShowRole()
    {
        hasRevealed = true;
        roleText.gameObject.SetActive(true);

        ulong myId = NetworkManager.Singleton.LocalClientId;
        bool isImpostor = (GameManager.Instance.ImpostorId.Value == myId);

        if (isImpostor)
        {
            roleText.text = "YOU ARE THE <color=red>IMPOSTOR</color>\n\nKILL EVERYONE";
        }
        else
        {
            roleText.text = "YOU ARE A <color=#00FFFF>CREWMATE</color>\n\nCOMPLETE TASKS";
        }

        StartCoroutine(HideTextRoutine());
    }

    private IEnumerator HideTextRoutine()
    {
        yield return new WaitForSeconds(displayDuration);
        roleText.gameObject.SetActive(false);
    }
}