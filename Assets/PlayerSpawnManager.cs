using UnityEngine;

/// <summary>
/// Manages random spawn positions on a specific "Spawn Platform" object.
/// </summary>
public class PlayerSpawnManager : MonoBehaviour
{
    public static PlayerSpawnManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [Tooltip("The platform transform. If null, will try to find a GameObject named 'Spawn Platform'.")]
    public Transform spawnPlatform;
    
    [Tooltip("The width (X) and length (Z) of the spawn area on the platform.")]
    public Vector2 spawnArea = new Vector2(5f, 5f);
    
    [Tooltip("Height above the platform to spawn the ball.")]
    public float spawnHeightOffset = 1f;

    [Header("Debug")]
    public bool drawGizmos = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-find platform if not assigned
        if (spawnPlatform == null)
        {
            GameObject platform = GameObject.Find("Spawn Platform");
            if (platform != null)
            {
                spawnPlatform = platform.transform;
            }
            else
            {
                Debug.LogWarning("[PlayerSpawnManager] 'Spawn Platform' not found in scene. Check your naming!");
            }
        }
    }

    /// <summary>
    /// Returns a randomized position centered on the Spawn Platform.
    /// Falls back to Vector3.zero if no platform is found.
    /// </summary>
    public Vector3 GetRandomSpawnPosition()
    {
        if (spawnPlatform == null)
        {
            Debug.LogError("[PlayerSpawnManager] Cannot get spawn position: No platform assigned.");
            return Vector3.zero;
        }

        float randomX = Random.Range(-spawnArea.x * 0.5f, spawnArea.x * 0.5f);
        float randomZ = Random.Range(-spawnArea.y * 0.5f, spawnArea.y * 0.5f);

        Vector3 offset = new Vector3(randomX, spawnHeightOffset, randomZ);
        
        // Transform the local offset based on the platform's rotation/scale
        return spawnPlatform.TransformPoint(new Vector3(randomX, 0, randomZ)) + (Vector3.up * spawnHeightOffset);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || spawnPlatform == null) return;

        Gizmos.color = new Color(0, 1, 1, 0.3f);
        
        // Visualize the spawn area as a box
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(spawnPlatform.position, spawnPlatform.rotation, spawnPlatform.lossyScale);
        Gizmos.matrix = rotationMatrix;
        
        Gizmos.DrawCube(Vector3.up * 0.1f, new Vector3(spawnArea.x, 0.1f, spawnArea.y));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
