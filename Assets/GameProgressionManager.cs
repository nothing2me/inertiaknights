using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

public class GameProgressionManager : NetworkBehaviour
{
    public static GameProgressionManager Instance { get; private set; }

    [Header("Progression State")]
    public NetworkVariable<int> globalObjectivesCompleted = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isFinalStateUnlocked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> gameWon = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("UI Feedback")]
    public GameObject uiContainer;
    public TextMeshProUGUI feedbackText;
    public float popupDuration = 4f;

    [Header("Settings")]
    public int totalBossesBeforeFinal = 3;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (uiContainer != null)
        {
            uiContainer.SetActive(false);
        }
    }

    /// <summary>
    /// Call this from a Boss's onDeath UnityEvent or directly
    /// </summary>
    public void CompleteGenericObjective()
    {
        if (!IsServer) return;

        globalObjectivesCompleted.Value++;
        int count = globalObjectivesCompleted.Value;
        
        Debug.Log($"[Progression] Objective completed! Total: {count}");

        if (count >= totalBossesBeforeFinal)
        {
            // All required bosses defeated before the final boss
            ShowFeedbackClientRpc($"{count}/{totalBossesBeforeFinal} Bosses Defeated...\n<color=red>THE FINAL BOSS AWAITS...</color>", popupDuration * 1.5f);
        }
        else
        {
            ShowFeedbackClientRpc($"{count}/{totalBossesBeforeFinal} Bosses Defeated", popupDuration);
        }
    }

    /// <summary>
    /// Call this specifically from the Final Boss's onDeath UnityEvent
    /// </summary>
    public void UnlockFinalState()
    {
        if (!IsServer) return;

        isFinalStateUnlocked.Value = true;
        Debug.Log("[Progression] THE FINAL BOSS HAS BEEN DEFEATED! The map gate may now trigger Game End.");
        
        ShowFeedbackClientRpc("The Final Boss is Dead.\nReturn to the bottom gate...", popupDuration * 2f);
    }

    public void TriggerGameWin()
    {
        if (!IsServer)
        {
            TriggerGameWinServerRpc();
            return;
        }
        
        TriggerGameWinProcess();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TriggerGameWinServerRpc()
    {
        TriggerGameWinProcess();
    }

    private void TriggerGameWinProcess()
    {
        if (!isFinalStateUnlocked.Value) return;

        if (gameWon.Value) return; // Prevent spamming
        gameWon.Value = true;

        Debug.Log("[Progression] VICTORY TRIGGERED!");
        ShowVictoryScreenClientRpc();
    }

    [ClientRpc]
    private void ShowFeedbackClientRpc(string message, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(FlashMessageRoutine(message, duration));
    }

    private IEnumerator FlashMessageRoutine(string message, float duration)
    {
        if (uiContainer != null && feedbackText != null)
        {
            feedbackText.text = message;
            uiContainer.SetActive(true);
            yield return new WaitForSeconds(duration);
            uiContainer.SetActive(false);
        }
    }

    [ClientRpc]
    private void ShowVictoryScreenClientRpc()
    {
        Debug.Log("🎉 YOU HAVE CONQUERED THE TOWER OF BABEL! 🎉");
        
        StopAllCoroutines();

        if (uiContainer != null)
        {
            uiContainer.SetActive(true);
            if (feedbackText != null)
            {
                feedbackText.text = "YOU HAVE CONQUERED THE TOWER!";
            }
        }
    }
}
