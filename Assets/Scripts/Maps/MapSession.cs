public static class MapSession
{
    public const string OriginalId = "original";
    public const string UnsavedId = "unsaved";

    public enum Phase
    {
        Menu,
        Edit,
        Play,
        QuickTest
    }

    public static Phase phase = Phase.Menu;
    public static string selectedId = OriginalId;
    public static PlayableMapDefinition workingCopy;
    public static bool workingCopyDirty;
    public static bool returnAfterMatch;
    public static bool allowLookAround = true;
    public static Faction chosenFaction = Faction.Attacker;
    public static float testSpeed = 10f;
    public static int testRunCount = 3;
    public static int activeTestRuns;
    public static int testRunsFinished;
    public static bool continueTest;
}
