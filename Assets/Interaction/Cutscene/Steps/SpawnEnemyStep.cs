using System.Collections;
using UnityEngine;

/// <summary>
/// Triggers an EnemySpawn encounter via ServerRpc (works from any client).
/// Find the spawn by GameObject name or tag.
/// </summary>
[System.Serializable]
public class SpawnEnemyStep : CutsceneStep
{
    [Tooltip("Exact name of the EnemySpawn GameObject. Checked first.")]
    public string spawnName;
    [Tooltip("Tag of the EnemySpawn GameObject. Used if spawnName is empty.")]
    public string spawnTag;

    public override IEnumerator Execute(CutsceneContext ctx)
    {
        EnemySpawn spawn = FindSpawn();
        if (spawn == null)
        {
            Debug.LogWarning($"[SpawnEnemyStep] EnemySpawn not found (name='{spawnName}', tag='{spawnTag}').");
            yield break;
        }

        spawn.StartEncounterServerRpc();
    }

    private EnemySpawn FindSpawn()
    {
        if (!string.IsNullOrEmpty(spawnName))
        {
            var go = GameObject.Find(spawnName);
            if (go != null) return go.GetComponent<EnemySpawn>();
        }
        if (!string.IsNullOrEmpty(spawnTag))
        {
            var go = GameObject.FindGameObjectWithTag(spawnTag);
            if (go != null) return go.GetComponent<EnemySpawn>();
        }
        return null;
    }
}
