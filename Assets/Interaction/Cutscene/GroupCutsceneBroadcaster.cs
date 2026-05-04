using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NetworkBehaviour singleton that handles group cutscene broadcasts.
/// Place on a NetworkObject in the scene (e.g., the NetworkManager GameObject).
/// Clients call RequestGroupCutscene() → ServerRpc validates → ClientRpc fires on ALL clients.
/// Only a string (cutscene name) travels over the network.
/// </summary>
public class GroupCutsceneBroadcaster : NetworkBehaviour
{
    public static GroupCutsceneBroadcaster Instance { get; private set; }

    // Server-side oneShot tracking — plain bool dict, no NetworkVariable (no race condition)
    private readonly Dictionary<string, bool> _usedOneShotMap = new Dictionary<string, bool>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Called by InteractableController on any client to request a group cutscene.</summary>
    public void RequestGroupCutscene(string cutsceneName)
    {
        if (string.IsNullOrEmpty(cutsceneName))
        {
            Debug.LogError("[GroupCutsceneBroadcaster] cutsceneName is null or empty.");
            return;
        }
        RequestGroupCutsceneServerRpc(cutsceneName);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestGroupCutsceneServerRpc(string cutsceneName)
    {
        // OneShot check — server processes RPCs sequentially, no race condition
        if (_usedOneShotMap.TryGetValue(cutsceneName, out bool used) && used)
        {
            Debug.Log($"[GroupCutsceneBroadcaster] '{cutsceneName}' already played (oneShot). Ignoring.");
            return;
        }

        // Validate the cutscene exists in the registry
        if (CutsceneRegistry.Find(cutsceneName) == null)
        {
            Debug.LogError($"[GroupCutsceneBroadcaster] Server: '{cutsceneName}' not in CutsceneRegistry. Aborting broadcast.");
            return;
        }

        _usedOneShotMap[cutsceneName] = true;
        StartGroupCutsceneClientRpc(cutsceneName);
    }

    [ClientRpc]
    private void StartGroupCutsceneClientRpc(string cutsceneName)
    {
        CutscenePlayer.Instance?.PlayByName(cutsceneName);
    }

    /// <summary>Allow resetting a cutscene's oneShot flag from server code (e.g., for testing).</summary>
    public void ResetOneShot(string cutsceneName)
    {
        if (!IsServer) return;
        _usedOneShotMap.Remove(cutsceneName);
    }
}
