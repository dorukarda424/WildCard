using UnityEngine;
using Photon.Pun;

namespace WildCard.Environment
{
    /// <summary>
    /// Trigger collider that instantly kills any player who enters it.
    /// Usage: Attach to a GameObject with a Collider set to "Is Trigger".
    /// Works for fall-off zones, lava, out-of-bounds, etc.
    /// Only the local player processes damage to avoid double-kills over the network.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        [Tooltip("Damage dealt — set very high to guarantee instant death.")]
        [SerializeField] private float damage = 99999f;

        private void OnTriggerEnter(Collider other)
        {
            // Only the owning client should process the kill
            var health = other.GetComponentInParent<PlayerHealth>();
            if (health == null) return;

            var pv = health.GetComponent<PhotonView>();
            if (pv != null && !pv.IsMine) return;

            // Use RPC so ALL clients (including MasterClient) process the death.
            // TakeDamageLocal only ran locally, which meant non-MasterClient
            // deaths were never broadcast and the round never ended.
            // -1 = environment kill (no killer credit)
            health.TakeDamageFromNetwork(damage, -1);
        }
    }
}
