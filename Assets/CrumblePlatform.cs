using UnityEngine;
using System.Collections;

public class CrumblePlatform : MonoBehaviour
{
    public float crumbleDelay = 2.0f;
    public float respawnDelay = 3.0f;
    
    private MeshRenderer meshRenderer;
    private Collider platformCollider;
    private Color originalColor;
    
    private bool isTriggered = false;

    private void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        platformCollider = GetComponent<Collider>();
        if (meshRenderer != null)
        {
            originalColor = meshRenderer.material.color;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isTriggered && collision.collider.GetComponent<Rigidbody>() != null)
        {
            StartCoroutine(CrumbleSequence());
        }
    }

    private IEnumerator CrumbleSequence()
    {
        isTriggered = true;
        
        // Tremble/Color change phase
        if (meshRenderer != null) meshRenderer.material.color = Color.yellow;
        
        float elapsed = 0;
        Vector3 originalPos = transform.position;
        
        while (elapsed < crumbleDelay)
        {
            // Shake effect
            transform.position = originalPos + (Random.insideUnitSphere * 0.1f);
            elapsed += Time.deltaTime;
            
            // Turn red right before breaking
            if (elapsed > crumbleDelay * 0.7f && meshRenderer != null)
            {
                meshRenderer.material.color = Color.red;
            }
            yield return null;
        }
        
        // Drop phase
        transform.position = originalPos; // restore pos
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (platformCollider != null) platformCollider.enabled = false;
        
        yield return new WaitForSeconds(respawnDelay);
        
        // Reset phase
        if (meshRenderer != null) 
        {
            meshRenderer.enabled = true;
            meshRenderer.material.color = originalColor;
        }
        if (platformCollider != null) platformCollider.enabled = true;
        
        isTriggered = false;
    }
}