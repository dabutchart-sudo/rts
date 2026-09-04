using TMPro;
using UnityEngine;

/// <summary>
/// Stores temporary Recon spotting state on a combat unit. Spotting is faction-aware:
/// an enemy can be spotted for Attackers, Defenders, or both independently.
/// </summary>
public sealed class SpottedTarget : MonoBehaviour
{
    private float spottedForAttackersUntil;
    private float spottedForDefendersUntil;
    private GameObject attackerMarker;
    private GameObject defenderMarker;

    public void MarkSpotted(Faction observingFaction, float duration)
    {
        float expiry = Time.time + Mathf.Max(0.1f, duration);
        if (observingFaction == Faction.Attacker)
        {
            spottedForAttackersUntil = Mathf.Max(spottedForAttackersUntil, expiry);
            EnsureMarker(Faction.Attacker);
        }
        else if (observingFaction == Faction.Defender)
        {
            spottedForDefendersUntil = Mathf.Max(spottedForDefendersUntil, expiry);
            EnsureMarker(Faction.Defender);
        }
    }

    public bool IsSpottedFor(Faction faction)
    {
        if (faction == Faction.Attacker) return Time.time < spottedForAttackersUntil;
        if (faction == Faction.Defender) return Time.time < spottedForDefendersUntil;
        return false;
    }

    private void Update()
    {
        UpdateMarker(attackerMarker, IsSpottedFor(Faction.Attacker));
        UpdateMarker(defenderMarker, IsSpottedFor(Faction.Defender));
    }

    private void EnsureMarker(Faction faction)
    {
        if (faction == Faction.Attacker && attackerMarker != null) return;
        if (faction == Faction.Defender && defenderMarker != null) return;

        GameObject marker = new GameObject(faction == Faction.Attacker ? "SpottedForAttackers" : "SpottedForDefenders");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(0f, 3.15f, 0f);
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = Vector3.one * 0.45f;

        TextMeshPro text = marker.AddComponent<TextMeshPro>();
        text.text = "◆";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 9f;
        text.fontStyle = FontStyles.Bold;
        text.color = FactionVisuals.GetColor(faction);
        text.enableAutoSizing = false;
        text.raycastTarget = false;
        text.sortingOrder = 40;
        marker.AddComponent<Billboard>();

        if (faction == Faction.Attacker) attackerMarker = marker;
        else defenderMarker = marker;
    }

    private void UpdateMarker(GameObject marker, bool active)
    {
        if (marker != null && marker.activeSelf != active) marker.SetActive(active);
    }

    public static SpottedTarget GetOrCreate(GameObject unit)
    {
        if (unit == null) return null;
        SpottedTarget spotted = unit.GetComponent<SpottedTarget>();
        if (spotted == null) spotted = unit.AddComponent<SpottedTarget>();
        return spotted;
    }
}