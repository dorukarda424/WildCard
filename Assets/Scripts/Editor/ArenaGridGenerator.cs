using UnityEngine;
using UnityEditor;
using WildCard.Environment.DeathArena;
using System.Collections.Generic;

namespace WildCard.Editor
{
    public class ArenaGridGenerator : ScriptableWizard
    {
        [Header("Grid Setup")]
        [Tooltip("Number of tiles along the Z axis.")]
        public int rows = 10;
        
        [Tooltip("Number of tiles along the X axis.")]
        public int columns = 10;
        
        [Tooltip("The size of each tile piece. The total width will be columns * tileSize.")]
        public float tileSize = 5f;

        [Header("Visuals")]
        [Tooltip("If you have an industrial material, assign it here to apply it to all tiles automatically.")]
        public Material tileMaterial;

        [Tooltip("Optional: Drop your customized tile prefab here. If null, a basic Cube is generated automatically.")]
        public GameObject optionalTilePrefab;

        [MenuItem("GameObject/Death Arena/Generate Floor Grid")]
        static void CreateWizard()
        {
            // Opens the Wizard pop-up window when the menu item is clicked
            ScriptableWizard.DisplayWizard<ArenaGridGenerator>("Generate Death Arena Grid", "Create Grid");
        }

        void OnWizardCreate()
        {
            GameObject parentGrid = new GameObject("Arena Floor Grid");
            parentGrid.transform.position = Vector3.zero;

            // Calculate start positions to center the entire grid around (0, 0, 0)
            float startX = -(columns * tileSize) / 2f + (tileSize / 2f);
            float startZ = -(rows * tileSize) / 2f + (tileSize / 2f);

            for (int x = 0; x < columns; x++)
            {
                for (int z = 0; z < rows; z++)
                {
                    GameObject tile;
                    if (optionalTilePrefab != null)
                    {
                        // Safely create a prefab instance so it stays linked to the Project view
                        tile = (GameObject)PrefabUtility.InstantiatePrefab(optionalTilePrefab, parentGrid.transform);
                        if (tile == null) tile = Instantiate(optionalTilePrefab, parentGrid.transform);
                    }
                    else
                    {
                        // Generate a basic cube from scratch
                        tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        tile.name = $"VoidTile_{x}_{z}";
                        tile.transform.SetParent(parentGrid.transform);

                        if (tileMaterial != null)
                            tile.GetComponent<Renderer>().sharedMaterial = tileMaterial;

                        // Give it the VoidTile behaviour
                        var voidScript = tile.AddComponent<VoidTile>();
                        voidScript.tileRenderer = tile.GetComponent<Renderer>();
                    }

                    // Scale and position
                    // We keep Y scale as 1 to keep a steady floor depth
                    tile.transform.localScale = new Vector3(tileSize, 1f, tileSize);
                    tile.transform.localPosition = new Vector3(startX + (x * tileSize), 0f, startZ + (z * tileSize));
                }
            }

            // Auto-assign to Arena Manager if one exists in the loaded scene
#if UNITY_2023_1_OR_NEWER
            var manager = FindAnyObjectByType<DeathArenaManager>();
#else
            var manager = FindObjectOfType<DeathArenaManager>();
#endif
            if (manager != null)
            {
                // Register all newly created VoidTiles into the manager script
                manager.allFloorTiles = new List<VoidTile>(parentGrid.GetComponentsInChildren<VoidTile>());
                Debug.Log($"Generated Floor Grid and successfully bound {manager.allFloorTiles.Count} tiles to the DeathArenaManager!");
            }
            else
            {
                Debug.Log("Generated Floor Grid. Don't forget to attach these to your DeathArenaManager script if it's not in the scene yet!");
            }

            // Keep it selected for user convenience
            Selection.activeObject = parentGrid;
        }
    }
}
