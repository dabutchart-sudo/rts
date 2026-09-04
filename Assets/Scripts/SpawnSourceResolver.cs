using System.Collections.Generic;
using UnityEngine;

public enum SpawnSourceKind
{
    DynamicBase,
    CapturePoint
}

/// <summary>
/// A faction-owned location from which purchased units can deploy.
/// The location is deliberately separate from the purchase catalog so player and AI
/// can use the same deployment rules without CapturePoint owning the store logic.
/// </summary>
public sealed class SpawnSource
{
    public Faction Faction { get; }
    public int SectorIndex { get; }
    public SpawnSourceKind Kind { get; }
    public string DisplayName { get; }
    public BaseZone BaseZone { get; }
    public CapturePoint CapturePoint { get; }

    public SpawnSource(Faction faction, int sectorIndex, BaseZone baseZone)
    {
        Faction = faction;
        SectorIndex = sectorIndex;
        Kind = SpawnSourceKind.DynamicBase;
        BaseZone = baseZone;
        CapturePoint = null;
        DisplayName = "BASE";
    }

    public SpawnSource(Faction faction, int sectorIndex, CapturePoint capturePoint)
    {
        Faction = faction;
        SectorIndex = sectorIndex;
        Kind = SpawnSourceKind.CapturePoint;
        BaseZone = null;
        CapturePoint = capturePoint;
        DisplayName = capturePoint != null ? capturePoint.capturePointName : "CAPTURE POINT";
    }

    public bool IsAvailable()
    {
        if (GameManager.Instance == null || GameManager.Instance.currentSectorIndex != SectorIndex)
        {
            return false;
        }

        if (Kind == SpawnSourceKind.DynamicBase)
        {
            return BaseZone != null && BaseZone.IsCurrentBaseFor(Faction);
        }

        return CapturePoint != null && CapturePoint.IsControlledBy(Faction);
    }

    public Transform GetNextSpawnTransform()
    {
        if (!IsAvailable()) return null;

        if (Kind == SpawnSourceKind.DynamicBase)
        {
            return BaseZone != null ? BaseZone.GetSpawnPoint() : null;
        }

        return CapturePoint != null ? CapturePoint.GetNextAvailableSpawnPoint() : null;
    }

    public Vector3 GetPosition()
    {
        if (Kind == SpawnSourceKind.DynamicBase)
        {
            Transform point = BaseZone != null ? BaseZone.GetSpawnPoint() : null;
            return point != null ? point.position : Vector3.zero;
        }

        return CapturePoint != null ? CapturePoint.transform.position : Vector3.zero;
    }
}

/// <summary>
/// Single source of truth for deployment locations and the faction purchase catalog.
/// Valid spawn sources are the faction's dynamic base plus capture points it currently owns.
/// </summary>
public static class SpawnSourceResolver
{
    public static List<SpawnSource> GetCurrentSectorSources(Faction faction, bool availableOnly)
    {
        List<SpawnSource> sources = new List<SpawnSource>();

        if (!TryGetCurrentSector(out Sector sector, out int sectorIndex))
        {
            return sources;
        }

        BaseZone factionBase = faction == Faction.Attacker ? sector.attackerBase : sector.defenderBase;
        if (factionBase != null)
        {
            SpawnSource baseSource = new SpawnSource(faction, sectorIndex, factionBase);
            if (!availableOnly || baseSource.IsAvailable()) sources.Add(baseSource);
        }

        if (sector.capturePoints != null)
        {
            foreach (CapturePoint capturePoint in sector.capturePoints)
            {
                if (capturePoint == null) continue;

                SpawnSource captureSource = new SpawnSource(faction, sectorIndex, capturePoint);
                if (!availableOnly || captureSource.IsAvailable()) sources.Add(captureSource);
            }
        }

        return sources;
    }

    public static List<PurchasableUnit> GetCurrentSectorCatalog(Faction faction)
    {
        List<PurchasableUnit> catalog = new List<PurchasableUnit>();
        HashSet<string> seen = new HashSet<string>();

        if (!TryGetCurrentSector(out Sector sector, out _ ) || sector.capturePoints == null)
        {
            return catalog;
        }

        foreach (CapturePoint capturePoint in sector.capturePoints)
        {
            if (capturePoint == null) continue;

            PurchasableUnit[] units = faction == Faction.Attacker
                ? capturePoint.attackerPurchasables
                : capturePoint.defenderPurchasables;

            if (units == null) continue;

            foreach (PurchasableUnit unit in units)
            {
                if (unit == null || unit.unitPrefab == null) continue;

                string key = BuildCatalogKey(unit);
                if (seen.Add(key)) catalog.Add(unit);
            }
        }

        return catalog;
    }

    /// <summary>
    /// Selects a useful AI deployment location from the same legal sources available to a player.
    /// Attackers favour the source closest to an objective they do not own. Defenders favour the
    /// source closest to the most threatened objective. If no useful target exists, the forward-most
    /// source nearest any active capture point wins.
    /// </summary>
    public static SpawnSource ChooseBestAISource(Faction faction, List<SpawnSource> availableSources)
    {
        if (availableSources == null || availableSources.Count == 0) return null;
        if (!TryGetCurrentSector(out Sector sector, out _ ) || sector.capturePoints == null)
        {
            return availableSources[0];
        }

        CapturePoint target = FindAITarget(faction, sector.capturePoints);
        if (target == null)
        {
            return availableSources[0];
        }

        SpawnSource bestSource = availableSources[0];
        float bestDistance = Vector3.Distance(bestSource.GetPosition(), target.transform.position);

        for (int i = 1; i < availableSources.Count; i++)
        {
            SpawnSource candidate = availableSources[i];
            if (candidate == null) continue;

            float distance = Vector3.Distance(candidate.GetPosition(), target.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSource = candidate;
            }
        }

        return bestSource;
    }

    private static CapturePoint FindAITarget(Faction faction, CapturePoint[] capturePoints)
    {
        CapturePoint best = null;

        if (faction == Faction.Attacker)
        {
            float bestProgress = float.MaxValue;
            foreach (CapturePoint cp in capturePoints)
            {
                if (cp == null || cp.IsControlledBy(Faction.Attacker)) continue;

                if (cp.captureProgress < bestProgress)
                {
                    bestProgress = cp.captureProgress;
                    best = cp;
                }
            }
        }
        else if (faction == Faction.Defender)
        {
            float highestThreat = float.MinValue;
            foreach (CapturePoint cp in capturePoints)
            {
                if (cp == null) continue;

                float threat = cp.attackerCount * 100f + cp.captureProgress;
                if (threat > highestThreat)
                {
                    highestThreat = threat;
                    best = cp;
                }
            }
        }

        if (best != null) return best;

        foreach (CapturePoint cp in capturePoints)
        {
            if (cp != null) return cp;
        }

        return null;
    }

    private static bool TryGetCurrentSector(out Sector sector, out int sectorIndex)
    {
        sector = null;
        sectorIndex = -1;

        if (GameManager.Instance == null || GameManager.Instance.sectors == null)
        {
            return false;
        }

        sectorIndex = GameManager.Instance.currentSectorIndex;
        if (sectorIndex < 0 || sectorIndex >= GameManager.Instance.sectors.Length)
        {
            return false;
        }

        sector = GameManager.Instance.sectors[sectorIndex];
        return sector != null;
    }

    private static string BuildCatalogKey(PurchasableUnit unit)
    {
        string key = $"{unit.unitDisplayName}|{unit.xpCost}|{unit.GetResolvedCategory()}";
        if (unit.TryGetResolvedInfantryClass(out UnitClass unitClass))
        {
            key += $"|{unitClass}";
        }
        return key;
    }
}
