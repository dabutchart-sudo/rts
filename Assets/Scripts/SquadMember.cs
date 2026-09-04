using UnityEngine;

/// <summary>
/// Stores squad membership and displays squad identity as a ground ring rather than a
/// floating letter. This keeps A/B/C/D visually separate from class and objective labels.
/// </summary>
public sealed class SquadMember : MonoBehaviour
{
    [SerializeField] private string squadId;
    [SerializeField] private string squadName;
    [SerializeField] private string squadLetter;

    [Header("Squad Ground Indicator")]
    [SerializeField] private bool showSquadIndicator = true;
    [SerializeField] private float ringDiameter = 1.15f;
    [SerializeField] private float ringHeight = 0.04f;

    private GameObject squadRing;
    private Renderer squadRingRenderer;
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

        DisableLegacyFloatingIndicator();
        CreateOrRefreshGroundRing();
    }

    private void LateUpdate()
    {
        if (squadRing == null) return;

        bool shouldShow = showSquadIndicator && IsPlayerFactionUnit();
        squadRing.SetActive(shouldShow);

        if (!shouldShow) return;

        float selectedScale = selectableUnit != null && selectableUnit.isSelected ? 1.28f : 1f;
        squadRing.transform.localScale = new Vector3(ringDiameter * selectedScale, ringHeight, ringDiameter * selectedScale);
    }

    private void DisableLegacyFloatingIndicator()
    {
        Transform legacy = transform.Find("SquadIndicator");
        if (legacy != null)
        {
            legacy.gameObject.SetActive(false);
        }
    }

    private void CreateOrRefreshGroundRing()
    {
        if (!showSquadIndicator || Squad == null || string.IsNullOrEmpty(squadLetter))
        {
            if (squadRing != null) squadRing.SetActive(false);
            return;
        }

        Transform existing = transform.Find("SquadGroundRing");
        if (existing != null)
        {
            squadRing = existing.gameObject;
            squadRingRenderer = squadRing.GetComponent<Renderer>();
        }

        if (squadRing == null)
        {
            squadRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            squadRing.name = "SquadGroundRing";
            squadRing.transform.SetParent(transform, false);
            squadRing.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            squadRing.transform.localRotation = Quaternion.identity;
            squadRing.transform.localScale = new Vector3(ringDiameter, ringHeight, ringDiameter);

            Collider ringCollider = squadRing.GetComponent<Collider>();
            if (ringCollider != null) Destroy(ringCollider);

            squadRingRenderer = squadRing.GetComponent<Renderer>();
        }

        if (squadRingRenderer != null)
        {
            squadRingRenderer.material.color = GetSquadColor(squadLetter);
        }

        squadRing.SetActive(IsPlayerFactionUnit());
    }

    private Color GetSquadColor(string letter)
    {
        switch (letter)
        {
            case "A": return new Color(0.15f, 0.8f, 1f);
            case "B": return new Color(1f, 0.65f, 0.1f);
            case "C": return new Color(0.35f, 1f, 0.35f);
            case "D": return new Color(0.85f, 0.35f, 1f);
            default: return Color.white;
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
        if (GameManager.Instance == null || GameManager.Instance.playerFaction == Faction.None)
        {
            return false;
        }

        if (GameManager.Instance.playerFaction == Faction.Attacker)
        {
            return CompareTag("Attacker");
        }

        if (GameManager.Instance.playerFaction == Faction.Defender)
        {
            return CompareTag("Defender");
        }

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
