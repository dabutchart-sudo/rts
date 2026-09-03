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

    public void SetClass(UnitClass newClass)
    {
        unitClass = newClass;
    }

    public static UnitClass GetClass(GameObject unit, UnitClass fallback = UnitClass.Assault)
    {
        if (unit == null) return fallback;

        UnitClassIdentity identity = unit.GetComponent<UnitClassIdentity>();
        return identity != null ? identity.Class : fallback;
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
