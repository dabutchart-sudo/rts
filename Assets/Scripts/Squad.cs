using System.Collections.Generic;
using UnityEngine;

public enum SquadOrderType
{
    None,
    Move,
    Attack,
    Defend,
    Retreat
}

public enum SquadCommandSource
{
    AI,
    Player,
    System
}

public enum SquadRole
{
    Attack,
    Defend,
    Support,
    Reserve
}

[System.Serializable]
public class Squad
{
    [SerializeField] private string squadId;
    [SerializeField] private string displayName;
    [SerializeField] private Faction faction;
    [SerializeField] private SquadRole role = SquadRole.Attack;
    [SerializeField] private List<GameObject> members = new List<GameObject>();
    [SerializeField] private SquadOrderType currentOrder = SquadOrderType.None;
    [SerializeField] private SquadCommandSource currentCommandSource = SquadCommandSource.AI;
    [SerializeField] private Transform strategicObjective;
    [SerializeField] private int strategicObjectiveSectorIndex = -1;

    public string SquadId => squadId;
    public string DisplayName => displayName;
    public Faction Faction => faction;
    public SquadRole Role => role;
    public IReadOnlyList<GameObject> Members => members;
    public SquadOrderType CurrentOrder => currentOrder;
    public SquadCommandSource CurrentCommandSource => currentCommandSource;
    public Transform StrategicObjective => strategicObjective;
    public int StrategicObjectiveSectorIndex => strategicObjectiveSectorIndex;
    public int MemberCount => members.Count;

    public Squad(string squadId, string displayName, Faction faction, SquadRole role)
    {
        this.squadId = squadId;
        this.displayName = displayName;
        this.faction = faction;
        this.role = role;
    }

    public bool Contains(GameObject unit)
    {
        return unit != null && members.Contains(unit);
    }

    public bool TryAddMember(GameObject unit, int capacity)
    {
        if (unit == null || members.Contains(unit) || members.Count >= capacity)
        {
            return false;
        }

        members.Add(unit);
        return true;
    }

    public void RemoveMember(GameObject unit)
    {
        if (unit == null) return;
        members.Remove(unit);
    }

    public int CountClass(UnitClass unitClass)
    {
        RemoveMissingMembers();

        int count = 0;
        foreach (GameObject member in members)
        {
            if (member == null) continue;

            UnitClassIdentity identity = member.GetComponent<UnitClassIdentity>();
            if (identity != null && identity.Class == unitClass)
            {
                count++;
            }
        }

        return count;
    }

    public string GetCompositionSummary()
    {
        return $"A{CountClass(UnitClass.Assault)} / E{CountClass(UnitClass.Engineer)} / R{CountClass(UnitClass.Recon)} / S{CountClass(UnitClass.Support)}";
    }

    public void SetRole(SquadRole newRole)
    {
        if (role == newRole) return;

        role = newRole;
        ClearStrategicObjective();
    }

    public void SetOrder(SquadOrderType order, SquadCommandSource source)
    {
        currentOrder = order;
        currentCommandSource = source;
    }

    public void ClearOrder()
    {
        currentOrder = SquadOrderType.None;
        currentCommandSource = SquadCommandSource.AI;
    }

    public void AssignStrategicObjective(Transform objective, int sectorIndex)
    {
        strategicObjective = objective;
        strategicObjectiveSectorIndex = objective != null ? sectorIndex : -1;
    }

    public void ClearStrategicObjective()
    {
        strategicObjective = null;
        strategicObjectiveSectorIndex = -1;
    }

    public void RemoveMissingMembers()
    {
        members.RemoveAll(member => member == null);
    }
}
