using UnityEngine;
using UnityEngine.UI;

public class ClickTask : BaseMinigameLogic
{
    public Button mashButton;
    public Slider progressBar;
    public int targetClicks = 10;
    private int currentClicks = 0;

    void Start()
    {
        mashButton.onClick.AddListener(OnClick);
        progressBar.value = 0;
    }

    void OnClick()
    {
        currentClicks++;
        progressBar.value = (float)currentClicks / targetClicks;

        if (currentClicks >= targetClicks)
        {
            Win();
        }
    }
}