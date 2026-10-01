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
    public float maxZoomHeight = 35f;

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
        if (!MapSession.allowLookAround && (GameManager.Instance == null || GameManager.Instance.playerFaction == Faction.None)) return;

        HandleKeyboardPan();
        HandleKeyboardZoom();
        HandleTrackpadPan();
        HandleMobileTouch();
    }

    void HandleKeyboardPan()
    {
        if (Keyboard.current == null) return;

        float x = 0;
        float z = 0;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) z += 1;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) z -= 1;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1;

        if (x != 0 || z != 0)
        {
            Vector3 move = new Vector3(x, 0, z).normalized * keyboardPanSpeed * Time.deltaTime;
            transform.position += move;
        }
    }

    void HandleKeyboardZoom()
    {
        if (Keyboard.current == null) return;

        float zoomInput = 0;
        
        // E to zoom in (move forward), Q to zoom out (move backward)
        if (Keyboard.current.eKey.isPressed) zoomInput += 1; 
        if (Keyboard.current.qKey.isPressed) zoomInput -= 1; 

        if (zoomInput != 0)
        {
            ApplyZoom(zoomInput * keyboardZoomSpeed * Time.deltaTime);
        }
    }

    void HandleTrackpadPan()
    {
        if (Mouse.current == null) return;

        // Unity reads Mac trackpad two-finger gestures as scroll deltas
        Vector2 scrollDelta = Mouse.current.scroll.ReadValue();

        if (scrollDelta != Vector2.zero)
        {
            if (Mathf.Abs(scrollDelta.y) >= Mathf.Abs(scrollDelta.x))
            {
                float zoom = scrollDelta.y;
                if (Mathf.Abs(zoom) > 10f) zoom *= 0.025f;
                else zoom *= 0.4f;
                ApplyZoom(zoom);
            }
            else
            {
                Vector3 move = new Vector3(-scrollDelta.x, 0, 0) * trackpadPanSpeed;
                transform.position += move;
            }
        }
    }

    void HandleMobileTouch()
    {
        // Strict two-finger touch for mobile devices
        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 2)
        {
            var touch1 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
            var touch2 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[1];

            // 1. HANDLE PANNING (Average direction of both fingers)
            Vector2 averageDelta = (touch1.delta + touch2.delta) / 2f;
            Vector3 move = new Vector3(-averageDelta.x, 0, -averageDelta.y) * mobileDragSpeed * Time.deltaTime;
            transform.position += move;

            // 2. HANDLE ZOOMING (Pinch distance)
            // Calculate where the fingers were in the previous frame
            Vector2 touch1PrevPos = touch1.screenPosition - touch1.delta;
            Vector2 touch2PrevPos = touch2.screenPosition - touch2.delta;

            float prevDistance = (touch1PrevPos - touch2PrevPos).magnitude;
            float currentDistance = (touch1.screenPosition - touch2.screenPosition).magnitude;

            // If current distance is larger, we spread fingers (zoom in). If smaller, we pinched (zoom out).
            float pinchDelta = currentDistance - prevDistance;
            
            if (Mathf.Abs(pinchDelta) > 0)
            {
                ApplyZoom(pinchDelta * mobilePinchZoomSpeed);
            }
        }
    }

    void ApplyZoom(float zoomAmount)
    {
        // Move the camera along the direction it is currently facing
        Vector3 projectedPos = transform.position + (transform.forward * zoomAmount);

        float nextHeight = projectedPos.y;
        bool zoomingIn = nextHeight < transform.position.y;
        if (zoomingIn && nextHeight < minZoomHeight) return;
        if (!zoomingIn && nextHeight > maxZoomHeight) return;
        transform.position = projectedPos;
    }
}