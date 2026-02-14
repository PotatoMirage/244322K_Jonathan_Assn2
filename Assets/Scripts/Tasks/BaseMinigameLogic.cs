using UnityEngine;
using UnityEngine.UI;

public abstract class BaseMinigameLogic : MonoBehaviour
{
    protected TaskUIManager manager;
    public void Setup(TaskUIManager uiManager) { manager = uiManager; }
    protected void Win() { manager.OnMinigameComplete(); }
    protected void Exit() { manager.CloseTaskUI(); }
}