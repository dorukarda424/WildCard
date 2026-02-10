using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ArenaGenerator : MonoBehaviour
{
    [Header("Arena Settings")]
    public Vector2 arenaSize = new Vector2(160, 240);
    public int layoutSeed = 55; 
    public float buildingDensity = 0.65f;
    public float wallHeight = 22f;
    
    [Header("Colors & Materials")]
    public Color teamAColor = new Color(0, 0.8f, 1f); // Neon Cyan (Blue)
    public Color teamBColor = new Color(1f, 0.1f, 0.1f); // Neon Red

    [Header("Material References")]
    public Material floorMat;
    public Material buildingMat;
    public Material holoAMat;
    public Material holoBMat;
    public Material chromeMat;
    public Material roadMat;
    public Material teamAMat;
    public Material teamBMat;

    private void CreateMaterials()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        // 1. Obsidian Infinity Floor
        floorMat = new Material(litShader);
        floorMat.color = new Color(0.01f, 0.01f, 0.03f);
        floorMat.SetFloat("_Metallic", 1.0f);
        floorMat.SetFloat("_Smoothness", 0.9f);

        // 2. Liquid Chrome Surfaces
        chromeMat = new Material(litShader);
        chromeMat.color = Color.white;
        chromeMat.SetFloat("_Metallic", 1.0f);
        chromeMat.SetFloat("_Smoothness", 1.0f);

        // 3. Cyber Road Matte
        roadMat = new Material(litShader);
        roadMat.color = new Color(0.1f, 0.1f, 0.15f);
        roadMat.SetFloat("_Metallic", 0.8f);
        roadMat.SetFloat("_Smoothness", 0.1f);

        // 4. Brutalist Dark Metal Buildings
        buildingMat = new Material(litShader);
        buildingMat.color = new Color(0.12f, 0.12f, 0.15f);
        buildingMat.SetFloat("_Metallic", 0.6f);
        buildingMat.SetFloat("_Smoothness", 0.25f);

        // 5. Holograms (Blue & Red) - Increased vibrancy
        holoAMat = CreateHoloMat(litShader, teamAColor);
        holoBMat = CreateHoloMat(litShader, teamBColor);

        // Team Glows - Professional Intensity (6.0f)
        // We darken the base color so the glow is punchy but doesn't wash out geometry
        float emissionMult = 6.0f;
        Color baseDark = new Color(0.1f, 0.1f, 0.1f, 1f); 

        teamAMat = new Material(litShader);
        teamAMat.color = baseDark; 
        teamAMat.SetColor("_EmissionColor", teamAColor * emissionMult);
        teamAMat.EnableKeyword("_EMISSION");

        teamBMat = new Material(litShader);
        teamBMat.color = baseDark;
        teamBMat.SetColor("_EmissionColor", teamBColor * emissionMult);
        teamBMat.EnableKeyword("_EMISSION");
    }

    private Material CreateHoloMat(Shader shader, Color color) {
        Material m = new Material(shader);
        m.color = new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 0.25f); 
        if (shader.name.Contains("Universal")) {
            m.SetFloat("_Surface", 1); // Transparent
            m.SetFloat("_Blend", 0); // Alpha blend
            m.SetColor("_EmissionColor", color * 4.5f); // Vibrant but clear
        } else {
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.One);
            m.SetInt("_ZWrite", 0);
        }
        m.EnableKeyword("_EMISSION");
        return m;
    }

    [ContextMenu("Generate Perfect City (Locked)")]
    public void GenerateArena()
    {
        Random.InitState(layoutSeed);

        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        CreateMaterials();
        SetupLightingEnvironment();

        // 1. Foundation
        CreateBoxAt(new Vector3(0, -1f, 0), new Vector3(arenaSize.x, 2, arenaSize.y), "Floor", floorMat);

        // 2. City Streets Grid
        CreateStreetGrid();

        // 3. High-Density Urban Blocks
        CreateDenseCityBlocks();

        // 4. The Citadel (Central Landmark - Split Color)
        CreateCitadelComplex();

        // 5. Skyway Overpasses
        CreateSkywaySystem();

        // 6. Street Clutter
        PopulateUrbanEnvironment();

        // 7. Symmetrical Bases
        CreateFortifiedBases();
        
        // 8. Perimeter walls
        CreateBorderWalls();

        Debug.Log($"'Red vs Blue' City Generated. Seed: {layoutSeed}.");
    }

    private void SetupLightingEnvironment()
    {
        GameObject probeObj = new GameObject("Environment_Probe");
        probeObj.transform.parent = transform;
        probeObj.transform.position = new Vector3(0, 20, 0);
        ReflectionProbe probe = probeObj.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.size = new Vector3(arenaSize.x * 2, 100, arenaSize.y * 2);
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;

        GameObject sunObj = new GameObject("Cyber_Light");
        sunObj.transform.parent = transform;
        sunObj.transform.rotation = Quaternion.Euler(55, -45, 0);
        Light sun = sunObj.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.3f;
    }

    private void CreateStreetGrid()
    {
        for (int i = -4; i <= 4; i++) {
            float x = i * (arenaSize.x / 10f);
            CreateBoxAt(new Vector3(x, 0.05f, 0), new Vector3(1.2f, 0.1f, arenaSize.y), "Street_V", chromeMat);
        }
        for (int i = -6; i <= 6; i++) {
            float z = i * (arenaSize.y / 14f);
            // Color crossroads based on team side
            Material gridMat = z > 0 ? teamBMat : teamAMat;
            CreateBoxAt(new Vector3(0, 0.05f, z), new Vector3(arenaSize.x, 0.1f, 1.2f), "Street_H", gridMat);
        }
    }

    private void CreateDenseCityBlocks()
    {
        int blocksX = 10;
        int blocksZ = 16;
        float stepX = arenaSize.x / blocksX;
        float stepZ = arenaSize.y / blocksZ;

        for (int x = 0; x < blocksX; x++) {
            for (int z = 0; z < blocksZ; z++) {
                float px = (x - blocksX/2f + 0.5f) * stepX;
                float pz = (z - blocksZ/2f + 0.5f) * stepZ;

                if (Mathf.Abs(px) < 22 && Mathf.Abs(pz) < 22) continue; 
                if (Mathf.Abs(pz) > arenaSize.y * 0.4f) continue;

                if (Random.value < buildingDensity) {
                    float h = Random.Range(15, 60);
                    Vector3 size = new Vector3(Random.Range(12, 18), h, Random.Range(12, 18));
                    CreateBoxAt(new Vector3(px, h/2, pz), size, "Building", buildingMat);
                    
                    // Windows - Color coded by side
                    Material sideMat = pz > 0 ? teamBMat : teamAMat;
                    for (int k = 1; k < h/5; k++) {
                        CreateBoxAt(new Vector3(px, k * 5, pz), new Vector3(size.x + 0.3f, 0.6f, size.z + 0.3f), "Window", sideMat);
                    }
                }
            }
        }
    }

    private void CreateCitadelComplex()
    {
        Vector3 pos = Vector3.zero;
        CreateBoxAt(pos + Vector3.up * 5, new Vector3(40, 10, 40), "Citadel_Base", buildingMat);
        
        // Split Central Spire (Hologram Red & Blue)
        GameObject sideA = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sideA.name = "Holo_Blue";
        sideA.transform.parent = transform;
        sideA.transform.position = pos + new Vector3(-4, 25, 0);
        sideA.transform.localScale = new Vector3(8, 25, 8);
        sideA.GetComponent<Renderer>().sharedMaterial = holoAMat;

        GameObject sideB = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sideB.name = "Holo_Red";
        sideB.transform.parent = transform;
        sideB.transform.position = pos + new Vector3(4, 25, 0);
        sideB.transform.localScale = new Vector3(8, 25, 8);
        sideB.GetComponent<Renderer>().sharedMaterial = holoBMat;

        CreateBoxAt(pos + Vector3.up * 1, new Vector3(50, 0.5f, 50), "Plaza", chromeMat);
    }

    private void CreateSkywaySystem()
    {
        for (int i = -2; i <= 2; i++) {
            float z = i * (arenaSize.y / 6f);
            Material sideHolo = z > 0 ? holoBMat : holoAMat;
            CreateBoxAt(new Vector3(0, 16, z), new Vector3(arenaSize.x * 0.85f, 0.5f, 8), "Skyway", buildingMat);
            CreateBoxAt(new Vector3(0, 17, z + 4), new Vector3(arenaSize.x * 0.85f, 1.5f, 0.2f), "Rail", sideHolo);
            CreateBoxAt(new Vector3(0, 17, z - 4), new Vector3(arenaSize.x * 0.85f, 1.5f, 0.2f), "Rail", sideHolo);
        }
    }

    private void PopulateUrbanEnvironment()
    {
        for (int i = 0; i < 300; i++) {
            float rx = Random.Range(-arenaSize.x * 0.45f, arenaSize.x * 0.45f);
            float rz = Random.Range(-arenaSize.y * 0.42f, arenaSize.y * 0.42f);
            Material sideMat = rz > 0 ? teamBMat : teamAMat;

            int type = Random.Range(0, 3);
            switch(type) {
                case 0: CreateBoxAt(new Vector3(rx, 1.5f, rz), new Vector3(6, 3, 1), "Cover", buildingMat); break;
                case 1: CreateBoxAt(new Vector3(rx, 1, rz), new Vector3(2, 2, 2), "Crate", sideMat); break;
                case 2: CreateBoxAt(new Vector3(rx, 6, rz), new Vector3(0.5f, 12, 0.5f), "Pylon", sideMat); break;
            }
        }
    }

    private void CreateFortifiedBases()
    {
        float z = arenaSize.y / 2 - 25;
        CreateBase(new Vector3(0, 0, z), teamBMat, MultiplayerSpawnPoint.Team.TeamB);
        CreateBase(new Vector3(0, 0, -z), teamAMat, MultiplayerSpawnPoint.Team.TeamA);
    }

    private void CreateBase(Vector3 pos, Material mat, MultiplayerSpawnPoint.Team team)
    {
        CreateBoxAt(pos + Vector3.up * 10, new Vector3(60, 20, 35), "HQ", buildingMat);
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.transform.parent = transform;
        pad.transform.position = pos + new Vector3(0, 0.2f, (team == MultiplayerSpawnPoint.Team.TeamB ? -25 : 25));
        pad.transform.localScale = new Vector3(45, 0.5f, 45);
        pad.GetComponent<Renderer>().sharedMaterial = mat;

        for (int i = 0; i < 10; i++) {
            GameObject sp = new GameObject("Spawn");
            sp.transform.parent = pad.transform;
            sp.transform.localPosition = new Vector3((i-5) * 0.12f, 1.5f, 0);
            sp.AddComponent<MultiplayerSpawnPoint>().assignedTeam = team;
        }
    }

    private void CreateBorderWalls()
    {
        float h = wallHeight + 10;
        CreateBoxAt(new Vector3(0, h/2, arenaSize.y/2), new Vector3(arenaSize.x, h, 8), "Border", buildingMat);
        CreateBoxAt(new Vector3(0, h/2, -arenaSize.y/2), new Vector3(arenaSize.x, h, 8), "Border", buildingMat);
        CreateBoxAt(new Vector3(arenaSize.x/2, h/2, 0), new Vector3(8, h, arenaSize.y), "Border", buildingMat);
        CreateBoxAt(new Vector3(-arenaSize.x/2, h/2, 0), new Vector3(8, h, arenaSize.y), "Border", buildingMat);
    }

    private void CreateBoxAt(Vector3 pos, Vector3 scale, string name, Material mat)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.parent = transform;
        box.transform.position = pos;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = mat;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ArenaGenerator))]
public class ArenaGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        ArenaGenerator gen = (ArenaGenerator)target;
        if (GUILayout.Button("GENERATE RED vs BLUE CITY"))
        {
            gen.GenerateArena();
        }
    }
}
#endif
