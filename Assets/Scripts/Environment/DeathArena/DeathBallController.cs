using UnityEngine;

namespace WildCard.Environment.DeathArena
{
    public class DeathBallController : MonoBehaviour
    {
        [Header("Arena Bounds")]
        public float arenaLimit = 25f;

        [Header("Visuals (Unbreakable)")]
        [Tooltip("Make sure your Spike Ball model is dragged here!")]
        public Transform visualMesh;
        public float actualBallRadius = 7.5f;

        [Header("Damage Settings")]
        public float damageAmount = 100f; 

        private float currentSpeed = 0f;
        private Transform playerTarget;
        private Vector3 currentDirection;
        private float lockedHeightY;

        // An invisible rotator we create mathematically so it never breaks
        private GameObject forcedRotator;

        private void Start()
        {
            lockedHeightY = transform.position.y;

            // Find the player! Make sure your player object is tagged "Player" in Unity!
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTarget = p.transform;

            if (actualBallRadius <= 0.1f) actualBallRadius = 1f;

            // --- ULTIMATE ROTATION FIX ---
            // If the user provided a visual mesh, we forcefully inject a new rotation pivot at runtime
            // completely disconnecting it from any broken Physics or Animator components!
            if (visualMesh != null)
            {
                forcedRotator = new GameObject("Forced_Visual_Rotator");
                forcedRotator.transform.SetParent(transform);
                
                // 3D Models often have their 'pivot point' at the very bottom (touching the floor).
                // If we spin the bottom point, the ball acts like a pendulum and swings through the floor!
                // FIX: Force the rotator to sit exactly at the mathematical center of your sphere!
                forcedRotator.transform.position = transform.position + (Vector3.up * actualBallRadius);

                // Lock the Mesh securely inside this new pristine rotator
                visualMesh.SetParent(forcedRotator.transform, true);
            }

            // Start rolling randomly initially
            float angle = Random.Range(0f, 360f);
            currentDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
        }

        private void Update()
        {
            float moveDistance = currentSpeed * Time.deltaTime;

            // 1. SEEK THE PLAYER (Creates intense pressure to use the Voids!)
            if (playerTarget != null)
            {
                Vector3 toPlayer = (playerTarget.position - transform.position);
                toPlayer.y = 0f; 
                
                if (toPlayer.sqrMagnitude > 0.1f)
                {
                    // The boulder actively hunts the player down!
                    // If the player drops into a void, the boulder will roll right over their head!
                    currentDirection = Vector3.RotateTowards(currentDirection, toPlayer.normalized, 3f * Time.deltaTime, 0f);
                }
            }

            // 2. Move physically
            Vector3 pos = transform.position;
            pos += currentDirection * moveDistance;

            // Soft wall clamps so it smoothly turns around if it ever hits a wall
            if (Mathf.Abs(pos.x) + actualBallRadius > arenaLimit || Mathf.Abs(pos.z) + actualBallRadius > arenaLimit)
            {
                 currentDirection = Vector3.RotateTowards(currentDirection, (Vector3.zero - pos).normalized, 10f * Time.deltaTime, 0f);
            }
            
            // Constrain tightly to the floor height (so it crosses completely over voids instead of falling in!)
            pos.y = lockedHeightY;
            pos.x = Mathf.Clamp(pos.x, -arenaLimit + actualBallRadius, arenaLimit - actualBallRadius);
            pos.z = Mathf.Clamp(pos.z, -arenaLimit + actualBallRadius, arenaLimit - actualBallRadius);
            transform.position = pos;

            // 3. THE UNBREAKABLE VISUAL ROTATION
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, currentDirection).normalized; 
            float rollAngle = (moveDistance / (2f * Mathf.PI * actualBallRadius)) * 360f;
            
            if (forcedRotator != null)
            {
                // This rotates our purely mathematical empty object, which physically forces the spike ball children to roll perfectly
                forcedRotator.transform.Rotate(rotationAxis, rollAngle, Space.World);
            }
            else
            {
                // Fallback
                transform.Rotate(rotationAxis, rollAngle, Space.World);
            }
        }

        public void SetSpeed(float speed)
        {
            currentSpeed = speed;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                Debug.Log("Player crushed by the Death Boulder!");
            }
        }
    }
}
