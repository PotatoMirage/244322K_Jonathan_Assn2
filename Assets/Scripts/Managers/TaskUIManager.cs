using UnityEngine;

public class TaskUIManager : MonoBehaviour
{
    public static TaskUIManager Instance;
    public Transform minigameContainer; // Assign an empty panel inside your Canvas
    public GameObject backgroundBlocker; // A semi-transparent black panel to block movement input

    private GameObject currentMinigame;
    private TaskInteractable currentTask;

    private void Awake() { Instance = this; }

    public void OpenTaskUI(GameObject prefab, TaskInteractable task)
    {
        CloseTaskUI(); // Safety cleanup

        currentTask = task;
        backgroundBlocker.SetActive(true);

        // Lock Player Movement (Optional, but recommended)
        // PlayerMovement.LocalPlayer.SetInput(false);

        currentMinigame = Instantiate(prefab, minigameContainer);

        // Inject the reference so the minigame knows who to call back
        var logic = currentMinigame.GetComponent<BaseMinigameLogic>();
        if (logic != null) logic.Setup(this);
    }

    public void OnMinigameComplete()
    {
        if (currentTask != null)
        {
            currentTask.CompleteTask();
        }
        CloseTaskUI();
    }

    public void CloseTaskUI()
    {
        if (currentMinigame != null) Destroy(currentMinigame);
        backgroundBlocker.SetActive(false);
        currentTask = null;

        // Unlock Player Movement
        // PlayerMovement.LocalPlayer.SetInput(true);
    }
}