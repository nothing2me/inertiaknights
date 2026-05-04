using UnityEngine;

public class BouncePad : MonoBehaviour
{
    public float baseBounceForce = 10f;
    public float impactMultiplier = 1.3f; // The harder they fall, the higher they bounce
    public float maxBounceForce = 50f; // Cap the bounce to prevent flying out of the map
    
    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody rb = collision.collider.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Measure how hard they hit the pad
            float incomingSpeed = collision.relativeVelocity.magnitude;
            
            // Calculate a trampoline-like bounce: base bounce + (incoming impact * multiplier)
            float finalBounceForce = baseBounceForce + (incomingSpeed * impactMultiplier);
            
            // Clamp it so players don't break the skybox
            finalBounceForce = Mathf.Min(finalBounceForce, maxBounceForce);

            Vector3 vel = rb.linearVelocity;
            vel.y = finalBounceForce;
            rb.linearVelocity = vel;
        }
    }
}