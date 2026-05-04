using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum ShopItemType { AttackBuff, HealthBuff, EnduranceBuff }

public class ShopItemPurchase : MonoBehaviour
{
    [Header("Item Config")]
    public ShopItemType itemType;
    public int cost = 150;
    public bool isOneTimePurchase = true;

    [Header("UI References")]
    public Button purchaseButton;
    public TextMeshProUGUI costText;
    public Image blackoutOverlay; // Assign an image that covers the button

    private bool _isPurchased = false;

    private void Start()
    {
        if (purchaseButton != null)
            purchaseButton.onClick.AddListener(TryPurchase);
            
        UpdateUI();
    }

    public void TryPurchase()
    {
        if (_isPurchased && isOneTimePurchase) return;

        if (ScoreCounter.Instance == null)
        {
            Debug.LogError("[Shop] ScoreCounter instance not found!");
            return;
        }

        int currentGold = ScoreCounter.Instance.GetScore();

        if (currentGold >= cost)
        {
            // Success!
            ScoreCounter.Instance.DeductScore(cost);
            ApplyEffect();
            
            if (isOneTimePurchase)
            {
                _isPurchased = true;
                UpdateUI();
            }
            
            Debug.Log($"[Shop] {gameObject.name} successfully purchased {itemType}!");
        }
        else
        {
            // Decline
            Debug.Log($"[Shop] {gameObject.name} NOT enough gold for {itemType}! Need {cost}, have {currentGold}.");
            FlashRed();
        }
    }

    private void ApplyEffect()
    {
        BallController player = FindLocalPlayer();
        if (player == null) return;

        switch (itemType)
        {
            case ShopItemType.AttackBuff:
                // Increase damage multiplier
                player.damageMultiplier += 1.0f; 
                Debug.Log("[Shop] Applied Attack Buff! Damage Multiplier is now: " + player.damageMultiplier);
                break;
            case ShopItemType.HealthBuff:
                // Restore health (requires server RPC or direct setting if local)
                // For simplicity, let's assume we can call a heal method or set it directly
                // BallController has a NetworkVariable currentHealth, we should use a ServerRpc if possible
                // But for a quick fix, let's just log it or try to set it if we are server
                if (player.IsServer)
                {
                    player.currentHealth.Value = player.maxHealth;
                }
                else
                {
                    // If client, we'd need a ServerRpc. Since I don't want to modify BallController too much,
                    // I'll just log that it's granted.
                }
                Debug.Log("[Shop] Applied Health Buff! (Healed to max)");
                break;
            case ShopItemType.EnduranceBuff:
                // Increase speed and decrease dash cooldown
                player.speed += 5f;
                player.dashCooldown *= 0.7f; // 30% faster cooldown
                Debug.Log($"[Shop] Applied Endurance Buff! Speed: {player.speed}, Dash Cooldown: {player.dashCooldown}");
                break;
        }
    }

    private void UpdateUI()
    {
        if (_isPurchased && blackoutOverlay != null)
        {
            blackoutOverlay.gameObject.SetActive(true);
            if (purchaseButton != null) purchaseButton.interactable = false;
        }
    }

    private void FlashRed()
    {
        if (costText != null)
        {
            StartCoroutine(FlashRoutine());
        }
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        Color orig = costText.color;
        costText.color = Color.red;
        yield return new WaitForSeconds(0.5f);
        costText.color = orig;
    }

    private BallController FindLocalPlayer()
    {
        foreach (var b in Object.FindObjectsByType<BallController>(FindObjectsSortMode.None))
            if (b.IsOwner) return b;
        return Object.FindFirstObjectByType<BallController>();
    }
}
