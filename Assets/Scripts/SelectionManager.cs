using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.AI;
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

    [Tooltip("Maximum time between two clicks on the same unit for squad selection.")]
    public float doubleClickThreshold = 0.35f;

    [Header("Formation Movement")]
    [Tooltip("Distance between unit destinations when moving multiple selected units.")]
    [Min(0.5f)] public float formationSpacing = 2.2f;

    [Tooltip("How far Unity may search for a nearby valid NavMesh point for each formation slot.")]
    [Min(0.1f)] public float formationNavMeshSampleRadius = 2.5f;

    [Tooltip("If enabled, formations rotate to face the direction the group is moving.")]
    public bool orientFormationToMovement = true;

    [Header("Unit Rosters")]
    public List<SelectableUnit> allUnits = new List<SelectableUnit>();
    public List<SelectableUnit> selectedUnits = new List<SelectableUnit>();

    private Vector2 startMousePos;
    private bool isDragging = false;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private SelectableUnit lastClickedUnit;
    private float lastClickTime = -10f;

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
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            ClearSelection();
            lastClickedUnit = null;
            Debug.Log("RAYCAST MISSED: The mouse did not hit any physical colliders.");
            return;
        }

        Debug.Log($"RAYCAST HIT: Object = {hit.collider.gameObject.name}, Tag = {hit.collider.tag}");

        SelectableUnit clickedUnit = hit.collider.GetComponentInParent<SelectableUnit>();
        if (clickedUnit == null)
        {
            ClearSelection();
            lastClickedUnit = null;
            Debug.LogWarning("SELECTION FAILED: No SelectableUnit script found on this object or its parent.");
            return;
        }

        string validTag = GetValidSelectionTag();
        Debug.Log($"FACTION CHECK: Player needs '{validTag}'. Clicked unit is '{clickedUnit.gameObject.tag}'.");

        if (!clickedUnit.gameObject.CompareTag(validTag))
        {
            ClearSelection();
            lastClickedUnit = null;
            Debug.LogWarning("SELECTION FAILED: Wrong faction tag.");
            return;
        }

        bool isDoubleClick = clickedUnit == lastClickedUnit && Time.unscaledTime - lastClickTime <= doubleClickThreshold;
        lastClickedUnit = clickedUnit;
        lastClickTime = Time.unscaledTime;

        if (isDoubleClick && TrySelectSquad(clickedUnit))
        {
            return;
        }

        ClearSelection();
        AddUnitToSelection(clickedUnit);
        Debug.Log("SELECTION SUCCESS.");
    }

    private bool TrySelectSquad(SelectableUnit clickedUnit)
    {
        SquadMember squadMember = clickedUnit.GetComponent<SquadMember>();
        if (squadMember == null || squadMember.Squad == null)
        {
            return false;
        }

        ClearSelection();
        string validTag = GetValidSelectionTag();

        foreach (GameObject memberObject in squadMember.Squad.Members)
        {
            if (memberObject == null || !memberObject.CompareTag(validTag)) continue;

            SelectableUnit member = memberObject.GetComponent<SelectableUnit>();
            if (member != null)
            {
                AddUnitToSelection(member);
            }
        }

        if (selectedUnits.Count > 0)
        {
            Debug.Log($"SQUAD SELECTION: {squadMember.Squad.DisplayName} selected ({selectedUnits.Count} units).");
            return true;
        }

        return false;
    }

    private void AddUnitToSelection(SelectableUnit unit)
    {
        if (unit == null || selectedUnits.Contains(unit)) return;

        selectedUnits.Add(unit);
        unit.SetSelected(true);

        RangeIndicator rangeIndicator = unit.GetComponent<RangeIndicator>();
        if (rangeIndicator != null) rangeIndicator.Show();
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
                AddUnitToSelection(unit);
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

        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        if (waypointPrefab != null)
        {
            Instantiate(waypointPrefab, hit.point + new Vector3(0, 0.1f, 0), Quaternion.Euler(90, 0, 0));
        }

        List<SelectableUnit> validUnits = new List<SelectableUnit>();
        foreach (SelectableUnit unit in selectedUnits)
        {
            if (unit != null && unit.GetComponent<AutonomousUnit>() != null)
            {
                validUnits.Add(unit);
            }
        }

        if (validUnits.Count == 0) return;

        if (validUnits.Count == 1)
        {
            AutonomousUnit singleUnitAI = validUnits[0].GetComponent<AutonomousUnit>();
            singleUnitAI.MoveToDirectOrder(hit.point);
            return;
        }

        Vector3 groupCenter = CalculateGroupCenter(validUnits);
        Vector3 forward = hit.point - groupCenter;
        forward.y = 0f;

        if (!orientFormationToMovement || forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        int columns = Mathf.CeilToInt(Mathf.Sqrt(validUnits.Count));
        int rows = Mathf.CeilToInt(validUnits.Count / (float)columns);

        for (int i = 0; i < validUnits.Count; i++)
        {
            int row = i / columns;
            int column = i % columns;

            float horizontalOffset = (column - (columns - 1) * 0.5f) * formationSpacing;
            float depthOffset = (row - (rows - 1) * 0.5f) * formationSpacing;

            Vector3 desiredPosition = hit.point + right * horizontalOffset + forward * depthOffset;
            Vector3 finalPosition = GetNearestNavMeshPoint(desiredPosition, hit.point);

            AutonomousUnit ai = validUnits[i].GetComponent<AutonomousUnit>();
            ai.MoveToDirectOrder(finalPosition);
        }

        Debug.Log($"FORMATION MOVE: {validUnits.Count} units ordered in a {rows}x{columns} formation with {formationSpacing:0.0}m spacing.");
    }

    private Vector3 CalculateGroupCenter(List<SelectableUnit> units)
    {
        Vector3 total = Vector3.zero;
        int count = 0;

        foreach (SelectableUnit unit in units)
        {
            if (unit == null) continue;
            total += unit.transform.position;
            count++;
        }

        return count > 0 ? total / count : Vector3.zero;
    }

    private Vector3 GetNearestNavMeshPoint(Vector3 desiredPosition, Vector3 fallbackPosition)
    {
        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit navHit, formationNavMeshSampleRadius, NavMesh.AllAreas))
        {
            return navHit.position;
        }

        if (NavMesh.SamplePosition(fallbackPosition, out NavMeshHit fallbackHit, formationNavMeshSampleRadius, NavMesh.AllAreas))
        {
            return fallbackHit.position;
        }

        return desiredPosition;
    }
}
