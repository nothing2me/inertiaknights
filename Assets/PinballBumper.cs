using UnityEngine;
using System.Collections;

public class PinballBumper : MonoBehaviour
{
    public float repelForce = 20f;
    public float verticalPop = 5f;
    public float squishDuration = 0.2f;
    
    private Vector3 originalScale;
    private bool isSquishing = false;
    
    private void Start()
    {
        originalScale = transform.localScale;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody rb = collision.collider.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Calculate repel direction outward from the bumper center
            Vector3 repelDir = (collision.contacts[0].point - transform.position).normalized;
            repelDir.y = 0; // Keep the push horizontal
            repelDir.Normalize();
            
            // Add a little pop upwards as well
            Vector3 force = (repelDir * repelForce) + (Vector3.up * verticalPop);
            rb.linearVelocity = force; // Directly overriding velocity gives snappier "friendslop" feel
            
            if (!isSquishing)
            {
                StartCoroutine(SquishEffect());
            }
        }
    }
    
    private IEnumerator SquishEffect()
    {
        isSquishing = true;
        // Squish down, expand wide
        Vector3 squishedScale = new Vector3(originalScale.x * 1.3f, originalScale.y * 0.5f, originalScale.z * 1.3f);
        
        float time = 0f;
        while (time < squishDuration / 2)
        {
            transform.localScale = Vector3.Lerp(originalScale, squishedScale, time / (squishDuration / 2));
            time += Time.deltaTime;
            yield return null;
        }
        
        time = 0f;
        while (time < squishDuration / 2)
        {
            transform.localScale = Vector3.Lerp(squishedScale, originalScale, time / (squishDuration / 2));
            time += Time.deltaTime;
            yield return null;
        }
        
        transform.localScale = originalScale;
        isSquishing = false;
    }
}