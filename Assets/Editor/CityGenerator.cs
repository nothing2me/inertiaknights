using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CityGenerator : EditorWindow
{
    private int targetBuildingCount = 60;
    private float cityRadius = 60f;
    private float minSpacing = 12f;
    
    // Noise settings
    private bool useNoiseClustering = true;
    private float noiseScale = 0.05f;
    private float noiseThreshold = 0.4f;
    private float noiseOffset = 0f;

    // Path Terrain Painting Settings
    private bool generatePaths = true;
    private int pathTerrainLayerIndex = 1; // Default to Layer 1 (usually the second texture)
    private float pathWidth = 4f;
    private float pathNoiseAmount = 2.5f;
    private float pathNoiseScale = 0.3f;
    private float pathFadeEdge = 2f;

    private bool snapRotationsTo90 = false;
    private float minScale = 0.85f;
    private float maxScale = 1.15f;
    private Vector3 centerOffset = Vector3.zero;

    private List<GameObject> buildingPrefabs = new List<GameObject>();

    [MenuItem("Tools/Generate City")]
    public static void ShowWindow()
    {
        GetWindow<CityGenerator>("City Generator");
    }

    private void OnEnable()
    {
        LoadPrefabs();
        noiseOffset = Random.Range(0f, 10000f);
    }

    private void LoadPrefabs()
    {
        buildingPrefabs.Clear();
        string[] prefabNames = {
            "rpgpp_lt_building_01",
            "rpgpp_lt_building_02",
            "rpgpp_lt_building_03",
            "rpgpp_lt_building_04",
            "rpgpp_lt_building_05"
        };

        foreach (string name in prefabNames)
        {
            string path = $"Assets/RPGPP_LT/Prefabs/Buildings/Bld_closed/{name}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                buildingPrefabs.Add(prefab);
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Organic City Generator", EditorStyles.boldLabel);

        if (buildingPrefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("Could not load prefabs from Assets/RPGPP_LT/Prefabs/Buildings/Bld_closed. Please ensure the directory and files exist.", MessageType.Warning);
            if (GUILayout.Button("Try Reloading Prefabs"))
            {
                LoadPrefabs();
            }
            return;
        }

        EditorGUILayout.LabelField($"Loaded {buildingPrefabs.Count} building prefabs.", EditorStyles.wordWrappedLabel);

        EditorGUILayout.Space();

        targetBuildingCount = EditorGUILayout.IntSlider("Target Building Count", targetBuildingCount, 5, 500);
        cityRadius = EditorGUILayout.Slider("City Radius", cityRadius, 10f, 300f);
        minSpacing = EditorGUILayout.Slider("Min Spacing Between Buildings", minSpacing, 2f, 30f);
        
        EditorGUILayout.Space();
        
        GUILayout.Label("Perlin Noise Clustering", EditorStyles.boldLabel);
        useNoiseClustering = EditorGUILayout.Toggle("Use Noise", useNoiseClustering);
        if (useNoiseClustering)
        {
            noiseScale = EditorGUILayout.Slider("Noise Scale", noiseScale, 0.01f, 0.2f);
            noiseThreshold = EditorGUILayout.Slider("Noise Threshold", noiseThreshold, 0.1f, 0.9f);
            if (GUILayout.Button("Randomize Noise Seed"))
            {
                noiseOffset = Random.Range(0f, 10000f);
            }
        }

        EditorGUILayout.Space();

        GUILayout.Label("Painted Terrain Paths", EditorStyles.boldLabel);
        generatePaths = EditorGUILayout.Toggle("Paint Paths on Terrain", generatePaths);
        if (generatePaths)
        {
            pathTerrainLayerIndex = EditorGUILayout.IntField("Path Terrain Layer Index", pathTerrainLayerIndex);
            pathWidth = EditorGUILayout.Slider("Base Path Width", pathWidth, 1f, 15f);
            pathNoiseAmount = EditorGUILayout.Slider("Brush Noise Jitter", pathNoiseAmount, 0f, 10f);
            pathNoiseScale = EditorGUILayout.Slider("Brush Noise Scale", pathNoiseScale, 0.01f, 2f);
            pathFadeEdge = EditorGUILayout.Slider("Brush Soft Edge", pathFadeEdge, 0.1f, 10f);
            EditorGUILayout.HelpBox("This directly modifies your Terrain's splatmap using Undo. Make sure 'Path Terrain Layer Index' points to your path texture!", MessageType.Info);
        }

        EditorGUILayout.Space();
        
        snapRotationsTo90 = EditorGUILayout.Toggle("Snap Rotations to 90°", snapRotationsTo90);
        minScale = EditorGUILayout.Slider("Min Scale", minScale, 0.5f, 1f);
        maxScale = EditorGUILayout.Slider("Max Scale", maxScale, 1f, 2f);

        centerOffset = EditorGUILayout.Vector3Field("City Center Position", centerOffset);

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Organic City", GUILayout.Height(40)))
        {
            GenerateCity();
        }
    }

    private void GenerateCity()
    {
        if (buildingPrefabs.Count == 0) return;

        GameObject cityParent = new GameObject("GeneratedOrganicCity");
        cityParent.transform.position = centerOffset;
        Undo.RegisterCreatedObjectUndo(cityParent, "Generate City");

        List<Vector2> placedPositions = new List<Vector2>();
        int maxAttempts = targetBuildingCount * 100; // Allow lots of attempts to fit buildings
        int attempts = 0;

        while (placedPositions.Count < targetBuildingCount && attempts < maxAttempts)
        {
            attempts++;

            // Random point within a circle radius
            Vector2 randomPoint = Random.insideUnitCircle * cityRadius;
            
            // Check noise cluster if enabled
            if (useNoiseClustering)
            {
                float sampleX = (randomPoint.x + centerOffset.x) * noiseScale + noiseOffset;
                float sampleY = (randomPoint.y + centerOffset.z) * noiseScale + noiseOffset;
                float noiseValue = Mathf.PerlinNoise(sampleX, sampleY);

                if (noiseValue < noiseThreshold)
                {
                    continue; 
                }
            }

            // Check distance against already placed buildings to prevent clipping
            bool tooClose = false;
            foreach (Vector2 pos in placedPositions)
            {
                if (Vector2.Distance(randomPoint, pos) < minSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                placedPositions.Add(randomPoint);

                Vector3 position = new Vector3(randomPoint.x, 0, randomPoint.y) + centerOffset;

                GameObject prefab = buildingPrefabs[Random.Range(0, buildingPrefabs.Count)];
                GameObject building = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                
                building.transform.position = position;
                building.transform.SetParent(cityParent.transform);

                if (snapRotationsTo90)
                {
                    float[] angles = { 0f, 90f, 180f, 270f };
                    building.transform.rotation = Quaternion.Euler(0, angles[Random.Range(0, angles.Length)], 0);
                }
                else
                {
                    building.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                }

                float scale = Random.Range(minScale, maxScale);
                building.transform.localScale = new Vector3(scale, scale, scale);
            }
        }

        if (generatePaths && placedPositions.Count > 1)
        {
            PaintPathsOnTerrain(placedPositions);
        }
        
        Debug.Log($"Generated {placedPositions.Count} buildings in GeneratedOrganicCity at {centerOffset}.");
    }

    private void PaintPathsOnTerrain(List<Vector2> positions)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogWarning("No active Terrain found! Cannot paint paths. Please ensure there is a Terrain in the scene.");
            return;
        }

        TerrainData tData = terrain.terrainData;

        if (pathTerrainLayerIndex < 0 || pathTerrainLayerIndex >= tData.alphamapLayers)
        {
            Debug.LogWarning($"Path Terrain Layer Index {pathTerrainLayerIndex} is out of bounds. The terrain only has {tData.alphamapLayers} layers.");
            return;
        }

        // Register the TerrainData for Undo!
        Undo.RegisterCompleteObjectUndo(tData, "Paint City Paths");

        int mapWidth = tData.alphamapWidth;
        int mapHeight = tData.alphamapHeight;
        float[,,] alphamap = tData.GetAlphamaps(0, 0, mapWidth, mapHeight);

        // Build list of lines connecting the buildings
        List<KeyValuePair<Vector2, Vector2>> lines = new List<KeyValuePair<Vector2, Vector2>>();
        HashSet<string> generatedEdges = new HashSet<string>();

        for (int i = 0; i < positions.Count; i++)
        {
            List<KeyValuePair<int, float>> distances = new List<KeyValuePair<int, float>>();
            for (int j = 0; j < positions.Count; j++)
            {
                if (i == j) continue;
                distances.Add(new KeyValuePair<int, float>(j, Vector2.Distance(positions[i], positions[j])));
            }
            distances.Sort((a, b) => a.Value.CompareTo(b.Value));

            // Connect to nearest 2 neighbors
            int connections = Mathf.Min(2, distances.Count);
            for (int k = 0; k < connections; k++)
            {
                int neighborIdx = distances[k].Key;
                int minIdx = Mathf.Min(i, neighborIdx);
                int maxIdx = Mathf.Max(i, neighborIdx);
                string edgeId = $"{minIdx}_{maxIdx}";

                if (generatedEdges.Contains(edgeId)) continue;
                generatedEdges.Add(edgeId);

                lines.Add(new KeyValuePair<Vector2, Vector2>(
                    positions[i] + new Vector2(centerOffset.x, centerOffset.z), 
                    positions[neighborIdx] + new Vector2(centerOffset.x, centerOffset.z)));
            }
        }

        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = tData.size;

        // Iterate over all alphamap pixels
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                // Convert splatmap coordinate to world coordinate
                float worldX = terrainPos.x + ((float)x / mapWidth) * terrainSize.x;
                float worldZ = terrainPos.z + ((float)y / mapHeight) * terrainSize.z;
                Vector2 worldPoint = new Vector2(worldX, worldZ);

                // Find minimum distance to any road segment
                float minDist = float.MaxValue;
                foreach (var line in lines)
                {
                    float dist = DistancePointLine(worldPoint, line.Key, line.Value);
                    if (dist < minDist) minDist = dist;
                }

                // Add Perlin Noise to the brush to make it look organic
                float noiseVal = Mathf.PerlinNoise(worldX * pathNoiseScale, worldZ * pathNoiseScale) * 2f - 1f; // -1 to 1
                float noisyWidth = pathWidth + (noiseVal * pathNoiseAmount);

                if (minDist <= noisyWidth)
                {
                    // Calculate brush alpha with soft edges
                    float alpha = 1f;
                    if (pathFadeEdge > 0.01f && minDist > noisyWidth - pathFadeEdge)
                    {
                        alpha = 1f - ((minDist - (noisyWidth - pathFadeEdge)) / pathFadeEdge);
                    }

                    // Apply alpha to the terrain alphamap
                    float existingWeight = alphamap[y, x, pathTerrainLayerIndex];
                    float newWeight = Mathf.Max(existingWeight, alpha); // Keep highest weight to prevent erasing existing paths
                    
                    // Normalize the other layers
                    float remainingWeight = 1f - newWeight;
                    float currentOtherWeight = 1f - existingWeight;

                    if (currentOtherWeight > 0.001f)
                    {
                        for (int l = 0; l < tData.alphamapLayers; l++)
                        {
                            if (l == pathTerrainLayerIndex) continue;
                            alphamap[y, x, l] = alphamap[y, x, l] * (remainingWeight / currentOtherWeight);
                        }
                    }
                    else
                    {
                        for (int l = 0; l < tData.alphamapLayers; l++)
                        {
                            if (l == pathTerrainLayerIndex) continue;
                            alphamap[y, x, l] = 0f;
                        }
                    }

                    alphamap[y, x, pathTerrainLayerIndex] = newWeight;
                }
            }
        }

        tData.SetAlphamaps(0, 0, alphamap);
        Debug.Log("Successfully painted paths onto the terrain splatmap.");
    }

    private float DistancePointLine(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        float proj = Vector2.Dot(ap, ab);
        float abLenSq = ab.sqrMagnitude;
        float d = proj / abLenSq;
        Vector2 closest;
        if (d <= 0f) closest = a;
        else if (d >= 1f) closest = b;
        else closest = a + ab * d;
        return Vector2.Distance(p, closest);
    }
}
