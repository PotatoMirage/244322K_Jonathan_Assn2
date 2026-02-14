using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WireEndpoint : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public Color wireColor;
    public WireTask task;
    public Image visual;
    public bool isLeftSide; // Set in Inspector

    public void SetColor(Color c)
    {
        wireColor = c;
        visual.color = c;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLeftSide) task.OnWireDragStart(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Required interface for Drag to work, logic handled in Parent
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // If we didn't hit a drop target, reset
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!isLeftSide) task.OnWireDragEnd(this);
    }
}