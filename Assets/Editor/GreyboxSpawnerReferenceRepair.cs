#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Repairs legacy GameManager spawner references in GreyboxBattlefield01.
/// Older generated scene data could contain a GameObject reference in fields that now
/// require UnitSpawner components. Unity nulls those mismatched references on deserialize.
/// This tool explicitly writes the correct component references back into the scene.
/// </summary>
public static class GreyboxSpawnerReferenceRepair
{
    private const string SceneName = "GreyboxBattlefield01";

    [MenuItem("RTS/Maps/Repair Greybox Spawner References")]
    public static void Repair()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != SceneName)
        {
            EditorUtility.DisplayDialog(
                "Greybox Battlefield 01",
                "Open GreyboxBattlefield01 first, then run this command again.",
                "OK");
            return;
        }

        GameManager gameManager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gameManager == null)
        {
            Debug.LogError("SPAWNER REPAIR: GameManager not found.");
            return;
        }

        UnitSpawner attacker = FindSpawner("AttackerSpawner", false);
        UnitSpawner defender = FindSpawner("DefenderSpawner", true);

        if (attacker == null || defender == null)
        {
            Debug.LogError(
                $"SPAWNER REPAIR: Could not resolve both spawners. " +
                $"Attacker={(attacker != null ? attacker.name : "MISSING")}, " +
                $"Defender={(defender != null ? defender.name : "MISSING")}.");
            return;
        }

        attacker.isDefenderSpawner = false;
        defender.isDefenderSpawner = true;
        EditorUtility.SetDirty(attacker);
        EditorUtility.SetDirty(defender);

        // Use SerializedObject deliberately here. This overwrites the stored scene reference
        // with the UnitSpawner component itself, eliminating the legacy GameObject/type mismatch.
        SerializedObject serializedGameManager = new SerializedObject(gameManager);
        SerializedProperty attackerProperty = serializedGameManager.FindProperty("attackerSpawner");
        SerializedProperty defenderProperty = serializedGameManager.FindProperty("defenderSpawner");

        if (attackerProperty == null || defenderProperty == null)
        {
            Debug.LogError("SPAWNER REPAIR: GameManager spawner serialized fields were not found.");
            return;
        }

        attackerProperty.objectReferenceValue = attacker;
        defenderProperty.objectReferenceValue = defender;
        serializedGameManager.ApplyModifiedPropertiesWithoutUndo();

        // Also set the runtime fields explicitly so the Inspector and current scene state agree.
        gameManager.attackerSpawner = attacker;
        gameManager.defenderSpawner = defender;
        EditorUtility.SetDirty(gameManager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"SPAWNER REPAIR: Complete. GameManager now references UnitSpawner components: " +
            $"Attacker={attacker.name}, Defender={defender.name}. Scene saved.");
    }

    private static UnitSpawner FindSpawner(string preferredObjectName, bool defender)
    {
        GameObject preferred = GameObject.Find(preferredObjectName);
        if (preferred != null)
        {
            UnitSpawner component = preferred.GetComponent<UnitSpawner>();
            if (component != null) return component;
        }

        UnitSpawner[] spawners = Object.FindObjectsByType<UnitSpawner>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (UnitSpawner spawner in spawners)
        {
            if (spawner == null) continue;
            if (spawner.isDefenderSpawner == defender) return spawner;
        }

        return null;
    }
}
#endif
