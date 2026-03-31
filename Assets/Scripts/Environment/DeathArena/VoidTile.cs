using System.Collections;
using UnityEngine;

namespace WildCard.Environment.DeathArena
{
    public class VoidTile : MonoBehaviour
    {
        [Header("Visual Indicators")]
        public Renderer tileRenderer;
        public Color safeColor = Color.gray; // Normal default floor
        public Color warningColor = Color.yellow; // About to open
        [Tooltip("The color it burns when completely open so players can see the edges.")]
        public Color openGlowColor = Color.red; 

        // Particle system in case you attach one
        [Tooltip("Particle effect explicitly playing out warning phase.")]
        public ParticleSystem warningParticles;

        [Header("Motion Sequence")]
        [Tooltip("How far the floor tile drops downward to create the hole.")]
        public float dropDepth = 5f;
        [Tooltip("How fast it drops/lifts back.")]
        public float dropSpeed = 15f; 
        
        // Property trackers
        public bool IsOpen { get; private set; }
        public bool IsWarning { get; private set; }

        private Vector3 originalPosition;
        private Coroutine currentRoutine;
        private Material tileMaterial;

        private void Awake()
        {
            originalPosition = transform.position;

            if (tileRenderer != null)
            {
                tileMaterial = tileRenderer.material; // Gets instance to avoid editing shared material
                tileMaterial.EnableKeyword("_EMISSION");
            }
        }
        
        private void Start()
        {
            // Preset visuals
            SetVisuals(safeColor, false);
        }

        public void WarnAndOpen(float openDuration)
        {
            if (currentRoutine != null) StopCoroutine(currentRoutine);
            currentRoutine = StartCoroutine(WarnAndOpenRoutine(openDuration));
        }

        public void CloseVoid()
        {
            if (currentRoutine != null) StopCoroutine(currentRoutine);
            currentRoutine = StartCoroutine(MoveToPosition(originalPosition, false));
            SetVisuals(safeColor, false);
            IsOpen = false;
            IsWarning = false;
        }

        private IEnumerator WarnAndOpenRoutine(float openDuration)
        {
            // 1. Warning Phase (1.5 seconds)
            IsWarning = true;
            SetVisuals(warningColor, true);

            if (warningParticles != null) 
                warningParticles.Play();

            // Perform an oscillating "pulse" on the emission for tension
            float warningTimer = 1.5f;
            while (warningTimer > 0)
            {
                warningTimer -= Time.deltaTime;
                float pulse = Mathf.PingPong(Time.time * 8f, 1f); // Quick throbbing
                if (tileMaterial != null) 
                    tileMaterial.SetColor("_EmissionColor", warningColor * pulse * 2f);
                yield return null;
            }

            // 2. Open Phase: Drops down into the "Void" 
            IsWarning = false;
            IsOpen = true;

            // Optional: intense glowing red rim indicating active opening underneath
            if (tileMaterial != null) 
                tileMaterial.SetColor("_EmissionColor", openGlowColor * 3f); 
            
            // Drop sequence (Floor slides downwards safely dropping players attached)
            Vector3 targetPosition = originalPosition - new Vector3(0, dropDepth, 0);
            yield return StartCoroutine(MoveToPosition(targetPosition, true));

            // Wait while staying open avoiding sweeping Death Ball
            yield return new WaitForSeconds(openDuration);

            // 3. Complete and slowly rise back (Close)
            CloseVoid();
        }

        private IEnumerator MoveToPosition(Vector3 targetPos, bool isOpening)
        {
            while (Vector3.Distance(transform.position, targetPos) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, dropSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = targetPos;
        }

        private void SetVisuals(Color outlineColor, bool isWarning)
        {
            if (tileMaterial != null)
            {
                // To keep minimal aesthetic - base color shifts slightly, and we utilize emission deeply
                tileMaterial.color = outlineColor;
                tileMaterial.SetColor("_EmissionColor", isWarning ? outlineColor * 1.5f : Color.black);
            }
        }
    }
}
