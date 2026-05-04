using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Simple AI that picks random destinations on the NavMesh.
/// Because our NavMesh is restricted to the painted paths, 
/// this NPC will automatically stay on the paths!
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPCPathWalker : MonoBehaviour
{
    [Header("Wander Settings")]
    public float wanderRadius = 50f;
    public float minWaitTime = 2f;
    public float maxWaitTime = 6f;
    
    private NavMeshAgent agent;
    private float timer;
    private float currentWaitTime;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        currentWaitTime = Random.Range(minWaitTime, maxWaitTime);
        timer = currentWaitTime;
    }

    void Update()
    {
        timer += Time.deltaTime;

        // --- Anti-Fall Logic ---
        // If the NPC somehow falls below the NavMesh or is disconnected, snap it back
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        // If we've waited long enough AND we've reached our previous destination
        if (timer >= currentWaitTime)
        {
            if (!agent.hasPath || agent.remainingDistance < 0.5f)
            {
                Vector3 destination = GetRandomPointOnPath();
                if (destination != Vector3.zero)
                {
                    agent.SetDestination(destination);
                }
                
                timer = 0f;
                currentWaitTime = Random.Range(minWaitTime, maxWaitTime);
            }
        }
    }

    private Vector3 GetRandomPointOnPath()
    {
        // Pick a random direction within radius
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;

        NavMeshHit hit;
        // SamplePosition finds the nearest point on the NavMesh.
        // Since the NavMesh ONLY exists on your painted paths, this is guaranteed to be on a path!
        if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return Vector3.zero;
    }
}
