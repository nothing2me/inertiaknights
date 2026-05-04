using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Master registry of all CutsceneData assets.
/// IMPORTANT: The asset file must be placed at Assets/Resources/CutsceneRegistry.asset
/// so it can be loaded via Resources.Load at runtime.
/// Add every CutsceneData asset to the 'all' list here.
/// </summary>
[CreateAssetMenu(fileName = "CutsceneRegistry", menuName = "Interaction/CutsceneRegistry")]
public class CutsceneRegistry : ScriptableObject
{
    public List<CutsceneData> all = new List<CutsceneData>();

    private static CutsceneRegistry _instance;
    private Dictionary<string, CutsceneData> _cache;

    public static CutsceneRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<CutsceneRegistry>("CutsceneRegistry");
                if (_instance == null)
                    Debug.LogError("[CutsceneRegistry] Asset not found at Assets/Resources/CutsceneRegistry.asset");
            }
            return _instance;
        }
    }

    public static CutsceneData Find(string name)
    {
        var inst = Instance;
        if (inst == null) return null;

        if (inst._cache == null)
            inst.BuildCache();

        inst._cache.TryGetValue(name, out CutsceneData result);
        if (result == null)
            Debug.LogError($"[CutsceneRegistry] No cutscene named '{name}' found. Did you add it to the registry?");
        return result;
    }

    private void BuildCache()
    {
        _cache = new Dictionary<string, CutsceneData>();
        foreach (var cd in all)
        {
            if (cd == null) continue;
            if (string.IsNullOrEmpty(cd.cutsceneName))
            {
                Debug.LogWarning($"[CutsceneRegistry] A CutsceneData asset has an empty cutsceneName. Skipping.");
                continue;
            }
            if (_cache.ContainsKey(cd.cutsceneName))
                Debug.LogError($"[CutsceneRegistry] Duplicate cutsceneName '{cd.cutsceneName}'. Only the first entry will be used.");
            else
                _cache[cd.cutsceneName] = cd;
        }
    }

    // Reset cache if the asset is reloaded in the editor
    private void OnEnable() => _cache = null;
}
