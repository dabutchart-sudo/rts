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

[System.Serializable]
public class Squad
{
    [SerializeField] private string squadId;
    [SerializeField] private string displayName;
    [SerializeField] private Faction faction;
    [SerializeField] private List<GameObject> members = new List<GameObject>();
    [SerializeField] private SquadOrderType currentOrder = SquadOrderType.None;
    [SerializeField] private SquadCommandSource currentCommandSource = SquadCommandSource.AI;

    public string SquadId => squadId;
    public string DisplayName => displayName;
    public Faction Faction => faction;
    public IReadOnlyList<GameObject> Members => members;
    public SquadOrderType CurrentOrder => currentOrder;
    public SquadCommandSource CurrentCommandSource => currentCommandSource;
    public int MemberCount => members.Count;

    public Squad(string squadId, string displayName, Faction faction)
    {
        this.squadId = squadId;
        this.displayName = displayName;
        this.faction = faction;
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

    public void RemoveMissingMembers()
    {
        members.RemoveAll(member => member == null);
    }
}
