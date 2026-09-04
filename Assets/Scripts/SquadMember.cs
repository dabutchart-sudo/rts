using TMPro;
using UnityEngine;

/// <summary>
/// Stores squad membership. Squad identity is shown with one to four pips rather than
/// letters or colour alone: Alpha=•, Bravo=••, Charlie=•••, Delta=••••.
/// This stays distinct from class letters and objective names and remains colour-blind safe.
/// </summary>
public sealed class SquadMember : MonoBehaviour
{
    [SerializeField] private string squadId;
    [SerializeField] private string squadName;
    [SerializeField] private string squadLetter;

    [Header("Squad Pip Indicator")]
    [SerializeField] private bool showSquadIndicator = true;
    [SerializeField] private float indicatorHeight = 2.05f;
    [SerializeField] private float indicatorScale = 0.34f;
    [SerializeField] private float normalFontSize = 8f;
    [SerializeField] private float selectedFontSize = 10f;

    private TextMeshPro indicatorText;
    private SelectableUnit selectableUnit;

    public Squad Squad { get; private set; }
    public string SquadId => squadId;
    public string SquadName => squadName;
    public string SquadLetter => squadLetter;

    private void Awake()
    {
        selectableUnit = GetComponent<SelectableUnit>();
    }

    public void Assign(Squad squad)
    {
        Squad = squad;
        squadId = squad != null ? squad.SquadId : string.Empty;
        squadName = squad != null ? squad.DisplayName : string.Empty;
        squadLetter = GetSquadLetter(squadName);

        DisableLegacyIndicators();
        CreateOrRefreshIndicator();
    }

    private void LateUpdate()
    {
        if (indicatorText == null) return;

        bool shouldShow = showSquadIndicator && IsPlayerFactionUnit();
        indicatorText.gameObject.SetActive(shouldShow);
        if (!shouldShow) return;

        bool selected = selectableUnit != null && selectableUnit.isSelected;
        indicatorText.text = GetSquadPips(squadLetter);
        indicatorText.fontSize = selected ? selectedFontSize : normalFontSize;
    }

    private void DisableLegacyIndicators()
    {
        Transform floating = transform.Find("SquadIndicator");
        if (floating != null) floating.gameObject.SetActive(false);

        Transform ring = transform.Find("SquadGroundRing");
        if (ring != null) ring.gameObject.SetActive(false);
    }

    private void CreateOrRefreshIndicator()
    {
        if (!showSquadIndicator || Squad == null || string.IsNullOrEmpty(squadLetter))
        {
            if (indicatorText != null) indicatorText.gameObject.SetActive(false);
            return;
        }

        Transform existing = transform.Find("SquadPipIndicator");
        if (existing != null) indicatorText = existing.GetComponent<TextMeshPro>();

        if (indicatorText == null)
        {
            GameObject indicatorObject = new GameObject("SquadPipIndicator");
            indicatorObject.transform.SetParent(transform, false);
            indicatorObject.transform.localPosition = new Vector3(0f, indicatorHeight, 0f);
            indicatorObject.transform.localRotation = Quaternion.identity;
            indicatorObject.transform.localScale = Vector3.one * indicatorScale;

            indicatorText = indicatorObject.AddComponent<TextMeshPro>();
            indicatorText.alignment = TextAlignmentOptions.Center;
            indicatorText.fontSize = normalFontSize;
            indicatorText.fontStyle = FontStyles.Bold;
            indicatorText.color = Color.white;
            indicatorText.enableAutoSizing = false;
            indicatorText.raycastTarget = false;
            indicatorText.sortingOrder = 25;

            indicatorObject.AddComponent<Billboard>();
        }

        indicatorText.text = GetSquadPips(squadLetter);
        indicatorText.gameObject.SetActive(IsPlayerFactionUnit());
    }

    private string GetSquadPips(string letter)
    {
        switch (letter)
        {
            case "A": return "•";
            case "B": return "••";
            case "C": return "•••";
            case "D": return "••••";
            default: return "•";
        }
    }

    private string GetSquadLetter(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return string.Empty;

        switch (displayName.Trim().ToLowerInvariant())
        {
            case "alpha": return "A";
            case "bravo": return "B";
            case "charlie": return "C";
            case "delta": return "D";
            default: return displayName.Substring(0, 1).ToUpperInvariant();
        }
    }

    private bool IsPlayerFactionUnit()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerFaction == Faction.None) return false;
        if (GameManager.Instance.playerFaction == Faction.Attacker) return CompareTag("Attacker");
        if (GameManager.Instance.playerFaction == Faction.Defender) return CompareTag("Defender");
        return false;
    }

    private void OnDestroy()
    {
        if (SquadManager.Instance != null)
        {
            SquadManager.Instance.UnregisterUnit(gameObject);
        }
    }
}