using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AutonomousUnit : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform currentObjective;
    private bool isAttacker;
    private bool hasDirectOrder = false;

    [Header("AI Squad Movement")]
    [SerializeField] private float aiSquadSpacing = 2.2f;
    [SerializeField] private float aiNavMeshSampleRadius = 2.5f;

    [Header("Movement Animation")]
    public bool isTank = false;
    public Transform visualTransform;
    public float wobbleSpeed = 14f;
    public float wobbleAngle = 8f;
    public float bobAmount = 0.06f;

    [Header("Tank Trundle Settings")]
    public float trundleSpeed = 8f;
    public float trundlePitchAngle = 1.8f;
    public float trundleRollAngle = 1.2f;
    public float trundleVibration = 0.02f;

    private Vector3 initialVisualLocalPos;
    private Quaternion initialVisualLocalRot;
    private float currentPitch = 0f;
    private float currentRoll = 0f;
    private float currentVibration = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (CompareTag("Attacker")) isAttacker = true;
        else if (CompareTag("Defender")) isAttacker = false;

        if (visualTransform == null)
        {
            Transform vm = transform.Find("VisualMesh");
            if (vm != null) visualTransform = vm;
        }

        if (visualTransform != null)
        {
            initialVisualLocalPos = visualTransform.localPosition;
            initialVisualLocalRot = visualTransform.localRotation;
        }

        Health h = GetComponent<Health>();
        if ((h != null && h.armor == Health.ArmorType.Heavy) || transform.Find("Turret") != null || name.ToLower().Contains("tank"))
        {
            isTank = true;
        }

        UpdateDestination();
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        bool useStrategicAI = ControlModeManager.Instance == null || ControlModeManager.Instance.ShouldUseStrategicAI(gameObject);

        if (!hasDirectOrder && useStrategicAI)
        {
            Transform target = GetAIObjective();
            if (target != currentObjective)
            {
                currentObjective = target;
                ApplySquadOrderState(target);

                if (currentObjective != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.SetDestination(GetStrategicDestination(currentObjective));
                }
                else if (currentObjective == null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
            }
        }
        else if (!hasDirectOrder && !useStrategicAI)
        {
            currentObjective = null;
        }
        else if (hasDirectOrder && agent.enabled && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            bool temporaryOrder = ControlModeManager.Instance == null || ControlModeManager.Instance.PlayerOrdersAreTemporary();
            if (temporaryOrder || !IsPlayerControlledUnit())
            {
                hasDirectOrder = false;
            }
            else
            {
                agent.ResetPath();
            }
        }

        CheckSectorBounds();
    }

    void LateUpdate()
    {
        AnimateMovement();
    }

    private void AnimateMovement()
    {
        if (agent == null) return;

        bool isMoving = agent.enabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 0.05f;
        float moveSpeedFactor = isMoving ? Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(0.1f, agent.speed)) : 0f;

        if (isTank)
        {
            float targetPitch = isMoving ? Mathf.Sin(Time.time * trundleSpeed) * trundlePitchAngle * moveSpeedFactor : 0f;
            float targetRoll = isMoving ? Mathf.Cos(Time.time * (trundleSpeed * 0.6f)) * trundleRollAngle * moveSpeedFactor : 0f;
            float targetVib = isMoving ? Mathf.Sin(Time.time * (trundleSpeed * 2.5f)) * trundleVibration * moveSpeedFactor : 0f;

            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * 10f);
            currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * 10f);
            currentVibration = Mathf.Lerp(currentVibration, targetVib, Time.deltaTime * 10f);

            transform.rotation = transform.rotation * Quaternion.Euler(currentPitch, 0f, currentRoll);
            transform.position += transform.up * currentVibration;
        }
        else if (visualTransform != null)
        {
            if (isMoving)
            {
                float roll = Mathf.Sin(Time.time * wobbleSpeed) * wobbleAngle * moveSpeedFactor;
                float bob = Mathf.Abs(Mathf.Sin(Time.time * wobbleSpeed)) * bobAmount * moveSpeedFactor;

                visualTransform.localRotation = initialVisualLocalRot * Quaternion.Euler(0f, 0f, roll);
                visualTransform.localPosition = initialVisualLocalPos + new Vector3(0f, bob, 0f);
            }
            else
            {
                visualTransform.localRotation = Quaternion.Lerp(visualTransform.localRotation, initialVisualLocalRot, Time.deltaTime * 12f);
                visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, initialVisualLocalPos, Time.deltaTime * 12f);
            }
        }
    }

    public void UpdateDestination()
    {
        if (GameManager.Instance == null) return;

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (agent == null || !agent.isActiveAndEnabled) return;

        bool useStrategicAI = ControlModeManager.Instance == null || ControlModeManager.Instance.ShouldUseStrategicAI(gameObject);
        if (!useStrategicAI)
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            currentObjective = null;
            return;
        }

        Transform target = GetAIObjective();

        if (target != null && agent.isOnNavMesh)
        {
            currentObjective = target;
            ApplySquadOrderState(target);
            agent.SetDestination(GetStrategicDestination(target));
        }
        else if (agent.isOnNavMesh)
        {
            currentObjective = null;
            agent.ResetPath();
        }
    }

    private Transform GetAIObjective()
    {
        SquadAIController controller = SquadAIController.EnsureInstance();
        if (controller != null)
        {
            return controller.GetStrategicTarget(gameObject, isAttacker);
        }

        return GameManager.Instance != null ? GameManager.Instance.GetCurrentTarget(gameObject, isAttacker) : null;
    }

    public void OrderRetreat(Transform retreatPoint)
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (retreatPoint != null && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            hasDirectOrder = true;
            currentObjective = retreatPoint;

            SquadMember squadMember = GetComponent<SquadMember>();
            if (squadMember != null && squadMember.Squad != null)
            {
                squadMember.Squad.SetOrder(SquadOrderType.Retreat, SquadCommandSource.System);
            }

            agent.SetDestination(retreatPoint.position);
        }
    }

    public void MoveToDirectOrder(Vector3 destination)
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (IsPositionWithinActiveBounds(destination))
            {
                hasDirectOrder = true;
                currentObjective = null;

                SquadMember squadMember = GetComponent<SquadMember>();
                if (squadMember != null && squadMember.Squad != null)
                {
                    squadMember.Squad.SetOrder(SquadOrderType.Move, SquadCommandSource.Player);
                }

                agent.SetDestination(destination);
            }
        }
    }

    public void RefreshControlMode()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (!IsPlayerControlledUnit()) return;

        if (ControlModeManager.Instance != null && ControlModeManager.Instance.CurrentMode == ControlMode.Auto)
        {
            hasDirectOrder = false;
            UpdateDestination();
        }
        else if (ControlModeManager.Instance != null && ControlModeManager.Instance.CurrentMode == ControlMode.Manual)
        {
            hasDirectOrder = false;
            currentObjective = null;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        }
    }

    private void ApplySquadOrderState(Transform target)
    {
        SquadMember squadMember = GetComponent<SquadMember>();
        if (squadMember == null || squadMember.Squad == null || target == null) return;

        SquadOrderType order = isAttacker ? SquadOrderType.Attack : SquadOrderType.Defend;
        squadMember.Squad.SetOrder(order, SquadCommandSource.AI);
    }

    private Vector3 GetStrategicDestination(Transform target)
    {
        if (target == null) return transform.position;

        SquadMember squadMember = GetComponent<SquadMember>();
        if (squadMember == null || squadMember.Squad == null || squadMember.Squad.MemberCount <= 1)
        {
            return target.position;
        }

        int slotIndex = GetSquadSlotIndex(squadMember.Squad);
        if (slotIndex < 0) return target.position;

        int memberCount = Mathf.Max(1, squadMember.Squad.MemberCount);
        int columns = Mathf.CeilToInt(Mathf.Sqrt(memberCount));
        int rows = Mathf.CeilToInt(memberCount / (float)columns);

        int row = slotIndex / columns;
        int column = slotIndex % columns;

        float x = (column - (columns - 1) * 0.5f) * aiSquadSpacing;
        float z = (row - (rows - 1) * 0.5f) * aiSquadSpacing;

        Vector3 direction = target.position - GetSquadCentre(squadMember.Squad);
        direction.y = 0f;
        Quaternion rotation = direction.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;

        Vector3 desired = target.position + rotation * new Vector3(x, 0f, z);

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, aiNavMeshSampleRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return target.position;
    }

    private int GetSquadSlotIndex(Squad squad)
    {
        if (squad == null) return -1;

        for (int i = 0; i < squad.Members.Count; i++)
        {
            if (squad.Members[i] == gameObject)
            {
                return i;
            }
        }

        return -1;
    }

    private Vector3 GetSquadCentre(Squad squad)
    {
        if (squad == null || squad.MemberCount == 0) return transform.position;

        Vector3 total = Vector3.zero;
        int count = 0;

        foreach (GameObject member in squad.Members)
        {
            if (member == null) continue;
            total += member.transform.position;
            count++;
        }

        return count > 0 ? total / count : transform.position;
    }

    private bool IsPlayerControlledUnit()
    {
        return ControlModeManager.Instance != null && ControlModeManager.Instance.IsPlayerFaction(gameObject);
    }

    private bool IsPositionWithinActiveBounds(Vector3 pos)
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null) return true;

        if (GameManager.Instance.isTransitioningSector) return true;

        int activeIndex = GameManager.Instance.currentSectorIndex;
        if (activeIndex >= 0 && activeIndex < GameManager.Instance.sectors.Length)
        {
            Sector activeSector = GameManager.Instance.sectors[activeIndex];
            if (activeSector.sectorBounds.size.sqrMagnitude > 0.01f)
            {
                return activeSector.sectorBounds.Contains(pos);
            }
        }
        return true;
    }

    private void CheckSectorBounds()
    {
        if (GameManager.Instance == null || GameManager.Instance.sectors == null) return;

        if (GameManager.Instance.isTransitioningSector) return;

        int activeIndex = GameManager.Instance.currentSectorIndex;
        if (activeIndex < 0 || activeIndex >= GameManager.Instance.sectors.Length) return;

        Sector activeSector = GameManager.Instance.sectors[activeIndex];

        if (activeSector.sectorBounds.size.sqrMagnitude > 0.01f)
        {
            if (!activeSector.sectorBounds.Contains(transform.position))
            {
                Vector3 clampedPos = activeSector.sectorBounds.ClosestPoint(transform.position);

                if (agent.enabled)
                {
                    agent.Warp(clampedPos);
                    if (currentObjective != null && agent.isOnNavMesh)
                    {
                        agent.SetDestination(GetStrategicDestination(currentObjective));
                    }
                }
                else
                {
                    transform.position = clampedPos;
                }
            }
        }
    }
}
