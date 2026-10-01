using UnityEngine;

public enum UnitCategory
{
    Infantry,
    Vehicle,
    Other
}

public sealed class UnitCategoryIdentity : MonoBehaviour
{
    [SerializeField] private UnitCategory category = UnitCategory.Infantry;

    public UnitCategory Category => category;

    public void SetCategory(UnitCategory newCategory)
    {
        category = newCategory;
    }

    public static UnitCategoryIdentity Ensure(GameObject unit, UnitCategory category)
    {
        if (unit == null) return null;

        UnitCategoryIdentity identity = unit.GetComponent<UnitCategoryIdentity>();
        if (identity == null)
        {
            identity = unit.AddComponent<UnitCategoryIdentity>();
        }

        identity.SetCategory(category);
        return identity;
    }
}
