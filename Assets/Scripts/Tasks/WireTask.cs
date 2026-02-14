using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class WireTask : BaseMinigameLogic
{
    [Header("Wire Configuration")]
    public List<WireEndpoint> leftWires;
    public List<WireEndpoint> rightWires;
    public GameObject linePrefab; // A simple UI Image stretched to look like a line
    public Transform lineParent;

    private WireEndpoint currentDragStart;
    private GameObject currentLine;
    private int solvedWires = 0;

    private void Start()
    {
        // Randomize Colors
        List<Color> colors = new List<Color> { Color.red, Color.blue, Color.yellow, Color.green };
        Shuffle(colors);

        for (int i = 0; i < 4; i++)
        {
            leftWires[i].SetColor(colors[i]);
            leftWires[i].task = this;

            // For the right side, we shuffle the order of colors
            // (In a real implementation, you'd map them to ensure a match exists)
            // For simplicity here, we assume 1:1 mapping is handled by the prefab setup or a more complex shuffle logic
            rightWires[i].SetColor(colors[i]);
            rightWires[i].task = this;
        }

        // Simple Shuffle for the right side positions physically would go here
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
                // Correct Match!
                solvedWires++;
                // Lock the wire in place (Visual logic here)

                if (solvedWires >= 4)
                {
                    Invoke(nameof(Win), 0.5f);
                }
            }
            else
            {
                // Wrong Match
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
        // Update the dragging line visual to follow mouse
        if (currentDragStart != null && currentLine != null)
        {
            Vector3 startPos = currentDragStart.transform.position;
            Vector3 endPos = Input.mousePosition;

            Vector3 dir = endPos - startPos;
            float dist = dir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            RectTransform rt = currentLine.GetComponent<RectTransform>();
            // Set position to the midpoint
            rt.position = startPos + (dir.normalized * dist * 0.5f);
            // Rotate to face the mouse
            rt.rotation = Quaternion.Euler(0, 0, angle);
            // Stretch width to distance, keep height fixed (thickness)
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
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}