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

        public Entry(string id, string groupName, string displayName, string modelFileName, bool includeInAutoDress)
        {
            this.id = id;
            this.groupName = groupName;
            this.displayName = displayName;
            this.modelFileName = modelFileName;
            this.includeInAutoDress = includeInAutoDress;
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
        new Entry("shop-wide", "Market", "Wide shop", "low-detail-building-wide-a", false)
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

    public static Entry[] AutoDressEntries()
    {
        var entries = new List<Entry>();
        for (int i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].includeInAutoDress) entries.Add(Entries[i]);
        }

        return entries.ToArray();
    }
}
