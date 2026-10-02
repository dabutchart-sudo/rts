using System.Collections.Generic;
using UnityEngine;

public enum CoverStance
{
    Exposed,
    InCover,
    Blocked
}

/// <summary>
/// A waist-high barricade. Spots run along both faces.
/// A soldier is in cover only from enemies on the other side of the wall.
/// Shots at chest height pass over it.
/// </summary>
public sealed class CoverPiece : MonoBehaviour
{
    [Tooltip("Standing spots along each face. Three fits the standard barricade.")]
    [Min(1)] public int slotsPerSide = 3;

    [Tooltip("The enemy must sit within this many degrees of the barricade's forward direction.")]
    [Range(15f, 89f)] public float protectionHalfAngle = 70f;

    [Tooltip("Damage taken from a shooter who is still in front of this barricade.")]
    [Range(0.1f, 1f)] public float incomingDamageMultiplier = 0.5f;

    [Tooltip("How much wider a frontal shot spreads. The wall still stops a bullet that hits it.")]
    [Min(1f)] public float incomingSpreadMultiplier = 1.6f;

    [Tooltip("A soldier will not cross the map for a slot.")]
    [Min(1f)] public float maxClaimDistance = 18f;

    [Tooltip("Draw the gold standing spots. The sandbox turns this on. A match leaves it off until squads use the spots.")]
    public bool showSlotMarkers;

    static readonly List<CoverPiece> Active = new List<CoverPiece>();

    Transform[] occupants;
    bool markersBuilt;

    void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
        EnsureOccupants();
    }

    void Start()
    {
        EnsureOccupants();
        RefreshSlotMarkers();
    }

    public void RefreshSlotMarkers()
    {
        if (showSlotMarkers) BuildMarkers();
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    public static int ActiveCount => Active.Count;

    public static CoverPiece GetActive(int index)
    {
        if (index < 0 || index >= Active.Count) return null;
        return Active[index];
    }

    public bool ThreatIsInFront(Vector3 threatPosition)
    {
        return TryGetCoverSide(threatPosition, out bool useBackFace) && useBackFace;
    }

    public bool Protects(Transform unit, Vector3 threatPosition)
    {
        int slot = FindSlot(unit);
        if (slot < 0) return false;
        if (!TryGetCoverSide(threatPosition, out bool useBackFace)) return false;
        if (SlotIsBack(slot) != useBackFace) return false;

        Vector3 stand = SlotPosition(slot);
        float dx = stand.x - unit.position.x;
        float dz = stand.z - unit.position.z;
        return dx * dx + dz * dz <= 1.44f;
    }

    public float TopY()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        Vector3 size = box != null ? box.size : Vector3.one;
        Vector3 center = box != null ? box.center : Vector3.zero;
        return transform.TransformPoint(center + new Vector3(0f, Mathf.Abs(size.y) * 0.5f, 0f)).y;
    }

    public static bool ShotPassesOver(Collider collider, float worldHeight)
    {
        if (collider == null) return false;
        CoverPiece piece = collider.GetComponent<CoverPiece>();
        if (piece == null) return false;
        return worldHeight >= piece.TopY() - 0.02f;
    }

    public bool Holds(Transform unit)
    {
        if (unit == null) return false;
        EnsureOccupants();
        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == unit) return true;
        }

        return false;
    }

    public bool HasRoomFor(Transform unit, Vector3 threatPosition)
    {
        if (!TryGetCoverSide(threatPosition, out bool useBackFace)) return false;
        EnsureOccupants();
        int start = useBackFace ? 0 : SideCount();
        int end = useBackFace ? SideCount() : occupants.Length;
        for (int i = start; i < end; i++)
        {
            if (occupants[i] == null || occupants[i] == unit) return true;
        }

        return false;
    }

    public void Release(Transform unit)
    {
        if (unit == null || occupants == null) return;
        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == unit) occupants[i] = null;
        }
    }

    public bool TryClaim(Transform unit, Vector3 threatPosition, out Vector3 standAt)
    {
        standAt = unit != null ? unit.position : transform.position;
        if (unit == null) return false;
        if (!TryGetCoverSide(threatPosition, out bool useBackFace)) return false;
        EnsureOccupants();

        int existing = FindSlot(unit);
        if (existing >= 0 && SlotIsBack(existing) == useBackFace)
        {
            standAt = SlotPosition(existing);
            standAt.y = unit.position.y;
            return true;
        }

        int best = -1;
        float bestDistance = float.MaxValue;
        int start = useBackFace ? 0 : SideCount();
        int end = useBackFace ? SideCount() : occupants.Length;
        for (int i = start; i < end; i++)
        {
            if (occupants[i] != null) continue;
            float distance = (SlotPosition(i) - unit.position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }

        if (best < 0) return false;

        for (int i = 0; i < Active.Count; i++)
        {
            if (Active[i] != null) Active[i].Release(unit);
        }

        occupants[best] = unit;
        standAt = SlotPosition(best);
        standAt.y = unit.position.y;
        return true;
    }

    public Vector3 SlotPosition(int index)
    {
        EnsureOccupants();
        int perSide = Mathf.Max(1, slotsPerSide);
        int sideIndex = index % perSide;
        bool backFace = SlotIsBack(index);

        BoxCollider box = GetComponent<BoxCollider>();
        Vector3 size = box != null ? box.size : Vector3.one;
        Vector3 center = box != null ? box.center : Vector3.zero;
        Vector3 scale = transform.lossyScale;

        float absScaleX = Mathf.Max(0.001f, Mathf.Abs(scale.x));
        float absScaleZ = Mathf.Max(0.001f, Mathf.Abs(scale.z));
        float halfLocalX = Mathf.Abs(size.x) * 0.5f;
        float halfLocalZ = Mathf.Abs(size.z) * 0.5f;
        float inset = 0.55f / absScaleX;
        float usable = Mathf.Max(0.05f, halfLocalX - inset);
        float along = perSide == 1 ? 0f : Mathf.Lerp(-usable, usable, sideIndex / (float)(perSide - 1));
        float standOff = 0.7f / absScaleZ;
        float localZ = backFace ? -(halfLocalZ + standOff) : halfLocalZ + standOff;

        Vector3 local = center + new Vector3(along, 0f, localZ);
        Vector3 world = transform.TransformPoint(local);
        float bottom = transform.TransformPoint(center + new Vector3(0f, -Mathf.Abs(size.y) * 0.5f, 0f)).y;
        world.y = bottom;
        return world;
    }

    int SideCount()
    {
        return Mathf.Max(1, slotsPerSide);
    }

    bool SlotIsBack(int index)
    {
        return index < SideCount();
    }

    int FindSlot(Transform unit)
    {
        if (unit == null || occupants == null) return -1;
        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == unit) return i;
        }

        return -1;
    }

    bool TryGetCoverSide(Vector3 threatPosition, out bool useBackFace)
    {
        useBackFace = false;
        Vector3 toThreat = threatPosition - transform.position;
        toThreat.y = 0f;
        if (toThreat.sqrMagnitude < 0.04f) return false;

        float angle = Vector3.Angle(transform.forward, toThreat);
        if (angle <= protectionHalfAngle)
        {
            useBackFace = true;
            return true;
        }

        if (angle >= 180f - protectionHalfAngle)
        {
            useBackFace = false;
            return true;
        }

        return false;
    }

    public bool TryPreviewStand(Transform unit, Vector3 threatPosition, Vector3 objectivePosition, float nearObjective, out Vector3 standAt)
    {
        standAt = unit != null ? unit.position : transform.position;
        if (unit == null) return false;
        if (!TryGetCoverSide(threatPosition, out bool useBackFace)) return false;
        EnsureOccupants();

        if ((transform.position - unit.position).sqrMagnitude > maxClaimDistance * maxClaimDistance) return false;

        int existing = FindSlot(unit);
        if (existing >= 0 && SlotIsBack(existing) == useBackFace)
        {
            Vector3 kept = SlotPosition(existing);
            if (FlatDistance(kept, objectivePosition) <= nearObjective)
            {
                standAt = kept;
                standAt.y = unit.position.y;
                return true;
            }
        }

        int best = -1;
        float bestDistance = float.MaxValue;
        int start = useBackFace ? 0 : SideCount();
        int end = useBackFace ? SideCount() : occupants.Length;
        for (int i = start; i < end; i++)
        {
            if (occupants[i] != null && occupants[i] != unit) continue;
            Vector3 slot = SlotPosition(i);
            if (FlatDistance(slot, objectivePosition) > nearObjective) continue;
            float distance = FlatDistance(slot, unit.position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }

        if (best < 0) return false;
        standAt = SlotPosition(best);
        standAt.y = unit.position.y;
        return true;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    void EnsureOccupants()
    {
        int count = SideCount() * 2;
        if (occupants != null && occupants.Length == count) return;

        Transform[] next = new Transform[count];
        if (occupants != null)
        {
            int copy = Mathf.Min(occupants.Length, next.Length);
            for (int i = 0; i < copy; i++) next[i] = occupants[i];
        }

        occupants = next;
    }

    void BuildMarkers()
    {
        if (!showSlotMarkers || markersBuilt) return;
        markersBuilt = true;
        EnsureOccupants();

        Vector3 scale = transform.lossyScale;
        Vector3 markerScale = new Vector3(
            0.45f / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            0.08f / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
            0.45f / Mathf.Max(0.001f, Mathf.Abs(scale.z)));

        for (int i = 0; i < occupants.Length; i++)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "CoverSlot";
            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null) DestroyImmediate(markerCollider);

            marker.transform.SetParent(transform, false);
            marker.transform.position = SlotPosition(i) + Vector3.up * 0.04f;
            marker.transform.localScale = markerScale;

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color gold = new Color(0.92f, 0.78f, 0.28f, 1f);
                Material material = renderer.material;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", gold);
                if (material.HasProperty("_Color")) material.SetColor("_Color", gold);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.92f, 0.78f, 0.28f, 0.9f);
        EnsureOccupants();
        for (int i = 0; i < occupants.Length; i++)
        {
            Gizmos.DrawWireSphere(SlotPosition(i) + Vector3.up * 0.2f, 0.25f);
        }

        Gizmos.color = new Color(0.9f, 0.25f, 0.2f, 0.9f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
    }
}

public static class CoverUse
{
    public static bool TryTakeCover(Transform unit, Vector3 threatPosition, out Vector3 standAt)
    {
        standAt = unit != null ? unit.position : Vector3.zero;
        if (unit == null) return false;

        CoverPiece best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < CoverPiece.ActiveCount; i++)
        {
            CoverPiece piece = CoverPiece.GetActive(i);
            if (piece == null || !piece.HasRoomFor(unit, threatPosition)) continue;

            float maxDistance = piece.maxClaimDistance;
            float distance = (piece.transform.position - unit.position).sqrMagnitude;
            if (distance > maxDistance * maxDistance) continue;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = piece;
        }

        if (best == null) return false;
        return best.TryClaim(unit, threatPosition, out standAt);
    }

    public static void Release(Transform unit)
    {
        if (unit == null) return;
        for (int i = 0; i < CoverPiece.ActiveCount; i++)
        {
            CoverPiece piece = CoverPiece.GetActive(i);
            if (piece != null) piece.Release(unit);
        }
    }

    public static bool IsHolding(Transform unit)
    {
        if (unit == null) return false;
        for (int i = 0; i < CoverPiece.ActiveCount; i++)
        {
            CoverPiece piece = CoverPiece.GetActive(i);
            if (piece != null && piece.Holds(unit)) return true;
        }

        return false;
    }

    public static bool TryFindFightingSpot(Transform unit, Vector3 objectivePosition, Vector3 threatPosition, float nearObjective, out Vector3 standAt)
    {
        standAt = unit != null ? unit.position : Vector3.zero;
        if (unit == null) return false;

        CoverPiece best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < CoverPiece.ActiveCount; i++)
        {
            CoverPiece piece = CoverPiece.GetActive(i);
            if (piece == null) continue;
            if (!piece.TryPreviewStand(unit, threatPosition, objectivePosition, nearObjective, out Vector3 preview)) continue;

            float distance = FlatBetween(unit.position, preview);
            if (piece.Holds(unit)) distance -= 6f;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = piece;
        }

        if (best == null)
        {
            Release(unit);
            return false;
        }

        return best.TryClaim(unit, threatPosition, out standAt);
    }

    static float FlatBetween(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public static CoverStance Evaluate(Transform unit, Transform threat)
    {
        if (unit == null || threat == null) return CoverStance.Exposed;

        CoverPiece held = FindHeldPiece(unit);
        bool protectedSide = held != null && held.Protects(unit, threat.position);
        bool clearShot = HasClearShot(unit.gameObject, threat.gameObject);

        if (protectedSide && clearShot) return CoverStance.InCover;
        if (!clearShot && RayHitsCover(unit, threat)) return CoverStance.Blocked;
        return CoverStance.Exposed;
    }

    public static bool TryGetIncomingModifiers(Transform target, Vector3 shooterPosition, out float damageMultiplier, out float spreadMultiplier)
    {
        damageMultiplier = 1f;
        spreadMultiplier = 1f;
        if (target == null) return false;

        CoverPiece held = FindHeldPiece(target);
        if (held == null || !held.Protects(target, shooterPosition)) return false;

        damageMultiplier = held.incomingDamageMultiplier;
        spreadMultiplier = held.incomingSpreadMultiplier;
        return true;
    }

    static CoverPiece FindHeldPiece(Transform unit)
    {
        for (int i = 0; i < CoverPiece.ActiveCount; i++)
        {
            CoverPiece piece = CoverPiece.GetActive(i);
            if (piece != null && piece.Holds(unit)) return piece;
        }

        return null;
    }

    static bool HasClearShot(GameObject from, GameObject target)
    {
        if (from == null || target == null) return false;
        Combat combat = from.GetComponent<Combat>();
        if (combat != null) return combat.HasLineOfSight(target);
        return !RayHitsCover(from.transform, target.transform);
    }

    static bool RayHitsCover(Transform from, Transform target)
    {
        if (from == null || target == null) return false;

        Vector3 origin = from.position + Vector3.up * 0.5f;
        Vector3 destination = target.position + Vector3.up * 0.5f;
        Vector3 direction = destination - origin;
        float distance = direction.magnitude;
        if (distance < 0.05f) return false;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, distance);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null) continue;
            if (collider.gameObject == from.gameObject || collider.gameObject == target.gameObject) continue;
            if (collider.GetComponent<Projectile>() != null) continue;
            if (CoverPiece.ShotPassesOver(collider, origin.y)) continue;
            if (collider.CompareTag("Cover") || collider.GetComponent<CoverPiece>() != null) return true;
        }

        return false;
    }
}
