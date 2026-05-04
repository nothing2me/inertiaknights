using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
public class NetworkTriggerZone : NetworkBehaviour
{
    [Tooltip("Invoked when the local authority player hits the trigger.")]
    public UnityEvent onLocalPlayerEnter;

    [Tooltip("If true, requires the player to be physically inside the trigger to count.")]
    public bool requiresPlayer = true;

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (requiresPlayer)
        {
            BallController player = other.GetComponent<BallController>();
            // Only fire if the colliding ball is OWNED by the local client (prevent desyncs)
            if (player != null && player.IsOwner)
            {
                onLocalPlayerEnter?.Invoke();
            }
        }
        else if (IsServer)
        {
            // If it doesn't require a player, let the server handle generic objects entering
            onLocalPlayerEnter?.Invoke();
        }
    }
}
