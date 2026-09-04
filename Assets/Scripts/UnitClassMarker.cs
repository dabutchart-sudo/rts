using TMPro;
using UnityEngine;

/// <summary>
/// Authoritative world-space class marker. The displayed letter always comes from
/// UnitClassIdentity, while colour always comes from the shared faction palette.
/// </summary>
public sealed class UnitClassMarker : MonoBehaviour
{
    [SerializeField] private float markerHeight = 2.55f;
    [SerializeField] private float markerScale = 0.42f;
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
        if (markerText != null) return;

        GameObject markerObject = new GameObject("UnitClassMarker");
        markerObject.transform.SetParent(transform, false);
        markerObject.transform.localPosition = new Vector3(0f, markerHeight, 0f);
        markerObject.transform.localRotation = Quaternion.identity;
        markerObject.transform.localScale = Vector3.one * markerScale;

        markerText = markerObject.AddComponent<TextMeshPro>();
        markerText.alignment = TextAlignmentOptions.Center;
        markerText.fontSize = fontSize;
        markerText.fontStyle = FontStyles.Bold;
        markerText.enableAutoSizing = false;
        markerText.raycastTarget = false;
        markerText.sortingOrder = 30;
        markerObject.AddComponent<Billboard>();
    }

    private void HideLegacyClassLabels()
    {
        TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            if (label == null || label == markerText) continue;
            string value = label.text != null ? label.text.Trim() : string.Empty;
            if (value == "A" || value == "E" || value == "R" || value == "S") label.gameObject.SetActive(false);
        }
    }

    private void Refresh()
    {
        if (markerText == null) return;
        UnitClass unitClass = identity != null ? identity.Class : UnitClass.Assault;
        markerText.text = GetClassLetter(unitClass);
        markerText.color = CompareTag("Attacker") ? FactionVisuals.AttackerColor :
                           CompareTag("Defender") ? FactionVisuals.DefenderColor : Color.white;
    }

    private string GetClassLetter(UnitClass unitClass)
    {
        switch (unitClass)
        {
            case UnitClass.Engineer: return "E";
            case UnitClass.Recon: return "R";
            case UnitClass.Support: return "S";
            default: return "A";
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