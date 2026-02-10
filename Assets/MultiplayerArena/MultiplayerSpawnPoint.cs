using UnityEngine;

public class MultiplayerSpawnPoint : MonoBehaviour
{
    public enum Team { TeamA, TeamB, Neutral }
    public Team assignedTeam = Team.Neutral;

    private void OnDrawGizmos()
    {
        Gizmos.color = assignedTeam == Team.TeamA ? Color.cyan : (assignedTeam == Team.TeamB ? Color.red : Color.white);
        Gizmos.DrawWireSphere(transform.position, 1f);
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}
