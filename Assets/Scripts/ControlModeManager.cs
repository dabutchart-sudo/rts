using System;
using UnityEngine;

public enum ControlMode
{
    Auto,
    Assist,
    Manual
}

public class ControlModeManager : MonoBehaviour
{
    public static ControlModeManager Instance { get; private set; }

    [Header("Control Mode")]
    [Tooltip("AUTO: AI controls the player's faction. ASSIST: AI controls it until the player gives a temporary order. MANUAL: the player gives strategic movement orders and AI objective movement is disabled for the player's faction.")]
    [SerializeField] private ControlMode currentMode = ControlMode.Assist;

    public ControlMode CurrentMode => currentMode;

    public event Action<ControlMode> ModeChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (Instance != null) return;

        ControlModeManager existing = FindFirstObjectByType<ControlModeManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject managerObject = new GameObject("ControlModeManager");
        managerObject.AddComponent<ControlModeManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SetMode(ControlMode mode)
    {
        if (currentMode == mode) return;

        currentMode = mode;
        Debug.Log($"Control mode changed to {currentMode}.");
        ModeChanged?.Invoke(currentMode);
    }

    public void SetAutoMode()
    {
        SetMode(ControlMode.Auto);
    }

    public void SetAssistMode()
    {
        SetMode(ControlMode.Assist);
    }

    public void SetManualMode()
    {
        SetMode(ControlMode.Manual);
    }

    public bool IsPlayerFaction(GameObject unit)
    {
        if (unit == null || GameManager.Instance == null) return false;

        if (GameManager.Instance.playerFaction == Faction.Attacker)
        {
            return unit.CompareTag("Attacker");
        }

        if (GameManager.Instance.playerFaction == Faction.Defender)
        {
            return unit.CompareTag("Defender");
        }

        return false;
    }

    public bool ShouldUseStrategicAI(GameObject unit)
    {
        if (!IsPlayerFaction(unit)) return true;
        return currentMode != ControlMode.Manual;
    }

    public bool CanPlayerSelectUnits()
    {
        return currentMode != ControlMode.Auto;
    }

    public bool CanPlayerIssueOrders()
    {
        return currentMode != ControlMode.Auto;
    }

    public bool PlayerOrdersAreTemporary()
    {
        return currentMode == ControlMode.Assist;
    }
}