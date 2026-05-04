using UnityEngine;

public class WipeoutSweeper : MonoBehaviour
{
    public float rotationSpeed = 90f; // degrees per second
    public Vector3 rotationAxis = Vector3.up;
    public float hitForce = 25f;
    public float upwardPop = 5f;

    private void Update()
    {
        transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime, Space.Self);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody rb = collision.collider.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Get direction outward from the sweeper's center
            Vector3 pushDir = (collision.contacts[0].point - transform.position).normalized;
            pushDir.y = 0; // Keep the push horizontal
            pushDir.Normalize();

            // Depending on rotation direction, calculate the swing tangent to hit them "forward"
            Vector3 tangentDir = Vector3.Cross(rotationAxis.normalized, pushDir).normalized;
            // If negative speed, reverse the tangent direction
            if (rotationSpeed < 0) tangentDir = -tangentDir;

            // Combine outward + forward tangential force with an upward pop
            Vector3 finalVelocity = (pushDir * 0.5f + tangentDir * 0.5f).normalized * hitForce;
            finalVelocity.y = upwardPop;

            rb.linearVelocity = finalVelocity;
        }
    }
}