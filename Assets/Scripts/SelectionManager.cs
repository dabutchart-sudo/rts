using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance;

    [Header("Visuals")]
    public GameObject waypointPrefab; 

    [Header("UI References")]
    public RectTransform selectionBox;
    private Canvas parentCanvas; 

    [Header("Selection Settings")]
    [Tooltip("How many pixels the mouse must move before a click turns into a drag box.")]
    public float dragThreshold = 10f;

    [Header("Unit Rosters")]
    public List<SelectableUnit> allUnits = new List<SelectableUnit>();
    public List<SelectableUnit> selectedUnits = new List<SelectableUnit>();

    private Vector2 startMousePos;
    private bool isDragging = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (selectionBox != null)
        {
            parentCanvas = selectionBox.GetComponentInParent<Canvas>();
            Image boxImage = selectionBox.GetComponent<Image>();
            if (boxImage != null) boxImage.enabled = true;
            
            // Ensure the box is hidden at the start
            selectionBox.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerFaction == Faction.None) return;
        if (Mouse.current == null) return;

        // Block clicks if pointer is over UI elements
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            if (isDragging)
            {
                isDragging = false;
                if (selectionBox != null) selectionBox.gameObject.SetActive(false);
            }
            return;
        }

        // 1. Mouse Button Down: Record starting position
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            startMousePos = Mouse.current.position.ReadValue();
            isDragging = true;
        }

        // 2. Mouse Button Held: Check if we passed the drag threshold
        if (Mouse.current.leftButton.isPressed && isDragging)
        {
            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            
            if (Vector2.Distance(startMousePos, currentMousePos) > dragThreshold)
            {
                if (selectionBox != null && !selectionBox.gameObject.activeSelf)
                {
                    selectionBox.gameObject.SetActive(true);
                }
                UpdateSelectionBox(currentMousePos);
            }
        }

        // 3. Mouse Button Up: Decide between Click or Box Selection
        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            isDragging = false;
            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            
            if (selectionBox != null)
            {
                selectionBox.gameObject.SetActive(false);
            }

            // If we dragged past the threshold, do a box selection. Otherwise, do a single click.
            if (Vector2.Distance(startMousePos, currentMousePos) > dragThreshold)
            {
                SelectUnitsInBox();
            }
            else
            {
                SelectSingleUnit();
            }
        }

        // 4. Right Mouse Button: Issue Move Order
        if (Mouse.current.rightButton.wasPressedThisFrame && selectedUnits.Count > 0)
        {
            IssueMoveOrder();
        }
    }

    void UpdateSelectionBox(Vector2 currentMousePos)
    {
        if (selectionBox == null) return;

        float scale = parentCanvas != null ? parentCanvas.scaleFactor : 1f;
        float width = (currentMousePos.x - startMousePos.x) / scale;
        float height = (currentMousePos.y - startMousePos.y) / scale;

        selectionBox.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
        selectionBox.anchoredPosition = new Vector2(
            Mathf.Min(startMousePos.x, currentMousePos.x) / scale,
            Mathf.Min(startMousePos.y, currentMousePos.y) / scale
        );
    }

    // Determine which tag is valid for selection based on the chosen faction
    private string GetValidSelectionTag()
    {
        if (GameManager.Instance != null && GameManager.Instance.playerFaction == Faction.Defender)
        {
            return "Defender";
        }
        return "Attacker";
    }

void SelectSingleUnit()
    {
        ClearSelection();

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // 1. Tell us exactly what the laser hit
            Debug.Log($"🖱️ RAYCAST HIT: Object = {hit.collider.gameObject.name}, Tag = {hit.collider.tag}");

            // 2. Use GetComponentInParent in case the raycast hits a child mesh instead of the root!
            SelectableUnit clickedUnit = hit.collider.GetComponentInParent<SelectableUnit>(); 
            
            if (clickedUnit == null)
            {
                Debug.LogWarning("❌ SELECTION FAILED: No 'SelectableUnit' script found on this object or its parent!");
                return;
            }

            // 3. Verify the tags
            string validTag = GetValidSelectionTag();
            Debug.Log($"🛡️ FACTION CHECK: Player needs '{validTag}'. Clicked unit is '{clickedUnit.gameObject.tag}'.");

            if (clickedUnit.gameObject.CompareTag(validTag))
            {
                selectedUnits.Add(clickedUnit);
                clickedUnit.SetSelected(true);
                
                RangeIndicator rangeIndicator = clickedUnit.GetComponent<RangeIndicator>();
                if (rangeIndicator != null) rangeIndicator.Show();
                
                Debug.Log("✅ SELECTION SUCCESS!");
            }
            else
            {
                Debug.LogWarning("❌ SELECTION FAILED: Wrong faction tag!");
            }
        }
        else
        {
            Debug.Log("💨 RAYCAST MISSED: The mouse didn't hit any physical colliders.");
        }
    }

    void SelectUnitsInBox()
    {
        ClearSelection();

        Vector2 currentMousePos = Mouse.current.position.ReadValue();
        float width = currentMousePos.x - startMousePos.x;
        float height = currentMousePos.y - startMousePos.y;
        Rect selectionRect = new Rect(startMousePos.x, startMousePos.y, width, height);

        if (width < 0) { selectionRect.x += width; selectionRect.width = -width; }
        if (height < 0) { selectionRect.y += height; selectionRect.height = -height; }

        string validTag = GetValidSelectionTag();

        foreach (SelectableUnit unit in allUnits)
        {
            if (unit == null) continue;
            
            // Skip the unit if its tag doesn't match the player's chosen faction
            if (!unit.gameObject.CompareTag(validTag)) continue;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(unit.transform.position);

            if (screenPos.z > 0 && selectionRect.Contains(new Vector2(screenPos.x, screenPos.y)))
            {
                selectedUnits.Add(unit);
                unit.SetSelected(true);
                
                RangeIndicator rangeIndicator = unit.GetComponent<RangeIndicator>();
                if (rangeIndicator != null) rangeIndicator.Show();
            }
        }
    }

    void ClearSelection()
    {
        foreach (SelectableUnit unit in selectedUnits)
        {
            if (unit != null)
            {
                unit.SetSelected(false);
                RangeIndicator rangeIndicator = unit.GetComponent<RangeIndicator>();
                if (rangeIndicator != null) rangeIndicator.Hide();
            }
        }
        selectedUnits.Clear();
    }

    void IssueMoveOrder()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (waypointPrefab != null)
            {
                Instantiate(waypointPrefab, hit.point + new Vector3(0, 0.1f, 0), Quaternion.Euler(90, 0, 0));
            }

            foreach (SelectableUnit unit in selectedUnits)
            {
                if (unit != null)
                {
                    AutonomousUnit ai = unit.GetComponent<AutonomousUnit>();
                    if (ai != null) ai.MoveToDirectOrder(hit.point);
                }
            }
        }
    }
}