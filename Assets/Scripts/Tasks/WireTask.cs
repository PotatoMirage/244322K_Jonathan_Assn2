using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class WireTask : BaseMinigameLogic
{
    [Header("Wire Configuration")]
    public List<WireEndpoint> leftWires;
    public List<WireEndpoint> rightWires;
    public GameObject linePrefab;
    public Transform lineParent;

    private WireEndpoint currentDragStart;
    private GameObject currentLine;
    private int solvedWires = 0;

    private void Start()
    {
        // Randomize Colors
        List<Color> colors = new() { Color.red, Color.blue, Color.yellow, Color.green };
        Shuffle(colors);

        for (int i = 0; i < 4; i++)
        {
            leftWires[i].SetColor(colors[i]);
            leftWires[i].task = this;
            rightWires[i].SetColor(colors[i]);
            rightWires[i].task = this;
        }

    }

    public void OnWireDragStart(WireEndpoint start)
    {
        if (currentLine != null) Destroy(currentLine);
        currentDragStart = start;

        currentLine = Instantiate(linePrefab, lineParent);
        currentLine.GetComponent<Image>().color = start.wireColor;
    }

    public void OnWireDragEnd(WireEndpoint end)
    {
        if (currentDragStart != null && end != currentDragStart)
        {
            if (currentDragStart.wireColor == end.wireColor)
            {
                solvedWires++;

                if (solvedWires >= 4)
                {
                    Invoke(nameof(Win), 0.5f);
                }
            }
            else
            {
                Destroy(currentLine);
            }
        }
        else
        {
            Destroy(currentLine);
        }
        currentDragStart = null;
        currentLine = null;
    }

    private void Update()
    {
        if (currentDragStart != null && currentLine != null)
        {
            Vector3 startPos = currentDragStart.transform.position;
            Vector3 endPos = Input.mousePosition;

            Vector3 dir = endPos - startPos;
            float dist = dir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            RectTransform rt = currentLine.GetComponent<RectTransform>();
            rt.position = startPos + (0.5f * dist * dir.normalized);
            rt.rotation = Quaternion.Euler(0, 0, angle);
            rt.sizeDelta = new Vector2(dist, 10f);
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}