using UnityEngine;

public class EnemySpike : MonoBehaviour
{
    public float flashDuration = 0.5f;
    private Color originalColor;
    private MeshRenderer meshRenderer;
    private bool isFlashing = false;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            originalColor = meshRenderer.material.color;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<Rigidbody>() != null)
        {
            StartFlash();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Rigidbody>() != null)
        {
            StartFlash();
        }
    }

    private void StartFlash()
    {
        if (!isFlashing && meshRenderer != null)
        {
            StartCoroutine(FlashRed());
        }
    }

    private System.Collections.IEnumerator FlashRed()
    {
        isFlashing = true;
        meshRenderer.material.color = Color.red;
        
        yield return new WaitForSeconds(flashDuration);
        
        meshRenderer.material.color = originalColor;
        isFlashing = false;
    }
}
