using UnityEngine;

/// <summary>
/// Automatically adds and configures MeshColliders for all child meshes.
/// Perfect for imported GLB/GLTF models where Unity's editor colliders fail to save or assign properly.
/// </summary>
public class AutoMeshCollider : MonoBehaviour
{
    void Awake()
    {
        // Find every MeshFilter in this object and its children
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        
        foreach (MeshFilter mf in filters)
        {
            if (mf.sharedMesh != null)
            {
                // Check if a MeshCollider already exists
                MeshCollider mc = mf.gameObject.GetComponent<MeshCollider>();
                if (mc == null)
                {
                    mc = mf.gameObject.AddComponent<MeshCollider>();
                }
                
                // Force the sharedMesh to ensure it actually has collision geometry
                mc.sharedMesh = mf.sharedMesh;
            }
        }
    }
}
