using UnityEngine;

public class SelectableUnit : MonoBehaviour
{
    [Header("Selection Visual")]
    [Tooltip("Legacy selection marker. It is hidden at runtime and replaced by a simple ground ring so it cannot cover squad labels.")]
    public GameObject selectionIndicator;

    [SerializeField] private float selectionRingRadius = 0.75f;
    [SerializeField] private float selectionRingWidth = 0.08f;
    [SerializeField] private float selectionRingHeight = 0.06f;
    [SerializeField] private int selectionRingSegments = 40;

    public bool isSelected = false;

    private LineRenderer selectionRing;

    void Start()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.allUnits.Add(this);
        }

        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(false);
        }

        CreateSelectionRing();
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(false);
        }

        if (selectionRing != null)
        {
            selectionRing.enabled = isSelected;
        }
    }

    private void CreateSelectionRing()
    {
        GameObject ringObject = new GameObject("SelectionRing");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = new Vector3(0f, selectionRingHeight, 0f);

        selectionRing = ringObject.AddComponent<LineRenderer>();
        selectionRing.useWorldSpace = false;
        selectionRing.loop = true;
        selectionRing.positionCount = Mathf.Max(12, selectionRingSegments);
        selectionRing.startWidth = selectionRingWidth;
        selectionRing.endWidth = selectionRingWidth;
        selectionRing.alignment = LineAlignment.TransformZ;
        selectionRing.textureMode = LineTextureMode.Stretch;
        selectionRing.numCornerVertices = 2;
        selectionRing.numCapVertices = 2;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            selectionRing.material = new Material(shader);
        }

        selectionRing.startColor = Color.white;
        selectionRing.endColor = Color.white;

        int segments = selectionRing.positionCount;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * selectionRingRadius;
            float z = Mathf.Sin(angle) * selectionRingRadius;
            selectionRing.SetPosition(i, new Vector3(x, 0f, z));
        }

        selectionRing.enabled = false;
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.allUnits.Remove(this);
            SelectionManager.Instance.selectedUnits.Remove(this);
        }
    }
}
