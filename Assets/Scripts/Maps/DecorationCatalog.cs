using System.Collections.Generic;

// Add a line here when the palette grows. A new group name shows up on its own.
// includeInAutoDress is only for the population pass. Hand-placed pieces can use any entry.
public static class DecorationCatalog
{
    public readonly struct Entry
    {
        public readonly string id;
        public readonly string groupName;
        public readonly string displayName;
        public readonly string modelFileName;
        public readonly bool includeInAutoDress;
        public readonly float footprint;

        public Entry(string id, string groupName, string displayName, string modelFileName, bool includeInAutoDress, float footprint = 0.9f)
        {
            this.id = id;
            this.groupName = groupName;
            this.displayName = displayName;
            this.modelFileName = modelFileName;
            this.includeInAutoDress = includeInAutoDress;
            this.footprint = footprint;
        }
    }

    public static readonly Entry[] Entries =
    {
        new Entry("parasol-a", "Market", "Parasol", "detail-parasol-a", true),
        new Entry("parasol-b", "Market", "Parasol B", "detail-parasol-b", true),
        new Entry("awning", "Market", "Awning", "detail-awning", true),
        new Entry("awning-wide", "Market", "Wide awning", "detail-awning-wide", false),
        new Entry("kiosk", "Market", "Kiosk", "low-detail-building-n", true),
        new Entry("shop-a", "Market", "Shop A", "low-detail-building-a", false),
        new Entry("shop-c", "Market", "Shop C", "low-detail-building-c", false),
        new Entry("shop-e", "Market", "Shop E", "low-detail-building-e", false),
        new Entry("shop-k", "Market", "Shop K", "low-detail-building-k", false),
        new Entry("shop-wide", "Market", "Wide shop", "low-detail-building-wide-a", false),
        new Entry("crop-row", "Farm", "Crop row", "", true, 1.3f),
        new Entry("hay-bale", "Farm", "Hay bale", "", true, 0.7f),
        new Entry("fence", "Farm", "Fence", "", true, 1.2f),
        new Entry("barn", "Farm", "Barn", "", false, 1.8f),
        new Entry("silo", "Farm", "Silo", "", false, 1f),
        new Entry("hard-standing", "Industrial", "Hard standing", "", true, 1.55f),
        new Entry("container", "Industrial", "Container", "", true, 1.4f),
        new Entry("container-b", "Industrial", "Container B", "", true, 1.4f),
        new Entry("pallet", "Industrial", "Pallet", "", true, 0.75f),
        new Entry("warehouse", "Industrial", "Warehouse", "", false, 1.9f),
        new Entry("apron", "Airport", "Apron", "", true, 1.55f),
        new Entry("runway", "Airport", "Runway", "", true, 2f),
        new Entry("plane", "Airport", "Plane", "", true, 1.5f),
        new Entry("crate", "Airport", "Crate", "", true, 0.65f),
        new Entry("barrier", "Airport", "Barrier", "", true, 1.2f),
        new Entry("light-mast", "Airport", "Light", "", true, 0.5f),
        new Entry("fuel-tank", "Airport", "Fuel tank", "", true, 1.15f),
        new Entry("hangar", "Airport", "Hangar", "", false, 1.9f),
        new Entry("tower", "Airport", "Control tower", "", false, 0.9f),
        new Entry("terminal", "Airport", "Terminal", "", false, 2.1f),
        new Entry("barricade", "Cover", "Barricade", "", false, 1.2f)
    };

    public static Entry Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return default;
        for (int i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].id == id) return Entries[i];
        }

        return default;
    }

    public static string DisplayName(string id)
    {
        Entry entry = Find(id);
        return string.IsNullOrEmpty(entry.id) ? "piece" : entry.displayName;
    }

    public static string GroupName(MapDistrictKind kind)
    {
        if (kind == MapDistrictKind.Farm) return "Farm";
        if (kind == MapDistrictKind.Industrial) return "Industrial";
        if (kind == MapDistrictKind.Airport) return "Airport";
        return "Market";
    }

    public static Entry[] AutoDressEntries(string groupName)
    {
        var entries = new List<Entry>();
        for (int i = 0; i < Entries.Length; i++)
        {
            if (!Entries[i].includeInAutoDress) continue;
            if (!string.IsNullOrEmpty(groupName) && Entries[i].groupName != groupName) continue;
            entries.Add(Entries[i]);
        }

        return entries.ToArray();
    }

    public static Entry[] AutoDressEntries()
    {
        return AutoDressEntries(null);
    }
}
