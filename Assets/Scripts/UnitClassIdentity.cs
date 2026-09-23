using System;
using UnityEngine;

public enum UnitClass
{
    Assault,
    Engineer,
    Recon,
    Support
}

public sealed class UnitClassIdentity : MonoBehaviour
{
    [SerializeField] private UnitClass unitClass = UnitClass.Assault;

    public UnitClass Class => unitClass;

    private void Awake()
    {
        UnitClassMarker.Ensure(gameObject);
    }

    public void SetClass(UnitClass newClass)
    {
        unitClass = newClass;
        UnitClassMarker.Ensure(gameObject);
    }

    public static UnitClass GetClass(GameObject unit, UnitClass fallback = UnitClass.Assault)
    {
        if (unit == null) return fallback;

        UnitClassIdentity identity = unit.GetComponent<UnitClassIdentity>();
        return identity != null ? identity.Class : fallback;
    }

    public static UnitClass InferFromDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return UnitClass.Assault;

        string value = displayName.Trim();

        if (value.IndexOf("engineer", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return UnitClass.Engineer;
        }

        if (value.IndexOf("recon", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("sniper", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("scout", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return UnitClass.Recon;
        }

        if (value.IndexOf("support", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("medic", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return UnitClass.Support;
        }

        return UnitClass.Assault;
    }

    public static UnitClassIdentity Ensure(GameObject unit, UnitClass unitClass)
    {
        if (unit == null) return null;

        UnitClassIdentity identity = unit.GetComponent<UnitClassIdentity>();
        if (identity == null)
        {
            identity = unit.AddComponent<UnitClassIdentity>();
        }

        identity.SetClass(unitClass);
        return identity;
    }
}
