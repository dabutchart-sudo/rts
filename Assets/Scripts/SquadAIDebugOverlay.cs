using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SquadAIDebugOverlay : MonoBehaviour
{
    private static SquadAIDebugOverlay instance;

    [Header("Debug Overlay")]
    [SerializeField] private bool visible = false;
    [SerializeField] private bool showBothFactions = true;
    [SerializeField] private bool drawObjectiveLines = true;
    [SerializeField] private float panelWidth = 520f;
    [SerializeField] private float panelMargin = 14f;
    [SerializeField] private float lineHeight = 20f;

    private GUIStyle panelStyle;
    private GUIStyle headerStyle;
    private GUIStyle bodyStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeOverlay()
    {
        if (instance != null) return;

        SquadAIDebugOverlay existing = FindAnyObjectByType<SquadAIDebugOverlay>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject host = new GameObject("SquadAIDebugOverlay");
        host.AddComponent<SquadAIDebugOverlay>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            visible = !visible;
        }
    }

    private void OnGUI()
    {
        if (!visible) return;
        if (SquadManager.Instance == null) return;

        EnsureStyles();

        List<Squad> squads = GetDisplayedSquads();
        if (squads.Count == 0) return;

        StringBuilder text = new StringBuilder();
        text.AppendLine("SQUAD AI DIAGNOSTICS   [G to close]");

        if (GameManager.Instance != null)
        {
            string playerFaction = GameManager.Instance.playerFaction.ToString();
            string controlMode = ControlModeManager.Instance != null ? ControlModeManager.Instance.CurrentMode.ToString() : "Unknown";
            text.AppendLine($"Sector {GameManager.Instance.currentSectorIndex + 1}   Player: {playerFaction}   Mode: {controlMode}");
        }

        text.AppendLine();

        SquadAIController controller = SquadAIController.EnsureInstance();
        foreach (Squad squad in squads)
        {
            if (squad == null) continue;

            text.AppendLine($"--- {squad.Faction} {squad.DisplayName} ---");
            text.AppendLine(controller != null ? controller.GetDiagnosticReport(squad) : BuildFallbackReport(squad));
            text.AppendLine();
        }

        GUIContent content = new GUIContent(text.ToString());
        float height = Mathf.Min(Screen.height - panelMargin * 2f, bodyStyle.CalcHeight(content, panelWidth - 24f) + 24f);
        Rect panelRect = new Rect(panelMargin, panelMargin, panelWidth, height);

        GUI.Box(panelRect, GUIContent.none, panelStyle);
        Rect textRect = new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 24f, panelRect.height - 20f);
        GUI.Label(textRect, content, bodyStyle);
    }

    private void OnDrawGizmos()
    {
        if (!visible || !drawObjectiveLines || SquadManager.Instance == null) return;

        foreach (Squad squad in GetDisplayedSquads())
        {
            if (squad == null || squad.StrategicObjective == null || squad.MemberCount == 0) continue;

            Vector3 centre = GetSquadCentre(squad);
            Gizmos.color = squad.Faction == Faction.Attacker ? Color.red : Color.blue;
            Gizmos.DrawLine(centre + Vector3.up * 0.4f, squad.StrategicObjective.position + Vector3.up * 0.4f);
            Gizmos.DrawWireSphere(centre + Vector3.up * 0.4f, 0.5f);
        }
    }

    private List<Squad> GetDisplayedSquads()
    {
        List<Squad> result = new List<Squad>();
        if (SquadManager.Instance == null) return result;

        if (showBothFactions || GameManager.Instance == null || GameManager.Instance.playerFaction == Faction.None)
        {
            AddSquads(result, SquadManager.Instance.AttackerSquads);
            AddSquads(result, SquadManager.Instance.DefenderSquads);
            return result;
        }

        if (GameManager.Instance.playerFaction == Faction.Attacker)
        {
            AddSquads(result, SquadManager.Instance.AttackerSquads);
        }
        else
        {
            AddSquads(result, SquadManager.Instance.DefenderSquads);
        }

        return result;
    }

    private void AddSquads(List<Squad> target, IReadOnlyList<Squad> source)
    {
        if (source == null) return;

        foreach (Squad squad in source)
        {
            if (squad != null && squad.MemberCount > 0)
            {
                target.Add(squad);
            }
        }
    }

    private string BuildFallbackReport(Squad squad)
    {
        string objective = squad.StrategicObjective != null ? squad.StrategicObjective.name : "None";
        return $"{squad.DisplayName} [{squad.Role}]  Members {squad.MemberCount}  Order {squad.CurrentOrder}/{squad.CurrentCommandSource}\nObjective: {objective}";
    }

    private Vector3 GetSquadCentre(Squad squad)
    {
        Vector3 total = Vector3.zero;
        int count = 0;

        foreach (GameObject member in squad.Members)
        {
            if (member == null) continue;
            total += member.transform.position;
            count++;
        }

        return count > 0 ? total / count : Vector3.zero;
    }

    private void EnsureStyles()
    {
        if (panelStyle != null) return;

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = Texture2D.whiteTexture;
        panelStyle.normal.textColor = Color.white;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = false,
            richText = false,
            clipping = TextClipping.Clip,
            lineHeight = lineHeight
        };
    }
}
