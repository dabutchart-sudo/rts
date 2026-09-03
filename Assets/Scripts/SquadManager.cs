using System.Collections.Generic;
using UnityEngine;

public sealed class SquadManager : MonoBehaviour
{
    public static SquadManager Instance { get; private set; }

    [Header("Squad Structure")]
    [SerializeField] private int squadCapacity = 4;
    [SerializeField] private int squadsPerFaction = 4;
    [Tooltip("Additional non-Assault infantry slots available in each squad. These do not reduce the four core Assault slots.")]
    [SerializeField] private int specialistSlotsPerSquad = 1;

    [Header("Runtime Squads")]
    [SerializeField] private List<Squad> attackerSquads = new List<Squad>();
    [SerializeField] private List<Squad> defenderSquads = new List<Squad>();

    public IReadOnlyList<Squad> AttackerSquads => attackerSquads;
    public IReadOnlyList<Squad> DefenderSquads => defenderSquads;

    private static readonly string[] DefaultSquadNames =
    {
        "Alpha",
        "Bravo",
        "Charlie",
        "Delta"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeManager()
    {
        EnsureInstance();
    }

    public static SquadManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        SquadManager existing = FindAnyObjectByType<SquadManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject("SquadManager");
        return host.AddComponent<SquadManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildSquadsIfNeeded();
    }

    private void BuildSquadsIfNeeded()
    {
        if (attackerSquads.Count == 0)
        {
            BuildSquadList(attackerSquads, Faction.Attacker, "ATK");
        }

        if (defenderSquads.Count == 0)
        {
            BuildSquadList(defenderSquads, Faction.Defender, "DEF");
        }
    }

    private void BuildSquadList(List<Squad> target, Faction faction, string idPrefix)
    {
        int count = Mathf.Max(1, squadsPerFaction);

        for (int i = 0; i < count; i++)
        {
            string name = i < DefaultSquadNames.Length ? DefaultSquadNames[i] : $"Squad {i + 1}";
            SquadRole role = GetDefaultRole(faction, i);
            target.Add(new Squad($"{idPrefix}-{i + 1}", name, faction, role));
        }
    }

    private SquadRole GetDefaultRole(Faction faction, int squadIndex)
    {
        if (squadIndex == 2)
        {
            return SquadRole.Support;
        }

        if (squadIndex == 3)
        {
            return SquadRole.Reserve;
        }

        return faction == Faction.Defender ? SquadRole.Defend : SquadRole.Attack;
    }

    public Squad RegisterAssaultUnit(GameObject unit)
    {
        if (unit == null) return null;
        UnitClassIdentity.Ensure(unit, UnitClass.Assault);
        return RegisterUnit(unit, UnitClass.Assault);
    }

    public Squad RegisterSpecialistUnit(GameObject unit, UnitClass unitClass)
    {
        if (unit == null || unitClass == UnitClass.Assault) return null;
        UnitClassIdentity.Ensure(unit, unitClass);
        return RegisterUnit(unit, unitClass);
    }

    public Squad RegisterUnit(GameObject unit, UnitClass unitClass)
    {
        if (unit == null) return null;

        Faction faction = GetFaction(unit);
        if (faction == Faction.None)
        {
            Debug.LogWarning($"SquadManager: Could not determine faction for '{unit.name}'. Unit was not assigned to a squad.");
            return null;
        }

        List<Squad> squads = faction == Faction.Attacker ? attackerSquads : defenderSquads;

        foreach (Squad squad in squads)
        {
            squad.RemoveMissingMembers();
            if (squad.Contains(unit)) return squad;
        }

        Squad targetSquad = unitClass == UnitClass.Assault
            ? FindSquadForAssault(squads)
            : FindSquadForSpecialist(squads, unitClass);

        if (targetSquad == null)
        {
            string slotType = unitClass == UnitClass.Assault ? "Assault" : "specialist";
            Debug.Log($"SquadManager: All {faction} {slotType} squad slots are occupied. '{unit.name}' remains unassigned.");
            return null;
        }

        int totalCapacity = Mathf.Max(1, squadCapacity) + Mathf.Max(0, specialistSlotsPerSquad);
        if (!targetSquad.TryAddMember(unit, totalCapacity)) return null;

        SquadMember squadMember = unit.GetComponent<SquadMember>();
        if (squadMember == null)
        {
            squadMember = unit.AddComponent<SquadMember>();
        }

        squadMember.Assign(targetSquad);

        UnitClassIdentity.Ensure(unit, unitClass);
        Debug.Log($"SquadManager: {unit.name} ({unitClass}) assigned to {faction} {targetSquad.DisplayName} [{targetSquad.Role}] ({targetSquad.MemberCount}/{totalCapacity}).");
        return targetSquad;
    }

    private Squad FindSquadForAssault(List<Squad> squads)
    {
        foreach (Squad squad in squads)
        {
            if (CountClass(squad, UnitClass.Assault) < Mathf.Max(1, squadCapacity))
            {
                return squad;
            }
        }

        return null;
    }

    private Squad FindSquadForSpecialist(List<Squad> squads, UnitClass unitClass)
    {
        int allowedSpecialists = Mathf.Max(0, specialistSlotsPerSquad);
        if (allowedSpecialists == 0) return null;

        Squad best = null;
        int bestSpecialistCount = int.MaxValue;
        int bestRoleBias = int.MinValue;

        foreach (Squad squad in squads)
        {
            int specialistCount = CountSpecialists(squad);
            if (specialistCount >= allowedSpecialists) continue;

            int roleBias = GetSpecialistRoleBias(squad, unitClass);
            if (best == null || specialistCount < bestSpecialistCount ||
                (specialistCount == bestSpecialistCount && roleBias > bestRoleBias))
            {
                best = squad;
                bestSpecialistCount = specialistCount;
                bestRoleBias = roleBias;
            }
        }

        return best;
    }

    private int GetSpecialistRoleBias(Squad squad, UnitClass unitClass)
    {
        if (squad == null) return 0;

        switch (unitClass)
        {
            case UnitClass.Support:
                return squad.Role == SquadRole.Support ? 3 : (squad.Role == SquadRole.Defend ? 2 : 0);
            case UnitClass.Recon:
                return squad.Role == SquadRole.Reserve ? 3 : (squad.Role == SquadRole.Attack ? 1 : 0);
            case UnitClass.Engineer:
                return squad.Role == SquadRole.Attack || squad.Role == SquadRole.Defend ? 2 : 0;
            default:
                return 0;
        }
    }

    private int CountSpecialists(Squad squad)
    {
        if (squad == null) return 0;

        int count = 0;
        foreach (GameObject member in squad.Members)
        {
            if (member != null && UnitClassIdentity.GetClass(member) != UnitClass.Assault)
            {
                count++;
            }
        }

        return count;
    }

    private int CountClass(Squad squad, UnitClass unitClass)
    {
        if (squad == null) return 0;

        int count = 0;
        foreach (GameObject member in squad.Members)
        {
            if (member != null && UnitClassIdentity.GetClass(member) == unitClass)
            {
                count++;
            }
        }

        return count;
    }

    public void UnregisterUnit(GameObject unit)
    {
        if (unit == null) return;

        RemoveFromSquads(attackerSquads, unit);
        RemoveFromSquads(defenderSquads, unit);
    }

    private void RemoveFromSquads(List<Squad> squads, GameObject unit)
    {
        foreach (Squad squad in squads)
        {
            if (squad.Contains(unit))
            {
                squad.RemoveMember(unit);
                return;
            }
        }
    }

    public Squad GetSquadForUnit(GameObject unit)
    {
        if (unit == null) return null;

        SquadMember member = unit.GetComponent<SquadMember>();
        return member != null ? member.Squad : null;
    }

    public void ClearStrategicObjectives()
    {
        ClearStrategicObjectives(attackerSquads);
        ClearStrategicObjectives(defenderSquads);
    }

    private void ClearStrategicObjectives(List<Squad> squads)
    {
        foreach (Squad squad in squads)
        {
            squad.ClearStrategicObjective();
        }
    }

    private Faction GetFaction(GameObject unit)
    {
        if (unit.CompareTag("Attacker")) return Faction.Attacker;
        if (unit.CompareTag("Defender")) return Faction.Defender;
        return Faction.None;
    }
}
