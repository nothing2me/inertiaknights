using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class EnemySpawn : NetworkBehaviour
{
    [Header("Spawning Settings")]
    public GameObject enemyPrefab;
    public float spawnInterval = 5f;
    public Vector2 spawnOffset = new Vector2(2f, 2f); // X and Z offset
    public int maxEnemiesAtOnce = 3; // Max enemies this specific obelisk can have alive at once

    [Header("Gizmo Settings")]
    public float obeliskRadius = 1f;

    private float spawnTimer = 0f;
    private List<GameObject> activeEnemies = new List<GameObject>();

    void Update()
    {
        // Only the server/host spawns enemies
        if (!IsServer) return;
        if (enemyPrefab == null) return;

        // Clean up the list to remove any dead/destroyed enemies
        activeEnemies.RemoveAll(enemy => (UnityEngine.Object)enemy == null || !enemy.activeInHierarchy);

        // Check against the local obelisk limit
        if (activeEnemies.Count < maxEnemiesAtOnce)
        {
            spawnTimer += Time.deltaTime;
            
            if (spawnTimer >= spawnInterval)
            {
                SpawnEnemy();
                spawnTimer = 0f;
            }
        }
        else
        {
            // Reset timer so it doesn't instantly spawn when an enemy dies
            spawnTimer = 0f;
        }
    }

    private void SpawnEnemy()
    {
        // Calculate spawn position using the X and Y (as Z) offset
        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnOffset.x, spawnOffset.x),
            0,
            Random.Range(-spawnOffset.y, spawnOffset.y)
        );

        Vector3 spawnPosition = transform.position + randomOffset;

        // Instantiate and spawn across the network
        GameObject newEnemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        
        NetworkObject networkObject = newEnemy.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();
        }
        
        activeEnemies.Add(newEnemy);

        // Assign this spawner as the home base for the new enemy
        EnemyPlayer playerScript = newEnemy.GetComponent<EnemyPlayer>();
        if (playerScript != null)
        {
            playerScript.SetHomeBase(transform);
        }
    }

    private void OnDrawGizmos()
    {
        // Draw the spawn area in the editor (Green Box)
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawCube(transform.position, new Vector3(spawnOffset.x * 2, 0.1f, spawnOffset.y * 2));
        
        // Draw the obelisk physical bounds
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, obeliskRadius);

        // Draw the roaming radius based on the assigned Enemy Prefab
        if (enemyPrefab != null)
        {
            EnemyPlayer enemyScript = enemyPrefab.GetComponent<EnemyPlayer>();
            if (enemyScript != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, enemyScript.idleRoamRadius);
            }
        }
    }
}
