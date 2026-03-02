using UnityEngine;

public class speedBoost : MonoBehaviour
{
    public float speedMultiplier = 5f;
    public float boostDelay = 0.1f; // Delay in seconds (e.g., 100ms)

    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody rb = collision.collider.GetComponent<Rigidbody>();
        if (rb != null)
        {
            StartCoroutine(ApplyBoost(rb));
        }
    }

    private System.Collections.IEnumerator ApplyBoost(Rigidbody rb)
    {
        yield return new WaitForSeconds(boostDelay);
        
        if (rb != null)
        {
            rb.linearVelocity *= speedMultiplier;
            Debug.Log($"Speed Boost Applied after {boostDelay}s delay!");
        }
    }
}
