using UnityEngine;

public class RaisePlat : MonoBehaviour
{
    public float raiseHeight = 3f;
    public float moveSpeed = 2f;
    public float stayDuration = 2f;
    
    private Vector3 startPos;
    private Rigidbody rb;
    private bool isMoving = false;

    void Start()
    {
        startPos = transform.position;
        rb = GetComponent<Rigidbody>();
        
        if (rb == null)
        {
            Debug.LogError("RaisePlat script requires a Rigidbody! Please add one and check 'Is Kinematic'.");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only trigger if a ball (Rigidbody) hits us and we aren't already moving
        if (!isMoving && collision.collider.GetComponent<Rigidbody>() != null)
        {
            StartCoroutine(RaiseAndLower());
        }
    }

    private System.Collections.IEnumerator RaiseAndLower()
    {
        isMoving = true;
        Vector3 targetPos = startPos + Vector3.up * raiseHeight;

        // Move Up using Rigidbody for smooth physics interaction
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            Vector3 nextPos = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            rb.MovePosition(nextPos);
            yield return null;
        }
        rb.MovePosition(targetPos);

        // Wait at top
        yield return new WaitForSeconds(stayDuration);

        // Move Down
        while (Vector3.Distance(transform.position, startPos) > 0.01f)
        {
            Vector3 nextPos = Vector3.MoveTowards(transform.position, startPos, moveSpeed * Time.deltaTime);
            rb.MovePosition(nextPos);
            yield return null;
        }
        rb.MovePosition(startPos);

        // Back to neutral, allow triggering again
        isMoving = false;
    }
}
