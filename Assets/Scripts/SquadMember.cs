using UnityEngine;

public sealed class SquadMember : MonoBehaviour
{
    [SerializeField] private string squadId;
    [SerializeField] private string squadName;

    public Squad Squad { get; private set; }
    public string SquadId => squadId;
    public string SquadName => squadName;

    public void Assign(Squad squad)
    {
        Squad = squad;
        squadId = squad != null ? squad.SquadId : string.Empty;
        squadName = squad != null ? squad.DisplayName : string.Empty;
    }

    private void OnDestroy()
    {
        if (SquadManager.Instance != null)
        {
            SquadManager.Instance.UnregisterUnit(gameObject);
        }
    }
}
