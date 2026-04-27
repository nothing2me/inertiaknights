using UnityEngine;

/// <summary>
/// Attach this script to any object to turn it into a Hay Pile.
/// When the player lands on this object after spawning from the sky,
/// it will cushion their fall and unlock their movement.
/// </summary>
public class HayPile : MonoBehaviour
{
    [Tooltip("If true, completely zeros out the player's downward velocity on impact.")]
    public bool cushionFall = true;
}
