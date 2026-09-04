using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    [Header("Movement Speeds")]
    public float moveSpeed = 25f;
    public float zoomSpeed = 20f;

    [Header("Height / Zoom Limits")]
    public float minHeight = 10f;
    public float maxHeight = 120f;

    [Header("Match Start Focus")]
    [Tooltip("Automatically centres the camera on the selected faction's starting base when a match begins.")]
    public bool focusFactionBaseOnMatchStart = true;

    [Header("Tactical Overview")]
    [Tooltip("Press this key to toggle a high tactical overview of the battlefield.")]
    public KeyCode tacticalOverviewKey = KeyCode.Z;

    [Tooltip("Camera height used by the tactical overview. This is clamped to the normal zoom limits.")]
    public float tacticalOverviewHeight = 110f;

    private Faction lastObservedFaction = Faction.None;
    private bool tacticalOverviewActive = false;
    private float heightBeforeOverview = 35f;

    void Update()
    {
        if (GameManager.Instance == null) return;

        HandleFactionStartFocus();

        if (GameManager.Instance.playerFaction == Faction.None) return;

        // 1. Pan with WASD or Arrow Keys.
        float xInput = Input.GetAxis("Horizontal");
        float zInput = Input.GetAxis("Vertical");

        Vector3 moveDirection = new Vector3(xInput, 0f, zInput).normalized;
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);

        // 2. Trackpad pinch / mouse wheel zooming.
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0f)
        {
            tacticalOverviewActive = false;

            Vector3 pos = transform.position;
            pos.y -= scrollInput * zoomSpeed * 10f;
            pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
            transform.position = pos;
        }

        // 3. Toggle a high tactical overview for quickly reading the whole battle.
        if (Input.GetKeyDown(tacticalOverviewKey))
        {
            ToggleTacticalOverview();
        }
    }

    private void HandleFactionStartFocus()
    {
        Faction currentFaction = GameManager.Instance.playerFaction;

        if (currentFaction == Faction.None)
        {
            lastObservedFaction = Faction.None;
            return;
        }

        if (currentFaction == lastObservedFaction) return;

        lastObservedFaction = currentFaction;

        if (focusFactionBaseOnMatchStart)
        {
            FocusCurrentFactionBase();
        }
    }

    public void FocusCurrentFactionBase()
    {
        if (GameManager.Instance == null ||
            GameManager.Instance.sectors == null ||
            GameManager.Instance.sectors.Length == 0)
        {
            return;
        }

        Sector startingSector = GameManager.Instance.sectors[0];
        if (startingSector == null) return;

        BaseZone baseZone = GameManager.Instance.playerFaction == Faction.Attacker
            ? startingSector.attackerBase
            : startingSector.defenderBase;

        if (baseZone == null) return;

        Transform focusTransform = baseZone.spawnPoint != null
            ? baseZone.spawnPoint
            : baseZone.transform;

        FocusGroundPoint(focusTransform.position);
    }

    public void ToggleTacticalOverview()
    {
        tacticalOverviewActive = !tacticalOverviewActive;

        if (tacticalOverviewActive)
        {
            heightBeforeOverview = Mathf.Clamp(transform.position.y, minHeight, maxHeight);
            SetHeightKeepingGroundFocus(Mathf.Clamp(tacticalOverviewHeight, minHeight, maxHeight));
        }
        else
        {
            SetHeightKeepingGroundFocus(Mathf.Clamp(heightBeforeOverview, minHeight, maxHeight));
        }
    }

    private void SetHeightKeepingGroundFocus(float targetHeight)
    {
        Vector3 groundFocus;
        if (!TryGetGroundFocus(out groundFocus))
        {
            Vector3 fallback = transform.position;
            fallback.y = targetHeight;
            transform.position = fallback;
            return;
        }

        Vector3 pos = transform.position;
        pos.y = targetHeight;
        transform.position = pos;
        FocusGroundPoint(groundFocus);
    }

    private bool TryGetGroundFocus(out Vector3 groundFocus)
    {
        Vector3 forward = transform.forward;

        if (forward.y >= -0.001f)
        {
            groundFocus = Vector3.zero;
            return false;
        }

        float groundY = 0f;
        float distance = (groundY - transform.position.y) / forward.y;

        if (distance <= 0f)
        {
            groundFocus = Vector3.zero;
            return false;
        }

        groundFocus = transform.position + forward * distance;
        groundFocus.y = groundY;
        return true;
    }

    private void FocusGroundPoint(Vector3 target)
    {
        Vector3 forward = transform.forward;

        // For a normal angled RTS camera, place the camera so its centre ray
        // intersects the target point while preserving the existing rotation.
        if (forward.y < -0.001f)
        {
            float distance = (target.y - transform.position.y) / forward.y;

            if (distance > 0f)
            {
                Vector3 desiredPosition = target - forward * distance;
                desiredPosition.y = Mathf.Clamp(transform.position.y, minHeight, maxHeight);
                transform.position = desiredPosition;
                return;
            }
        }

        // Safe fallback for an unusual horizontal camera angle.
        Vector3 fallbackPosition = transform.position;
        fallbackPosition.x = target.x;
        fallbackPosition.z = target.z;
        fallbackPosition.y = Mathf.Clamp(fallbackPosition.y, minHeight, maxHeight);
        transform.position = fallbackPosition;
    }
}
