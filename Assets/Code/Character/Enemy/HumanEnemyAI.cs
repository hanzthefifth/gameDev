using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class HumanoidEnemyAI : MonoBehaviour
{
    private enum EnemyState
    {
        Idle,
        Patrol,
        Investigate,
        Chase,
        Attack
    }
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator; 
    [SerializeField] private NavMeshAgent agent;

    [Header("Perception")]
    [SerializeField] private float viewDistance = 20f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private float loseSightDelay = 3f;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Combat")]
    [SerializeField] private float attackRange = 2.2f;
    [SerializeField] private float attackCooldown = 1.25f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float stoppingDistance = 1.5f;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolWaitTime = 1f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 3.5f;

    private EnemyState state = EnemyState.Idle;
    private Vector3 lastKnownPosition;
    private int patrolIndex;
    private float patrolWaitTimer;
    private float lastSeenTime;
    private float nextAttackTime;

    private void Awake()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        agent.stoppingDistance = stoppingDistance;
        lastKnownPosition = transform.position;
    }

    private void Update()
    {
        bool canSeePlayer = player != null && CanSeePlayer();

        if (canSeePlayer)
        {
            lastKnownPosition = player.position;
            lastSeenTime = Time.time;
        }

        if (canSeePlayer)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            state = distance <= attackRange ? EnemyState.Attack : EnemyState.Chase;
        }
        else if (state == EnemyState.Chase || state == EnemyState.Attack)
        {
            if (Time.time - lastSeenTime > loseSightDelay)
            {
                state = EnemyState.Investigate;
            }
        }
        else if (state == EnemyState.Idle || state == EnemyState.Patrol)
        {
            state = patrolPoints != null && patrolPoints.Length > 0 ? EnemyState.Patrol : EnemyState.Idle;
        }

        switch (state)
        {
            case EnemyState.Idle:
                HandleIdle();
                break;
            case EnemyState.Patrol:
                HandlePatrol();
                break;
            case EnemyState.Investigate:
                HandleInvestigate();
                break;
            case EnemyState.Chase:
                HandleChase();
                break;
            case EnemyState.Attack:
                HandleAttack();
                break;
        }

        UpdateAnimator();
    }


    private void HandleIdle()
    {
        agent.isStopped = true;
    }

    private void HandlePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            state = EnemyState.Idle;
            return;
        }

        agent.isStopped = false;
        agent.speed = patrolSpeed;

        if (!agent.hasPath)
        {
            agent.SetDestination(patrolPoints[patrolIndex].position);
        }

        if (agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            patrolWaitTimer += Time.deltaTime;
            if (patrolWaitTimer >= patrolWaitTime)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[patrolIndex].position);
                patrolWaitTimer = 0f;
            }
        }
    }

    private void HandleInvestigate()
    {
        agent.isStopped = false;
        agent.speed = patrolSpeed;
        agent.SetDestination(lastKnownPosition);

        if (agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            state = patrolPoints != null && patrolPoints.Length > 0 ? EnemyState.Patrol : EnemyState.Idle;
        }
    }

    private void HandleChase()
    {
        if (player == null)
        {
            state = EnemyState.Idle;
            return;
        }

        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);
    }

    private void HandleAttack()
    {
        if (player == null)
        {
            state = EnemyState.Idle;
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > attackRange * 1.1f)
        {
            state = EnemyState.Chase;
            return;
        }

        agent.isStopped = true;
        FaceTarget(player.position);

        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            player.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
        }
    }

    private bool CanSeePlayer()
    {
        Vector3 origin = transform.position + Vector3.up * 1.6f;
        Vector3 direction = (player.position - origin);
        float distance = direction.magnitude;

        if (distance > viewDistance)
        {
            return false;
        }

        float angle = Vector3.Angle(transform.forward, direction);
        if (angle > viewAngle * 0.5f)
        {
            return false;
        }

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, viewDistance, obstacleMask))
        {
            if (!hit.transform.CompareTag("Player"))
            {
                return false;
            }
        }

        if (playerMask.value != 0)
        {
            Collider[] hits = Physics.OverlapSphere(origin, viewDistance, playerMask);
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform == player)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 lookDirection = targetPosition - transform.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }
        animator.SetFloat("Speed", agent.velocity.magnitude);
        //animator.SetBool("IsAttacking", state == EnemyState.Attack); //for later
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 leftBoundary = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary * viewDistance);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
