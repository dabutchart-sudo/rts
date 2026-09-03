using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SquadAIDebugOverlay : MonoBehaviour
{
    private static SquadAIDebugOverlay instance;

    [Header("Debug Overlay")]
    [SerializeField] private bool visible = false;
    [SerializeField] private bool showBothFactions = false;
    [SerializeField] private bool drawObjectiveLines = true;
    [SerializeField] private float panelWidth = 520f;
    [SerializeField] private float panelMargin = 14f;
    [SerializeField] private int fontSize = 14;

    [Header("Objective Lines")]
    [SerializeField] private float objectiveLineWidth = 3f;
    [SerializeField] private float objectiveLineAlpha = 0.85f;

    private GUIStyle panelStyle;
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
        if (!visible || SquadManager.Instance == null) return;

        EnsureStyles();

        List<Squad> squads = GetDisplayedSquads();
        if (squads.Count == 0) return;

        if (drawObjectiveLines && Event.current.type == EventType.Repaint)
        {
            DrawObjectiveLinesInGameView(squads);
        }

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

        float usablePanelWidth = Mathf.Min(panelWidth, Mathf.Max(200f, Screen.width - panelMargin * 2f));
        GUIContent content = new GUIContent(text.ToString());
        float contentHeight = bodyStyle.CalcHeight(content, usablePanelWidth - 24f) + 24f;
        float height = Mathf.Min(Screen.height - panelMargin * 2f, contentHeight);

        float panelX = Screen.width - usablePanelWidth - panelMargin;
        Rect panelRect = new Rect(panelX, panelMargin, usablePanelWidth, height);

        GUI.Box(panelRect, GUIContent.none, panelStyle);
        Rect textRect = new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 24f, panelRect.height - 20f);
        GUI.Label(textRect, content, bodyStyle);
    }

    private void DrawObjectiveLinesInGameView(List<Squad> squads)
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        foreach (Squad squad in squads)
        {
            if (squad == null || squad.StrategicObjective == null || squad.MemberCount == 0) continue;

            Vector3 squadCentre = GetSquadCentre(squad) + Vector3.up * 0.4f;
            Vector3 objectivePosition = squad.StrategicObjective.position + Vector3.up * 0.4f;

            Vector3 startScreen = camera.WorldToScreenPoint(squadCentre);
            Vector3 endScreen = camera.WorldToScreenPoint(objectivePosition);

            if (startScreen.z <= 0f || endScreen.z <= 0f) continue;

            Vector2 start = new Vector2(startScreen.x, Screen.height - startScreen.y);
            Vector2 end = new Vector2(endScreen.x, Screen.height - endScreen.y);

            Color lineColor = squad.Faction == Faction.Attacker
                ? new Color(1f, 0.25f, 0.2f, objectiveLineAlpha)
                : new Color(0.2f, 0.55f, 1f, objectiveLineAlpha);

            DrawScreenLine(start, end, lineColor, objectiveLineWidth);
            DrawScreenMarker(end, lineColor, 8f);
        }
    }

    private void DrawScreenLine(Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length < 0.1f) return;

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, length, width), Texture2D.whiteTexture);

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private void DrawScreenMarker(Vector2 centre, Color color, float size)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), Texture2D.whiteTexture);
        GUI.color = previousColor;
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
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            wordWrap = false,
            richText = false,
            clipping = TextClipping.Clip
        };
    }
}
