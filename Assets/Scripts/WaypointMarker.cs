using UnityEngine;

public class WaypointMarker : MonoBehaviour
{
    [Tooltip("How long the marker stays on the ground before disappearing")]
    public float lifetime = 1.5f;

    void Start()
    {
        // Destroy this object after 'lifetime' seconds
        Destroy(gameObject, lifetime);
    }
}