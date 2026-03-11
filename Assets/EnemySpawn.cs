using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public enum SpawnMode
{
    Continuous,
    Encounter
}

[System.Serializable]
public class SpawnEntry
{
    public GameObject prefab;
    public int count = 1;
}

public class EnemySpawn : NetworkBehaviour
{
    [Header("What To Spawn")]
    [Tooltip("List of prefabs and how many of each to spawn. Works for enemies, bosses, or any mix.")]
    public SpawnEntry[] spawnEntries;

    [Header("Spawn Mode")]
    [Tooltip("Continuous: keeps respawning up to a cap (classic obelisk). Encounter: spawns everything once, opens linked gate when all dead.")]
    public SpawnMode mode = SpawnMode.Continuous;

    [Header("Continuous Mode Settings")]
    public float spawnInterval = 5f;
    public int maxEnemiesAtOnce = 3;

    [Header("Encounter Mode Settings")]
    [Tooltip("Gate that unlocks when the encounter is cleared. Leave empty for no gate.")]
    public BossGate linkedGate;
    [Tooltip("If > 0, encounter only activates when a player enters this radius. If 0, activates on game start.")]
    [Range(0f, 100f)] public float activationRadius = 0f;
    [Tooltip("Seconds between each individual spawn in the encounter wave")]
    [Range(0f, 3f)] public float encounterSpawnDelay = 0.5f;

    [Header("Spawn Area")]
    public Vector2 spawnOffset = new Vector2(2f, 2f);

    [Header("Gizmo Settings")]
    public float obeliskRadius = 1f;

    private float spawnTimer = 0f;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private bool encounterStarted = false;
    private bool encounterCleared = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (linkedGate != null)
            linkedGate.Lock();

        if (mode == SpawnMode.Encounter && activationRadius <= 0f)
            StartEncounter();
    }

    void Update()
    {
        if (!IsServer) return;
        if (spawnEntries == null || spawnEntries.Length == 0) return;

        CleanDeadEnemies();

        if (mode == SpawnMode.Continuous)
            UpdateContinuous();
        else
            UpdateEncounter();
    }

    // --- Continuous: classic respawning obelisk ---

    private void UpdateContinuous()
    {
        if (activeEnemies.Count < maxEnemiesAtOnce)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                SpawnRandomEntry();
                spawnTimer = 0f;
            }
        }
        else
        {
            spawnTimer = 0f;
        }
    }

    private void SpawnRandomEntry()
    {
        SpawnEntry entry = spawnEntries[Random.Range(0, spawnEntries.Length)];
        if (entry.prefab != null)
            SpawnOne(entry.prefab);
    }

    // --- Encounter: spawn everything, track kills, open gate ---

    private void UpdateEncounter()
    {
        if (encounterCleared) return;

        if (!encounterStarted && activationRadius > 0f)
        {
            if (AnyPlayerInRadius(activationRadius))
                StartEncounter();
            return;
        }

        if (encounterStarted && activeEnemies.Count == 0)
        {
            encounterCleared = true;
            OnEncounterCleared();
        }
    }

    private void StartEncounter()
    {
        if (encounterStarted) return;
        encounterStarted = true;
        StartCoroutine(SpawnEncounterWave());
    }

    private IEnumerator SpawnEncounterWave()
    {
        for (int i = 0; i < spawnEntries.Length; i++)
        {
            SpawnEntry entry = spawnEntries[i];
            if (entry.prefab == null) continue;

            for (int j = 0; j < entry.count; j++)
            {
                SpawnOne(entry.prefab);
                if (encounterSpawnDelay > 0f)
                    yield return new WaitForSeconds(encounterSpawnDelay);
            }
        }
    }

    private void OnEncounterCleared()
    {
        Debug.Log($"[EnemySpawn] Encounter cleared at {gameObject.name}!");
        if (linkedGate != null)
            linkedGate.Unlock();
    }

    // --- Shared spawn logic ---

    private void SpawnOne(GameObject prefab)
    {
        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnOffset.x, spawnOffset.x),
            0f,
            Random.Range(-spawnOffset.y, spawnOffset.y)
        );

        GameObject spawned = Instantiate(prefab, transform.position + randomOffset, Quaternion.identity);

        NetworkObject netObj = spawned.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();

        activeEnemies.Add(spawned);

        EnemyPlayer enemyScript = spawned.GetComponent<EnemyPlayer>();
        if (enemyScript != null)
            enemyScript.SetHomeBase(transform);
    }

    private void CleanDeadEnemies()
    {
        activeEnemies.RemoveAll(e => e == null || !e.activeInHierarchy);
    }

    private bool AnyPlayerInRadius(float radius)
    {
        float r2 = radius * radius;
        BallController[] players = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if ((p.transform.position - transform.position).sqrMagnitude <= r2)
                return true;
        }
        return false;
    }

    // --- Editor gizmos ---

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawCube(transform.position, new Vector3(spawnOffset.x * 2, 0.1f, spawnOffset.y * 2));

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, obeliskRadius);

        if (mode == SpawnMode.Encounter && activationRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);
        }

        if (linkedGate != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, linkedGate.transform.position);
        }

        if (spawnEntries != null && spawnEntries.Length > 0)
        {
            SpawnEntry first = spawnEntries[0];
            if (first.prefab != null)
            {
                EnemyPlayer enemyScript = first.prefab.GetComponent<EnemyPlayer>();
                if (enemyScript != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(transform.position, enemyScript.idleRoamRadius);
                }
            }
        }
    }
}
