using UnityEngine;

/// <summary>
/// Applies the first-pass Recon/sniper combat identity without requiring a separate
/// combat implementation. Values are deliberately conservative for initial testing.
/// </summary>
public sealed class ReconUnitProfile : MonoBehaviour
{
    [Header("Recon Weapon Profile")]
    [SerializeField] private float attackRange = 24f;
    [SerializeField] private float fireRate = 0.55f;
    [SerializeField] private float damagePerShot = 42f;
    [SerializeField] private float accuracySpread = 0.65f;

    private bool applied;

    private void Awake()
    {
        ApplyProfile();
    }

    public void ApplyProfile()
    {
        if (applied) return;

        Combat combat = GetComponent<Combat>();
        if (combat == null) return;

        combat.attackRange = Mathf.Max(1f, attackRange);
        combat.fireRate = Mathf.Max(0.05f, fireRate);
        combat.damagePerShot = Mathf.Max(1f, damagePerShot);
        combat.accuracySpread = Mathf.Max(0f, accuracySpread);
        applied = true;
    }

    public static ReconUnitProfile ApplyIfRecon(GameObject unit)
    {
        if (unit == null || UnitClassIdentity.GetClass(unit) != UnitClass.Recon)
        {
            return null;
        }

        ReconUnitProfile profile = unit.GetComponent<ReconUnitProfile>();
        if (profile == null)
        {
            profile = unit.AddComponent<ReconUnitProfile>();
        }

        profile.ApplyProfile();
        return profile;
    }
}
