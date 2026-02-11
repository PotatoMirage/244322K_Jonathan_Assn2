using UnityEngine;

public class CubeColour : MonoBehaviour
{
    public PlayerMovement script;

    Color[] ColorCube = new[] {
        new Color(1.0f, 0f, 0f),
        new Color(0f, 1.0f, 0f),
        new Color(0f, 0f, 1.0f),
        new Color(1.0f, 1.0f, 0f)
    };

    void Start()
    {

        script = transform.parent.GetComponent<PlayerMovement>();

        if (script != null)
        {
            ApplyColor(script.PlayerNum.Value);
            script.PlayerNum.OnValueChanged += OnColorChanged;
        }
    }
    void OnColorChanged(int previousValue, int newValue)
    {
        ApplyColor(newValue);
    }

    void ApplyColor(int playerIndex)
    {
        int safeIndex = playerIndex % ColorCube.Length;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.material.color = ColorCube[safeIndex];
        }
    }

    void OnDestroy()
    {
        if (script != null)
        {
            script.PlayerNum.OnValueChanged -= OnColorChanged;
        }
    }
}
