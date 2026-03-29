using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WildCard.Environment.DeathArena
{
    public class DeathArenaManager : MonoBehaviour
    {
        [Header("Arena References")]
        [Tooltip("The massive rotating death ball object.")]
        public DeathBallController deathBall;
        
        [Tooltip("All floor tiles in the arena that can become safe voids.")]
        public List<VoidTile> allFloorTiles;

        [Header("Progression Settings")]
        public float initialBallSpeed = 15f;
        public float maxBallSpeed = 60f;
        [Tooltip("Time in seconds for the ball speed to reach max, and void times to reach min.")]
        public float difficultyRampUpTime = 120f; 

        [Header("Void Settings")]
        [Tooltip("How many voids are open at the same time.")]
        public int simultaneousVoids = 3;
        
        [Tooltip("How long a void stays open initially.")]
        public float initialVoidOpenDuration = 4f;
        [Tooltip("How long a void stays open at maximum difficulty.")]
        public float minVoidOpenDuration = 1.5f;

        [Tooltip("Time between the closing of the old voids and opening of new ones.")]
        public float voidCycleInterval = 6f; 

        private float timer = 0f;
        private float currentBallSpeed;
        private float currentVoidOpenDuration;

        private void Start()
        {
            // Initialize game state
            currentBallSpeed = initialBallSpeed;
            currentVoidOpenDuration = initialVoidOpenDuration;

            if (deathBall != null)
                deathBall.SetSpeed(currentBallSpeed);

            if (allFloorTiles.Count > 0)
                StartCoroutine(VoidCycleRoutine());
            else
                Debug.LogWarning("DeathArenaManager has no Floor Tiles assigned!");
        }

        private void Update()
        {
            // Ramp up difficulty over time
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / difficultyRampUpTime);

            currentBallSpeed = Mathf.Lerp(initialBallSpeed, maxBallSpeed, progress);
            currentVoidOpenDuration = Mathf.Lerp(initialVoidOpenDuration, minVoidOpenDuration, progress);

            if (deathBall != null)
                deathBall.SetSpeed(currentBallSpeed);
        }

        private IEnumerator VoidCycleRoutine()
        {
            // Initial delay before the trap starts
            yield return new WaitForSeconds(2f);

            while (true)
            {
                // Select new random tiles to become voids (excluding already active ones if we were to overlap)
                List<VoidTile> availableTiles = new List<VoidTile>();
                
                // Only consider closed tiles to avoid interrupting current animations
                foreach (var tile in allFloorTiles)
                {
                    if (!tile.IsOpen && !tile.IsWarning)
                    {
                        availableTiles.Add(tile);
                    }
                }

                List<VoidTile> selectedTiles = new List<VoidTile>();
                for (int i = 0; i < simultaneousVoids; i++)
                {
                    if (availableTiles.Count == 0) break;
                    int randomIndex = Random.Range(0, availableTiles.Count);
                    selectedTiles.Add(availableTiles[randomIndex]);
                    availableTiles.RemoveAt(randomIndex); // Prevent selecting the same tile twice
                }

                // Tell selected tiles to enter the warning phase, then open
                foreach (var tile in selectedTiles)
                {
                    tile.WarnAndOpen(currentVoidOpenDuration);
                }

                // Wait for the cycle interval (includes time before they warn + open + close)
                yield return new WaitForSeconds(voidCycleInterval + currentVoidOpenDuration);
            }
        }
    }
}
