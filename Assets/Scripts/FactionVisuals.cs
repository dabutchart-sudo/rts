using UnityEngine;

/// <summary>
/// Single source of truth for faction colours used by battlefield markers and UI helpers.
/// Attacker is red; Defender is blue. Use these colours rather than defining local variants.
/// </summary>
public static class FactionVisuals
{
    public static readonly Color AttackerColor = new Color(0.92f, 0.18f, 0.18f, 1f);
    public static readonly Color DefenderColor = new Color(0.15f, 0.55f, 1f, 1f);
    public static readonly Color NeutralColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    public static Color GetColor(Faction faction)
    {
        switch (faction)
        {
            case Faction.Attacker: return AttackerColor;
            case Faction.Defender: return DefenderColor;
            default: return NeutralColor;
        }
    }
}