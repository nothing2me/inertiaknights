using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;

public class NPCSetupTool : EditorWindow
{
    [MenuItem("Tools/Setup NavMesh NPC")]
    public static void SetupNPC()
    {
        // 1. Try to find an existing NPC to clone
        GameObject source = GameObject.Find("NPC");
        if (source == null) source = GameObject.Find("Static NPC");
        
        // Fallback: try to find anything with "NPC" in the name
        if (source == null)
        {
            GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject go in allObjects)
            {
                if (go.name.Contains("NPC"))
                {
                    source = go;
                    break;
                }
            }
        }

        if (source == null)
        {
            Debug.LogError("[NPC Tool] Could not find any object with 'NPC' in its name in the scene to use as a template!");
            return;
        }

        // 2. Clone it
        GameObject newNPC = GameObject.Instantiate(source);
        newNPC.name = "PathWalking_NPC_" + Random.Range(10, 99);
        Undo.RegisterCreatedObjectUndo(newNPC, "Create Path NPC");
        
        // Ensure it's active
        newNPC.SetActive(true);

        // 3. CLEANUP: Strip away ALL scripts except the ones we need for the NPC to look and act right
        MonoBehaviour[] scripts = newNPC.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null) continue;
            string n = script.GetType().Name;
            // Only keep the AI and Visuals
            if (n != "NavMeshAgent" && n != "NPCPathWalker" && n != "NPCDirectionalBillboard" && n != "BillboardSpriteAnimator")
            {
                DestroyImmediate(script);
            }
        }

        // 4. Setup Rigidbody (Physics-based grounding as requested)
        Rigidbody rb = newNPC.GetComponent<Rigidbody>();
        if (rb == null) rb = newNPC.AddComponent<Rigidbody>();
        rb.isKinematic = false; // Allow physics to hit the collider
        rb.useGravity = true;   // Let it 'fall' to the surface
        rb.constraints = RigidbodyConstraints.FreezeRotation; // Stop it from rolling away

        // 5. Setup NavMeshAgent
        NavMeshAgent agent = newNPC.GetComponent<NavMeshAgent>();
        if (agent == null) agent = newNPC.AddComponent<NavMeshAgent>();
        agent.baseOffset = 0.5f; 
        agent.speed = 3.5f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;

        // 6. Add our custom Wander script (now with Anti-Fall logic)
        if (newNPC.GetComponent<NPCPathWalker>() == null)
        {
            newNPC.AddComponent<NPCPathWalker>();
        }

        // 7. Warp to NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(source.transform.position, out hit, 100f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            Debug.Log($"[NPC Tool] Warp-snapped {newNPC.name} to path at {hit.position}");
        }
        else
        {
            Debug.LogWarning("[NPC Tool] Warning: Could not find any NavMesh nearby. Is your path baked?");
        }

        Selection.activeGameObject = newNPC;
    }
}
#endif
