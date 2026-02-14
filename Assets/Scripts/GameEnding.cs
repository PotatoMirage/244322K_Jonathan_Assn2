using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class GameEnding : NetworkBehaviour
{
    public float fadeDuration = 1f;
    public float displayImageDuration = 1f;
    public CanvasGroup exitBackgroundImageCanvasGroup;
    public AudioSource exitAudio;
    public CanvasGroup caughtBackgroundImageCanvasGroup;
    public AudioSource caughtAudio;

    public string lobbySceneName = "Lobby";
    public string gameplaySceneName = "MainScene";

    private HashSet<ulong> playersAtExit = new HashSet<ulong>();

    bool m_IsPlayerAtExit;
    bool m_IsPlayerCaught;
    float m_Timer;
    bool m_HasAudioPlayed;

    private void Update()
    {
        if (IsServer) CheckWinCondition();

        if (m_IsPlayerAtExit)
            EndLevel(exitBackgroundImageCanvasGroup, false, exitAudio);
        else if (m_IsPlayerCaught)
            EndLevel(caughtBackgroundImageCanvasGroup, true, caughtAudio);
    }

    private void CheckWinCondition()
    {
        int totalLivingPlayers = 0;
        int playersInZone = playersAtExit.Count;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            PlayerMovement playerScript = client.PlayerObject.GetComponent<PlayerMovement>();
            if (playerScript != null && !playerScript.isDead.Value)
            {
                totalLivingPlayers++;
            }
        }

        if (totalLivingPlayers == 0) return;

        if (playersInZone >= totalLivingPlayers)
        {
            TriggerWinClientRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void TriggerWinClientRpc()
    {
        m_IsPlayerAtExit = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerMovement>())
        {
            if (IsServer)
            {
                ulong id = other.gameObject.GetComponent<NetworkObject>().OwnerClientId;
                playersAtExit.Add(id);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerMovement>())
        {
            if (IsServer)
            {
                ulong id = other.gameObject.GetComponent<NetworkObject>().OwnerClientId;
                playersAtExit.Remove(id);
            }
        }
    }

    public void CaughtPlayer()
    {
        m_IsPlayerCaught = true;
    }

    void EndLevel(CanvasGroup imageCanvasGroup, bool doRestart, AudioSource audioSource)
    {
        if (!m_HasAudioPlayed)
        {
            audioSource.Play();
            m_HasAudioPlayed = true;
        }

        m_Timer += Time.deltaTime;
        imageCanvasGroup.alpha = m_Timer / fadeDuration;

        if (m_Timer > fadeDuration + displayImageDuration)
        {
            if (IsServer)
            {
                string sceneToLoad = doRestart ? lobbySceneName : gameplaySceneName;
                NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad, UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }
    }
}