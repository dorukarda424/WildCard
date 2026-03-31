using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WildCard.Environment.DeathArena
{
    public class DeathArenaManager : MonoBehaviour
    {
        [Header("Multi-Floor Setup")]
        [Tooltip("Assign the parent objects of each floor grid, ordered from TOP to BOTTOM. You can right-click this component and select 'Find And Sort Floors' to do this automatically!")]
        public List<GameObject> floorParents;

        [HideInInspector]
        public List<VoidTile> allFloorTiles; // Kept for backwards compatibility

        [Header("Progression Settings")]
        [Tooltip("Time in seconds for the void times to reach their fastest speed.")]
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
        private float currentVoidOpenDuration;

        private int cyclesOnCurrentFloor = 0;

        private List<List<VoidTile>> floorsTilesList = new List<List<VoidTile>>();
        private int currentFloorIndex = 0;
        private Transform player;

        [ContextMenu("Find And Sort Floors Automatically")]
        public void FindAllFloors()
        {
#if UNITY_2023_1_OR_NEWER
            VoidTile[] allLevelTiles = FindObjectsByType<VoidTile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            VoidTile[] allLevelTiles = FindObjectsOfType<VoidTile>(true);
#endif
            HashSet<Transform> uniqueParents = new HashSet<Transform>();
            foreach (var t in allLevelTiles)
            {
                if (t.transform.parent != null)
                {
                    uniqueParents.Add(t.transform.parent);
                }
            }

            List<Transform> sortedParents = new List<Transform>(uniqueParents);
            // Sort by Y position descending (top to bottom)
            sortedParents.Sort((a, b) => b.position.y.CompareTo(a.position.y));

            floorParents = new List<GameObject>();
            foreach (var p in sortedParents)
            {
                floorParents.Add(p.gameObject);
            }

            Debug.Log($"Found and sorted {floorParents.Count} floor grids from Top to Bottom!");
        }

        private void Start()
        {
            // Initialize game state
            currentVoidOpenDuration = initialVoidOpenDuration;

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;

            // Auto-fetch if not set up or if they contain nulls (deleted objects)
            if (floorParents == null) 
            {
                floorParents = new List<GameObject>();
            }
            
            floorParents.RemoveAll(parent => parent == null);

            // Magic check: If you copy-pasted in the scene, the manager won't know about the new ones instantly. 
            // We check if the total tiles in the scene exceed what we tracked.
#if UNITY_2023_1_OR_NEWER
            int totalTilesInScene = FindObjectsByType<VoidTile>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
#else
            int totalTilesInScene = FindObjectsOfType<VoidTile>(true).Length;
#endif
            int trackedTiles = 0;
            foreach (var floorGrid in floorParents)
            {
                if (floorGrid != null) trackedTiles += floorGrid.GetComponentsInChildren<VoidTile>(true).Length;
            }

            if (floorParents.Count == 0 || trackedTiles < totalTilesInScene)
            {
                Debug.Log("Detected new unassigned floor grids! Automatically linking all floors to the manager...");
                FindAllFloors();
            }

            foreach (var parent in floorParents)
            {
                if (parent != null)
                {
                    floorsTilesList.Add(new List<VoidTile>(parent.GetComponentsInChildren<VoidTile>(true)));
                }
            }

            if (floorsTilesList.Count > 0 && floorsTilesList[0].Count > 0)
            {
                // Find which floor the player is actually starting on so you don't instantly trigger drops if they start at the bottom!
                if (player != null)
                {
                    for (int i = 0; i < floorParents.Count; i++)
                    {
                        if (floorParents[i] != null && player.position.y <= floorParents[i].transform.position.y + 2f)
                        {
                            currentFloorIndex = i;
                        }
                    }
                }

                StartCoroutine(VoidCycleRoutine());
                Debug.Log($"DeathArenaManager started! Voids active on Floor {currentFloorIndex + 1} out of {floorParents.Count}");
            }
            else
            {
                Debug.LogWarning("DeathArenaManager has no Floor Tiles assigned or found!");
            }
        }

        private void Update()
        {
            // Ramp up difficulty over time
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / difficultyRampUpTime);

            currentVoidOpenDuration = Mathf.Lerp(initialVoidOpenDuration, minVoidOpenDuration, progress);

            // Floor drop logic - check if player fell to the next grid
            if (player != null && currentFloorIndex < floorParents.Count - 1)
            {
                float nextFloorY = floorParents[currentFloorIndex + 1].transform.position.y;
                
                // If player drops close to the next floor height
                if (player.position.y <= nextFloorY + 2f) 
                {
                    AdvanceToNextFloor(nextFloorY);
                }
            }
        }

        private void AdvanceToNextFloor(float nextFloorY)
        {
            if (currentFloorIndex >= floorParents.Count - 1) return;

            currentFloorIndex++;
            cyclesOnCurrentFloor = 0; // Reset cycles for the new floor
            Debug.Log("Player progressed to Floor " + (currentFloorIndex + 1));

            // Mechanism: "and after two openings that top one will be gone"
            // So if we jump from Floor 1 to Floor 2 to Floor 3 (Index 2)... we destroy Index 0.
            int floorToDestroy = currentFloorIndex - 2;
            if (floorToDestroy >= 0 && floorParents[floorToDestroy] != null)
            {
                Destroy(floorParents[floorToDestroy]);
                Debug.Log($"Destroyed top floor behind player: {floorParents[floorToDestroy].name}");
            }
        }

        private IEnumerator VoidCycleRoutine()
        {
            // Initial delay before the trap starts
            yield return new WaitForSeconds(2f);

            while (true)
            {
                if (currentFloorIndex >= floorsTilesList.Count) yield break; // Reached bottom

                List<VoidTile> activeFloorTiles = floorsTilesList[currentFloorIndex];
                
                if (activeFloorTiles == null || activeFloorTiles.Count == 0 || floorParents[currentFloorIndex] == null) 
                {
                    yield return null;
                    continue;
                }

                // Select new random tiles to become voids for current active floor
                List<VoidTile> availableTiles = new List<VoidTile>();
                
                foreach (var tile in activeFloorTiles)
                {
                    // Ensure the tile exists, is fully active, and not already busy
                    if (tile != null && tile.gameObject.activeInHierarchy && !tile.IsOpen && !tile.IsWarning)
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
                    availableTiles.RemoveAt(randomIndex); 
                }

                // Tell selected tiles to enter the warning phase, then open
                foreach (var tile in selectedTiles)
                {
                    if (tile != null) tile.WarnAndOpen(currentVoidOpenDuration);
                }

                // Wait for the cycle interval
                yield return new WaitForSeconds(voidCycleInterval + currentVoidOpenDuration);

                cyclesOnCurrentFloor++;
                Debug.Log($"Floor {currentFloorIndex + 1} has cycled {cyclesOnCurrentFloor} / 2 times.");

                // "after 2 opening it will be gone"
                if (cyclesOnCurrentFloor >= 2)
                {
                    GameObject collapsingFloor = floorParents[currentFloorIndex];
                    float nextFloorY = 0f;
                    
                    if (currentFloorIndex + 1 < floorParents.Count)
                    {
                        nextFloorY = floorParents[currentFloorIndex + 1].transform.position.y;
                        AdvanceToNextFloor(nextFloorY);
                    }
                    
                    if (collapsingFloor != null)
                    {
                        Destroy(collapsingFloor);
                        Debug.Log("Top floor completely destroyed after 2 openings!");
                    }
                }
            }
        }
    }
}
