using UnityEngine;
using Unity.Netcode;

public class BossGate : NetworkBehaviour
{
    [Header("Gate State")]
    public NetworkVariable<bool> isLocked = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    [Header("References")]
    [Tooltip("The collider that physically blocks passage. Disabled when unlocked.")]
    public Collider blockingCollider;
    [Tooltip("Optional renderer to tint locked/unlocked. Leave empty to skip.")]
    public Renderer gateRenderer;

    [Header("Visuals")]
    public Color lockedColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    public Color unlockedColor = new Color(0.2f, 1f, 0.2f, 0.4f);

    private MaterialPropertyBlock propBlock;

    public override void OnNetworkSpawn()
    {
        propBlock = new MaterialPropertyBlock();
        isLocked.OnValueChanged += OnLockStateChanged;
        ApplyVisualState(isLocked.Value);
    }

    public override void OnNetworkDespawn()
    {
        isLocked.OnValueChanged -= OnLockStateChanged;
    }

    public void Lock()
    {
        if (!IsServer) return;
        isLocked.Value = true;
    }

    public void Unlock()
    {
        if (!IsServer) return;
        isLocked.Value = false;
        Debug.Log($"[BossGate] {gameObject.name} unlocked!");
    }

    /// <summary>Called by UnlockGateStep from any client. ServerRpc guarantees server-side execution.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void UnlockServerRpc()
    {
        Unlock();
    }

    private void OnLockStateChanged(bool previous, bool current)
    {
        ApplyVisualState(current);
    }

    private void ApplyVisualState(bool locked)
    {
        if (blockingCollider != null)
            blockingCollider.enabled = locked;

        if (gateRenderer != null)
        {
            propBlock.SetColor("_BaseColor", locked ? lockedColor : unlockedColor);
            gateRenderer.SetPropertyBlock(propBlock);
        }
    }
}
