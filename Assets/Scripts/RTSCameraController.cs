using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    [Header("Movement Speeds")]
    public float moveSpeed = 25f;
    public float zoomSpeed = 20f;

    [Header("Height / Zoom Limits")]
    public float minHeight = 10f;
    public float maxHeight = 50f;

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerFaction == Faction.None) return;

        // 1. Pan with WASD or Arrow Keys
        float xInput = Input.GetAxis("Horizontal"); // A/D
        float zInput = Input.GetAxis("Vertical");   // W/S

        Vector3 moveDirection = new Vector3(xInput, 0, zInput).normalized;
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);

        // 2. Trackpad Pinch / Mouse Scrollwheel Zooming
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0)
        {
            Vector3 pos = transform.position;
            pos.y -= scrollInput * zoomSpeed * 10f;
            pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
            transform.position = pos;
        }
    }
}