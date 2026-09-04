using UnityEngine;

/// <summary>
/// Ensures specialist gameplay profiles are attached regardless of whether a unit was
/// spawned by the player store, AI Commander, scene setup, or another runtime system.
/// </summary>
public sealed class SpecialistAbilityBootstrap : MonoBehaviour
{
    [SerializeField] private float scanInterval = 1f;
    private float nextScanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeBootstrap()
    {
        SpecialistAbilityBootstrap existing = FindAnyObjectByType<SpecialistAbilityBootstrap>(FindObjectsInactive.Include);
        if (existing != null) return;

        GameObject host = new GameObject("SpecialistAbilityBootstrap");
        host.AddComponent<SpecialistAbilityBootstrap>();
    }

    private void Update()
    {
        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + Mathf.Max(0.2f, scanInterval);

        ApplyToFaction("Attacker");
        ApplyToFaction("Defender");
    }

    private void ApplyToFaction(string factionTag)
    {
        GameObject[] units = GameObject.FindGameObjectsWithTag(factionTag);
        foreach (GameObject unit in units)
        {
            if (unit == null) continue;

            UnitCategoryIdentity category = unit.GetComponent<UnitCategoryIdentity>();
            if (category != null && category.Category != UnitCategory.Infantry) continue;

            UnitClassIdentity classIdentity = unit.GetComponent<UnitClassIdentity>();
            if (classIdentity == null) continue;

            switch (classIdentity.Class)
            {
                case UnitClass.Engineer:
                    EngineerUnitProfile.ApplyIfEngineer(unit);
                    break;
                case UnitClass.Recon:
                    ReconUnitProfile.ApplyIfRecon(unit);
                    break;
                case UnitClass.Support:
                    SupportUnitProfile.ApplyIfSupport(unit);
                    break;
            }
        }
    }
}
