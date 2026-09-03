using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RangeIndicator : MonoBehaviour
{
    [Header("Range Settings")]
    [Tooltip("This should match the attack range of this specific unit.")]
    public float weaponRange = 10f; 
    
    [Header("Visual Settings")]
    [Tooltip("How many points make up the circle. Higher = smoother.")]
    public int segments = 50;
    public float lineWidth = 0.15f;
    [Tooltip("The color of the ring. Adjust the Alpha (A) for translucency.")]
    public Color circleColor = new Color(0f, 1f, 0f, 0.4f); // Translucent green by default

    private LineRenderer line;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        SetupLineRenderer();
        CreatePoints();
        
        // Hide the circle by default so the screen isn't cluttered
        Hide(); 
    }

    void SetupLineRenderer()
    {
        line.useWorldSpace = false; // Moves with the unit
        line.loop = true; // Closes the circle automatically
        line.positionCount = segments;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        
        // Using Unity's default sprite shader allows for easy transparency without needing custom materials
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = circleColor;
        line.endColor = circleColor;
    }

    void CreatePoints()
    {
        // Mathematically plot points in a circle around the unit
        for (int i = 0; i < segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * 360f / segments);
            float x = Mathf.Sin(rad) * weaponRange;
            float z = Mathf.Cos(rad) * weaponRange;

            // Y is set to 0.2f so the line floats just above the ground/navmesh
            line.SetPosition(i, new Vector3(x, 0.2f, z));
        }
    }

    // Call this method when the player selects the unit
    public void Show()
    {
        if (line != null)
        {
            line.enabled = true;
        }
    }

    // Call this method when the player deselects the unit
    public void Hide()
    {
        if (line != null)
        {
            line.enabled = false;
        }
    }
}