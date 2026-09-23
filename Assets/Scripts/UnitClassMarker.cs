using TMPro;
using UnityEngine;

/// <summary>
/// Authoritative class marker painted onto the top face of infantry units.
/// Floating text is reserved for squad number, while class is conveyed by shape:
/// Assault=triangle, Engineer=crossed mark, Recon=target, Support=plus.
/// </summary>
public sealed class UnitClassMarker : MonoBehaviour
{
    [Header("Top-Face Class Icon")]
    [Tooltip("Placed slightly above the cube top to prevent z-fighting while still reading as painted on.")]
    [SerializeField] private float markerHeight = 0.56f;
    [Tooltip("Sized to cover roughly three quarters of the visible top face.")]
    [SerializeField] private float markerScale = 0.72f;
    [SerializeField] private float fontSize = 9f;

    private TextMeshPro markerText;
    private UnitClassIdentity identity;

    private void Awake()
    {
        identity = GetComponent<UnitClassIdentity>();
        CreateMarker();
        HideLegacyClassLabels();
        Refresh();
    }

    private void LateUpdate()
    {
        if (identity == null) identity = GetComponent<UnitClassIdentity>();
        Refresh();
    }

    private void CreateMarker()
    {
        Transform existing = transform.Find("UnitClassMarker");
        if (existing != null) markerText = existing.GetComponent<TextMeshPro>();

        if (markerText == null)
        {
            GameObject markerObject = new GameObject("UnitClassMarker");
            markerObject.transform.SetParent(transform, false);
            markerText = markerObject.AddComponent<TextMeshPro>();
        }

        markerText.transform.localPosition = new Vector3(0f, markerHeight, 0f);
        markerText.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        markerText.transform.localScale = Vector3.one * markerScale;

        Billboard billboard = markerText.GetComponent<Billboard>();
        if (billboard != null) Destroy(billboard);

        markerText.alignment = TextAlignmentOptions.Center;
        markerText.fontSize = fontSize;
        markerText.fontStyle = FontStyles.Bold;
        markerText.color = Color.white;
        markerText.outlineWidth = 0.12f;
        markerText.outlineColor = Color.black;
        markerText.enableAutoSizing = false;
        markerText.raycastTarget = false;
        markerText.sortingOrder = 28;
    }

    private void HideLegacyClassLabels()
    {
        TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            if (label == null || label == markerText) continue;

            string value = label.text != null ? label.text.Trim() : string.Empty;
            if (value == "A" || value == "E" || value == "R" || value == "S")
            {
                label.gameObject.SetActive(false);
            }
        }
    }

    private void Refresh()
    {
        if (markerText == null) return;

        UnitClass unitClass = identity != null ? identity.Class : UnitClass.Assault;
        markerText.text = GetClassIcon(unitClass);
    }

    private string GetClassIcon(UnitClass unitClass)
    {
        switch (unitClass)
        {
            case UnitClass.Engineer: return "×";
            case UnitClass.Recon: return "◎";
            case UnitClass.Support: return "+";
            default: return "▲";
        }
    }

    public static UnitClassMarker Ensure(GameObject unit)
    {
        if (unit == null) return null;

        UnitClassMarker marker = unit.GetComponent<UnitClassMarker>();
        if (marker == null) marker = unit.AddComponent<UnitClassMarker>();
        return marker;
    }
}
