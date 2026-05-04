using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Tool to generate a NavMesh surface that follows only the painted paths on a Terrain.
/// Usage: Attach to a GameObject, assign the Terrain, set the Path Layer Index, and click "Generate".
/// </summary>
public class TerrainPathToNavMesh : MonoBehaviour
{
    [Header("Settings")]
    public Terrain terrain;
    [Tooltip("The index of the terrain texture layer used for paths (0, 1, 2...)")]
    public int pathLayerIndex = 1;
    [Range(0f, 1f), Tooltip("Weights above this will be considered part of the path.")]
    public float threshold = 0.4f;
    [Tooltip("Slight vertical offset to ensure the mesh sits above the terrain surface.")]
    public float yOffset = 0.05f;

    [Header("Output")]
    public GameObject generatedOverlay;

    [ContextMenu("Generate Path NavMesh")]
    public void Generate()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) { Debug.LogError("[NavTool] No Terrain found! Assign one in the inspector."); return; }

        TerrainData data = terrain.terrainData;
        int res = data.alphamapResolution;
        float[,,] alphamaps = data.GetAlphamaps(0, 0, res, res);

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // We iterate through the alphamap grid
        for (int y = 0; y < res - 1; y++)
        {
            for (int x = 0; x < res - 1; x++)
            {
                // In Unity alphamaps, indexing is [y, x, layer]
                float weight = alphamaps[y, x, pathLayerIndex];
                
                if (weight >= threshold)
                {
                    int startIdx = vertices.Count;
                    
                    // Create a quad for this 'pixel' of the alphamap
                    Vector3 p0 = GetLocalPos(x, y, data);
                    Vector3 p1 = GetLocalPos(x + 1, y, data);
                    Vector3 p2 = GetLocalPos(x, y + 1, data);
                    Vector3 p3 = GetLocalPos(x + 1, y + 1, data);

                    vertices.Add(p0); 
                    vertices.Add(p1); 
                    vertices.Add(p2); 
                    vertices.Add(p3);

                    // Triangle 1
                    triangles.Add(startIdx); 
                    triangles.Add(startIdx + 2); 
                    triangles.Add(startIdx + 1);
                    // Triangle 2
                    triangles.Add(startIdx + 1); 
                    triangles.Add(startIdx + 2); 
                    triangles.Add(startIdx + 3);
                }
            }
        }

        if (vertices.Count == 0)
        {
            Debug.LogWarning($"[NavTool] No path detected for layer {pathLayerIndex} with threshold {threshold}. Check your layer index!");
            return;
        }

        // Cleanup old overlay
        if (generatedOverlay != null) DestroyImmediate(generatedOverlay);

        generatedOverlay = new GameObject("NavMesh_PathOverlay");
        generatedOverlay.transform.position = terrain.transform.position;
        generatedOverlay.transform.rotation = terrain.transform.rotation;
        
        Mesh mesh = new Mesh();
        mesh.name = "TerrainPathMesh";
        // Use 32-bit index buffer to support large terrains
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        generatedOverlay.AddComponent<MeshFilter>().mesh = mesh;
        // Add a MeshCollider so there is actually physical ground to stand on
        generatedOverlay.AddComponent<MeshCollider>().sharedMesh = mesh;
        
        MeshRenderer mr = generatedOverlay.AddComponent<MeshRenderer>();
        // Use URP Lit shader instead of Standard to avoid the 'Pink' material issue in URP projects
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null) urpShader = Shader.Find("Standard"); // Fallback
        
        mr.sharedMaterial = new Material(urpShader);
        mr.sharedMaterial.color = new Color(0f, 1f, 0.2f, 0.4f); // Transparent green
        
        // Configure URP transparency properties if using URP Lit
        if (urpShader.name.Contains("Universal Render Pipeline"))
        {
            mr.sharedMaterial.SetFloat("_Surface", 1); // 1 is Transparent
            mr.sharedMaterial.SetFloat("_Blend", 0);   // 0 is Alpha
            mr.sharedMaterial.SetOverrideTag("RenderType", "Transparent");
            mr.sharedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        
        // Disable shadow casting for the overlay
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Add NavMeshSurface
        NavMeshSurface surface = generatedOverlay.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All; // Changed to All to ensure it picks up the mesh
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        
        // Set the Terrain itself to Not Walkable in the Navigation window manually, 
        // OR we can add a NavMeshModifier to the terrain here.
        NavMeshModifier terrainModifier = terrain.gameObject.GetComponent<NavMeshModifier>();
        if (terrainModifier == null) terrainModifier = terrain.gameObject.AddComponent<NavMeshModifier>();
        terrainModifier.overrideArea = true;
        terrainModifier.area = 1; // 1 is usually 'Not Walkable' by default in Unity

        Debug.Log($"[NavTool] Successfully generated path mesh with {vertices.Count} vertices. Baking NavMesh...");
        
        // Trigger the bake
        surface.BuildNavMesh();
        
        // Optionally hide the overlay mesh renderer in game
        mr.enabled = true; // Keep enabled for editor feedback; user can disable later
    }

    private Vector3 GetLocalPos(int x, int y, TerrainData data)
    {
        float normX = (float)x / (data.alphamapResolution - 1);
        float normY = (float)y / (data.alphamapResolution - 1);
        
        // Terrain heights are stored in [0, 1] relative to size.y
        float height = data.GetInterpolatedHeight(normX, normY);
        
        return new Vector3(normX * data.size.x, height + yOffset, normY * data.size.z);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TerrainPathToNavMesh))]
public class TerrainPathToNavMeshEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TerrainPathToNavMesh script = (TerrainPathToNavMesh)target;
        
        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Path NavMesh", GUILayout.Height(40)))
        {
            script.Generate();
        }
    }
}
#endif
