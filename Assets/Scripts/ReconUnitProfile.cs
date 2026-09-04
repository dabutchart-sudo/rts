using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Applies the first-pass Recon/sniper identity. Recon uses the normal squad objective
/// system but stops short of the objective, creating a simple overwatch/standoff role.
/// Values are deliberately conservative for initial observation and tuning.
/// </summary>
public sealed class ReconUnitProfile : MonoBehaviour
{
    [Header("Recon Weapon Profile")]
    [SerializeField] private float attackRange = 24f;
    [SerializeField] private float fireRate = 0.55f;
    [SerializeField] private float damagePerShot = 42f;
    [SerializeField] private float accuracySpread = 0.65f;

    [Header("Recon Movement Profile")]
    [Tooltip("How far Recon tries to stop from its squad objective. This keeps it behind the Assault element instead of standing on the capture point.")]
    [SerializeField] private float objectiveStandoffDistance = 14f;

    private bool applied;

    private void Awake()
    {
        ApplyProfile();
    }

    public void ApplyProfile()
    {
        if (applied) return;

        Combat combat = GetComponent<Combat>();
        if (combat != null)
        {
            combat.attackRange = Mathf.Max(1f, attackRange);
            combat.fireRate = Mathf.Max(0.05f, fireRate);
            combat.damagePerShot = Mathf.Max(1f, damagePerShot);
            combat.accuracySpread = Mathf.Max(0f, accuracySpread);
        }

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, objectiveStandoffDistance);
        }

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
