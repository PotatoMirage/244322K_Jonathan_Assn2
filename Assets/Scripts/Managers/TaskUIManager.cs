using UnityEngine;

public class TaskUIManager : MonoBehaviour
{
    public static TaskUIManager Instance;
    public Transform minigameContainer;
    public GameObject backgroundBlocker;

    private GameObject currentMinigame;
    private TaskInteractable currentTask;

    private void Awake() { Instance = this; }

    public void OpenTaskUI(GameObject prefab, TaskInteractable task)
    {
        CloseTaskUI();

        currentTask = task;
        backgroundBlocker.SetActive(true);

        currentMinigame = Instantiate(prefab, minigameContainer);

        BaseMinigameLogic logic = currentMinigame.GetComponent<BaseMinigameLogic>();
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

    }
}