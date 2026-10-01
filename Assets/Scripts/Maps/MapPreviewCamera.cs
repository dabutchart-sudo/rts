using UnityEngine;
using UnityEngine.InputSystem;

public class MapPreviewCamera : MonoBehaviour
{
    [Header("Look around the stamped district")]
    public float keyboardPanSpeed = 16f;
    public float keyboardZoomSpeed = 12f;
    public float scrollZoomSpeed = 0.8f;
    public float minHeight = 6f;
    public float maxHeight = 55f;

    void Update()
    {
        Pan();
        ZoomFromKeys();
        ZoomFromScroll();
    }

    void Pan()
    {
        if (Keyboard.current == null) return;

        float x = 0f;
        float z = 0f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) z += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) z -= 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;

        if (x == 0f && z == 0f) return;

        Vector3 move = new Vector3(x, 0f, z).normalized * keyboardPanSpeed * Time.deltaTime;
        transform.position += move;
    }

    void ZoomFromKeys()
    {
        if (Keyboard.current == null) return;

        float zoom = 0f;
        if (Keyboard.current.eKey.isPressed) zoom += 1f;
        if (Keyboard.current.qKey.isPressed) zoom -= 1f;
        if (zoom != 0f) ApplyZoom(zoom * keyboardZoomSpeed * Time.deltaTime);
    }

    void ZoomFromScroll()
    {
        if (Mouse.current == null) return;

        float scroll = Mathf.Clamp(Mouse.current.scroll.ReadValue().y, -3f, 3f);
        if (Mathf.Abs(scroll) > 0.01f) ApplyZoom(scroll * scrollZoomSpeed);
    }

    void ApplyZoom(float amount)
    {
        Vector3 next = transform.position + transform.forward * amount;
        if (next.y < minHeight || next.y > maxHeight) return;
        transform.position = next;
    }
}
