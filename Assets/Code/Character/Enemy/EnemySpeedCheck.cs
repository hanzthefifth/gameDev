using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator), typeof(NavMeshAgent))]
public class EnemyAnimDebug : MonoBehaviour
{
    Animator animator;
    NavMeshAgent agent;

    Vector3 lastPos;

    void Awake()
    {
        animator = GetComponent<Animator>();
        agent    = GetComponent<NavMeshAgent>();
        lastPos  = transform.position;
    }

    void Update()
    {
        // Option 1: use NavMeshAgent velocity
        Vector3 v = agent.velocity;
        v.y = 0f;
        float speed = v.magnitude;

        // If agent.velocity is unreliable, use transform delta instead:
        // Vector3 current = transform.position;
        // Vector3 delta = current - lastPos;
        // delta.y = 0f;
        // float speed = delta.magnitude / Time.deltaTime;
        // lastPos = current;

        animator.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
        Debug.Log($"[EnemyAnimDebug] speed={speed:F3}");
    }
}
