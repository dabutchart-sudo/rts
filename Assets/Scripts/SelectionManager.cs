using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
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
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

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
            selectionBox.gameObject.SetActive(false);
        }

        if (ControlModeManager.Instance != null)
        {
            ControlModeManager.Instance.ModeChanged += HandleControlModeChanged;
        }
    }

    void OnDestroy()
    {
        if (ControlModeManager.Instance != null)
        {
            ControlModeManager.Instance.ModeChanged -= HandleControlModeChanged;
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerFaction == Faction.None) return;
        if (Mouse.current == null) return;

        if (ControlModeManager.Instance != null && !ControlModeManager.Instance.CanPlayerSelectUnits())
        {
            if (selectedUnits.Count > 0) ClearSelection();
            return;
        }

        // Only block RTS input when the pointer is over an interactive UI control
        // such as a Button, Toggle, Slider, etc. Decorative/full-screen UI graphics
        // must not prevent selecting units in the world.
        if (IsPointerOverInteractiveUI())
        {
            if (isDragging)
            {
                isDragging = false;
                if (selectionBox != null) selectionBox.gameObject.SetActive(false);
            }
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            startMousePos = Mouse.current.position.ReadValue();
            isDragging = true;
        }

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

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            isDragging = false;
            Vector2 currentMousePos = Mouse.current.position.ReadValue();

            if (selectionBox != null)
            {
                selectionBox.gameObject.SetActive(false);
            }

            if (Vector2.Distance(startMousePos, currentMousePos) > dragThreshold)
            {
                SelectUnitsInBox();
            }
            else
            {
                SelectSingleUnit();
            }
        }

        if (Mouse.current.rightButton.wasPressedThisFrame && selectedUnits.Count > 0)
        {
            if (ControlModeManager.Instance == null || ControlModeManager.Instance.CanPlayerIssueOrders())
            {
                IssueMoveOrder();
            }
        }
    }

    private bool IsPointerOverInteractiveUI()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || Mouse.current == null) return false;

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Mouse.current.position.ReadValue()
        };

        uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, uiRaycastResults);

        foreach (RaycastResult result in uiRaycastResults)
        {
            if (result.gameObject == null) continue;

            Selectable interactiveControl = result.gameObject.GetComponentInParent<Selectable>();
            if (interactiveControl != null && interactiveControl.IsActive() && interactiveControl.IsInteractable())
            {
                return true;
            }
        }

        return false;
    }

    private void HandleControlModeChanged(ControlMode mode)
    {
        if (mode == ControlMode.Auto)
        {
            ClearSelection();
        }

        foreach (SelectableUnit unit in allUnits)
        {
            if (unit == null) continue;
            AutonomousUnit autonomousUnit = unit.GetComponent<AutonomousUnit>();
            if (autonomousUnit != null) autonomousUnit.RefreshControlMode();
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
            Debug.Log($"RAYCAST HIT: Object = {hit.collider.gameObject.name}, Tag = {hit.collider.tag}");

            SelectableUnit clickedUnit = hit.collider.GetComponentInParent<SelectableUnit>();

            if (clickedUnit == null)
            {
                Debug.LogWarning("SELECTION FAILED: No SelectableUnit script found on this object or its parent.");
                return;
            }

            string validTag = GetValidSelectionTag();
            Debug.Log($"FACTION CHECK: Player needs '{validTag}'. Clicked unit is '{clickedUnit.gameObject.tag}'.");

            if (clickedUnit.gameObject.CompareTag(validTag))
            {
                selectedUnits.Add(clickedUnit);
                clickedUnit.SetSelected(true);

                RangeIndicator rangeIndicator = clickedUnit.GetComponent<RangeIndicator>();
                if (rangeIndicator != null) rangeIndicator.Show();

                Debug.Log("SELECTION SUCCESS.");
            }
            else
            {
                Debug.LogWarning("SELECTION FAILED: Wrong faction tag.");
            }
        }
        else
        {
            Debug.Log("RAYCAST MISSED: The mouse did not hit any physical colliders.");
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