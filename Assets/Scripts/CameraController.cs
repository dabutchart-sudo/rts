using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

public class RTSCamera : MonoBehaviour
{
    [Header("Keyboard Panning (Mac / PC)")]
    public float keyboardPanSpeed = 20f;

    [Header("Trackpad & Touch Panning")]
    [Tooltip("Sensitivity for Mac two-finger trackpad swipe")]
    public float trackpadPanSpeed = 0.05f;

    [Tooltip("Sensitivity for mobile two-finger drag")]
    public float mobileDragSpeed = 0.5f;

    [Header("Zoom Settings")]
    public float keyboardZoomSpeed = 15f;
    public float mobilePinchZoomSpeed = 0.05f;

    [Tooltip("How close to the ground the camera can get")]
    public float minZoomHeight = 5f;

    [Tooltip("How high up into the sky the camera can go")]
    public float maxZoomHeight = 120f;

    [Header("Match Start Focus")]
    [Tooltip("Automatically centres the camera on the selected faction's Sector 1 base when the match begins.")]
    public bool focusFactionBaseOnMatchStart = true;

    [Header("Tactical Overview")]
    [Tooltip("Height used by the tactical overview toggle.")]
    public float tacticalOverviewHeight = 110f;

    private Faction lastObservedFaction = Faction.None;
    private bool tacticalOverviewActive = false;
    private Vector3 positionBeforeOverview;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        HandleFactionStartFocus();

        if (GameManager.Instance.playerFaction == Faction.None) return;

        HandleKeyboardPan();
        HandleKeyboardZoom();
        HandleTacticalOverviewToggle();
        HandleTrackpadPan();
        HandleMobileTouch();
    }

    void HandleFactionStartFocus()
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

    void HandleKeyboardPan()
    {
        if (Keyboard.current == null) return;

        float x = 0f;
        float z = 0f;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) z += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) z -= 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;

        if (x != 0f || z != 0f)
        {
            tacticalOverviewActive = false;
            Vector3 move = new Vector3(x, 0f, z).normalized * keyboardPanSpeed * Time.deltaTime;
            transform.position += move;
        }
    }

    void HandleKeyboardZoom()
    {
        if (Keyboard.current == null) return;

        float zoomInput = 0f;

        // E to zoom in, Q to zoom out.
        if (Keyboard.current.eKey.isPressed) zoomInput += 1f;
        if (Keyboard.current.qKey.isPressed) zoomInput -= 1f;

        if (zoomInput != 0f)
        {
            tacticalOverviewActive = false;
            ApplyZoom(zoomInput * keyboardZoomSpeed * Time.deltaTime);
        }
    }

    void HandleTacticalOverviewToggle()
    {
        if (Keyboard.current == null || !Keyboard.current.zKey.wasPressedThisFrame) return;

        if (!tacticalOverviewActive)
        {
            positionBeforeOverview = transform.position;
            tacticalOverviewActive = true;
            SetHeightKeepingGroundFocus(Mathf.Clamp(tacticalOverviewHeight, minZoomHeight, maxZoomHeight));
        }
        else
        {
            transform.position = positionBeforeOverview;
            tacticalOverviewActive = false;
        }
    }

    void HandleTrackpadPan()
    {
        if (Mouse.current == null) return;

        // Unity reads Mac trackpad two-finger gestures as scroll deltas.
        Vector2 scrollDelta = Mouse.current.scroll.ReadValue();

        if (scrollDelta != Vector2.zero)
        {
            tacticalOverviewActive = false;
            Vector3 move = new Vector3(-scrollDelta.x, 0f, -scrollDelta.y) * trackpadPanSpeed;
            transform.position += move;
        }
    }

    void HandleMobileTouch()
    {
        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count != 2) return;

        tacticalOverviewActive = false;

        var touch1 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
        var touch2 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[1];

        Vector2 averageDelta = (touch1.delta + touch2.delta) / 2f;
        Vector3 move = new Vector3(-averageDelta.x, 0f, -averageDelta.y) * mobileDragSpeed * Time.deltaTime;
        transform.position += move;

        Vector2 touch1PrevPos = touch1.screenPosition - touch1.delta;
        Vector2 touch2PrevPos = touch2.screenPosition - touch2.delta;

        float prevDistance = (touch1PrevPos - touch2PrevPos).magnitude;
        float currentDistance = (touch1.screenPosition - touch2.screenPosition).magnitude;
        float pinchDelta = currentDistance - prevDistance;

        if (Mathf.Abs(pinchDelta) > 0f)
        {
            ApplyZoom(pinchDelta * mobilePinchZoomSpeed);
        }
    }

    void ApplyZoom(float zoomAmount)
    {
        Vector3 projectedPos = transform.position + (transform.forward * zoomAmount);

        if (projectedPos.y >= minZoomHeight && projectedPos.y <= maxZoomHeight)
        {
            transform.position = projectedPos;
        }
    }

    void SetHeightKeepingGroundFocus(float targetHeight)
    {
        if (!TryGetGroundFocus(out Vector3 groundFocus))
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

    bool TryGetGroundFocus(out Vector3 groundFocus)
    {
        Vector3 forward = transform.forward;

        if (forward.y >= -0.001f)
        {
            groundFocus = Vector3.zero;
            return false;
        }

        float distance = -transform.position.y / forward.y;
        if (distance <= 0f)
        {
            groundFocus = Vector3.zero;
            return false;
        }

        groundFocus = transform.position + forward * distance;
        groundFocus.y = 0f;
        return true;
    }

    void FocusGroundPoint(Vector3 target)
    {
        Vector3 forward = transform.forward;

        if (forward.y < -0.001f)
        {
            float distance = (target.y - transform.position.y) / forward.y;

            if (distance > 0f)
            {
                Vector3 desiredPosition = target - forward * distance;
                desiredPosition.y = Mathf.Clamp(transform.position.y, minZoomHeight, maxZoomHeight);
                transform.position = desiredPosition;
                return;
            }
        }

        Vector3 fallbackPosition = transform.position;
        fallbackPosition.x = target.x;
        fallbackPosition.z = target.z;
        fallbackPosition.y = Mathf.Clamp(fallbackPosition.y, minZoomHeight, maxZoomHeight);
        transform.position = fallbackPosition;
    }
}
