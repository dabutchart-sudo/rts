using UnityEngine;

public class SelectableUnit : MonoBehaviour
{
    [Header("Visuals")]
    public GameObject selectionIndicator; // A ring or quad at their feet
    
    public bool isSelected = false;

    void Start()
    {
        // Register this unit with the manager when it spawns
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.allUnits.Add(this);
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(isSelected);
        }
    }

    void OnDestroy()
    {
        // Remove this unit from the roster when it dies
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.allUnits.Remove(this);
            SelectionManager.Instance.selectedUnits.Remove(this);
        }
    }
}