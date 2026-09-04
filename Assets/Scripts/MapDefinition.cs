using UnityEngine;

/// <summary>
/// Lightweight identity/configuration component for a playable battlefield.
/// Keeping map identity separate from GameManager means maps can later be selected,
/// tested and procedurally generated without hard-coding scene names throughout gameplay.
/// </summary>
public sealed class MapDefinition : MonoBehaviour
{
    [Header("Map Identity")]
    public string mapId = "development_test";
    public string displayName = "Development Test Map";
    [TextArea] public string description = "Original systems-development battlefield.";

    [Header("Testing")]
    [Tooltip("Useful when filtering maps from automated balance testing later.")]
    public bool allowAutomatedTesting = true;

    public static MapDefinition Active { get; private set; }

    private void Awake()
    {
        if (Active != null && Active != this)
        {
            Debug.LogWarning($"Multiple MapDefinition components are active. Using '{displayName}'.", this);
        }

        Active = this;
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }
}
