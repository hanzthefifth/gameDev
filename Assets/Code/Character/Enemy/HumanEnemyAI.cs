// using UnityEngine;
// using UnityEngine.AI;

// [RequireComponent(typeof(NavMeshAgent))]
// public class HumanoidEnemyAI : MonoBehaviour
// {
//     private enum EnemyState
//     {
//         Idle,
//         Patrol,
//         Investigate,
//         Chase,
//         Attack,
//         RangedAttack,
//         Retreat
//     }

//     [Header("References")]
//     [SerializeField] private Transform player;
//     [SerializeField] private Animator animator;
//     [SerializeField] private NavMeshAgent agent;

//     [Header("Perception")]
//     [SerializeField] private float viewDistance = 20f;
//     [SerializeField] private float viewAngle = 90f;
//     [SerializeField] private float loseSightDelay = 3f;
//     [SerializeField] private LayerMask playerMask;
//     [SerializeField] private LayerMask obstacleMask;

//     [Header("Capabilities")]
//     [SerializeField] private bool hasMeleeAttack = true;
//     [SerializeField] private bool hasRangedAttack = true;
//     [SerializeField] private bool canStrafe = true;     // governs tactical repositioning
//     [SerializeField] private bool canRetreat = false;

//     [Header("Combat")]
//     [SerializeField] private float attackRange = 2.2f;
//     [SerializeField] private float attackCooldown = 1.25f;
//     [SerializeField] private float attackDamage = 10f;
//     [SerializeField] private float stoppingDistance = 1.5f;

//     [Header("Ranged Tactics")]
//     [SerializeField] private float minTimeStanding = 2f;
//     [SerializeField] private float maxTimeStanding = 4f;
//     [SerializeField] private float repositionDuration = 1.5f;      // how long we spend running to a new spot
//     [SerializeField] private float blockedCheckInterval = 0.5f;    // LOS check interval
    

//     [Header("Ranged Combat")]
//     [SerializeField] private float rangedAttackRange = 18f;
//     [SerializeField] private float rangedAttackCooldown = 0.3f;
//     [SerializeField] private float rangedAttackDamage = 8f;
//     [SerializeField] private float preferredRangedDistance = 12f;
//     [SerializeField] private float preferredDistanceTolerance = 2f;
//     [SerializeField] private float strafeStepDistance = 2.5f;      // step size for lateral move / backoff
//     [SerializeField] private float backoffDistance = 5f;      // only back off if closer than this
// [SerializeField] private float kiteSpeed = 2.0f;          // backpedal speed (slower than chaseSpeed)
//     [SerializeField] private float rangedSpreadAngle = 3f;
//     [SerializeField] private Transform rangedFirePoint;
//     [SerializeField] private LayerMask rangedHitMask = ~0;

//     [Header("NavMesh / Stuck Handling")]
//     [SerializeField] private float navMeshSampleRadius = 2f;      // how far to search for a valid point
//     [SerializeField] private float stuckSpeedThreshold = 0.05f;   // below this speed, considered "not moving"
//     [SerializeField] private float stuckTimeToReset = 1.5f;       // how long to be stuck before reset

//     [Header("Retreat")]
//     [Tooltip("Distance (in meters) at which we consider triggering a hard retreat (very close).")]
//     [SerializeField] private float retreatTriggerDistance = 4f;
//     [Tooltip("How far we try to move away in a single Retreat step.")]
//     [SerializeField] private float retreatStepDistance = 4f;

//     [Header("Patrol")]
//     [SerializeField] private Transform[] patrolPoints;
//     [SerializeField] private float patrolWaitTime = 1f;
//     [SerializeField] private float patrolSpeed = 2f;
//     [SerializeField] private float chaseSpeed = 3.5f;

//     private EnemyState state = EnemyState.Idle;
//     private Vector3 lastKnownPosition;
//     private int patrolIndex;
//     private float patrolWaitTimer;
//     private float lastSeenTime;
//     private float nextAttackTime;
//     private IDamageable playerDamageable;
//     //private EnemyHealth enemyHealth;
//     private EnemyAIDebugger debugger; // Optional debugger reference

//     // Internal ranged-combat state
//     private float combatStanceTimer;      // when to switch between stand / move
//     private bool isRepositioning;        // currently moving laterally
//     private float nextBlockedCheckTime;  // LOS check throttle

//     // Stuck detection
//     private float stuckTimer;

//     private void Awake()
//     {
//         if (agent == null)
//         {
//             agent = GetComponent<NavMeshAgent>();
//         }

//         if (animator == null)
//         {
//             animator = GetComponentInChildren<Animator>();
//         }

//         if (player == null)
//         {
//             ResolvePlayerReference();
//         }
//         else
//         {
//             CachePlayerDamageable();
//         }

//         agent.stoppingDistance = stoppingDistance;
//         lastKnownPosition = transform.position;
//         enemyHealth = GetComponent<EnemyHealth>();

//         // Optional debugger
//         debugger = GetComponent<EnemyAIDebugger>();

//         // Start with a standing window so they don't immediately sidestep on first ranged frame
//         combatStanceTimer = Time.time + Random.Range(minTimeStanding, maxTimeStanding);
//     }

//     private void Update()
//     {
//         if (enemyHealth != null && enemyHealth.IsDead)
//         {
//             return;
//         }

//         if (player == null)
//         {
//             ResolvePlayerReference();
//         }

//         bool canSeePlayer = player != null && CanSeePlayer();

//         if (canSeePlayer)
//         {
//             lastKnownPosition = player.position;
//             lastSeenTime = Time.time;
//         }

//         if (canSeePlayer)
//         {
//             float distance = Vector3.Distance(transform.position, player.position);
//             if (hasMeleeAttack && distance <= attackRange)
//             {
//                 state = EnemyState.Attack;
//             }
//             else if (hasRangedAttack && distance <= rangedAttackRange)
//             {
//                 state = EnemyState.RangedAttack;
//             }
//             else
//             {
//                 state = EnemyState.Chase;
//             }
//         }
//         else if (state == EnemyState.Chase || state == EnemyState.Attack ||
//                  state == EnemyState.RangedAttack || state == EnemyState.Retreat)
//         {
//             if (Time.time - lastSeenTime > loseSightDelay)
//             {
//                 state = EnemyState.Investigate;
//                 debugger?.OnPlayerLost();
//             }
//         }
//         else if (state == EnemyState.Idle || state == EnemyState.Patrol)
//         {
//             state = patrolPoints != null && patrolPoints.Length > 0 ? EnemyState.Patrol : EnemyState.Idle;
//         }

//         switch (state)
//         {
//             case EnemyState.Idle:
//                 HandleIdle();
//                 break;
//             case EnemyState.Patrol:
//                 HandlePatrol();
//                 break;
//             case EnemyState.Investigate:
//                 HandleInvestigate();
//                 break;
//             case EnemyState.Chase:
//                 HandleChase();
//                 break;
//             case EnemyState.Attack:
//                 HandleAttack();
//                 break;
//             case EnemyState.RangedAttack:
//                 HandleRangedAttack();
//                 break;
//             case EnemyState.Retreat:
//                 HandleRetreat();
//                 break;
//         }

//         HandleStuckFailSafe();
//         UpdateAnimator();
//     }

//     private void HandleStuckFailSafe()
//     {
//         if (agent == null || !agent.enabled)
//             return;

//         bool tryingToMove =
//             !agent.isStopped &&
//             agent.hasPath &&
//             !agent.pathPending &&
//             agent.remainingDistance > agent.stoppingDistance + 0.2f;

//         float speed = agent.velocity.magnitude;

//         if (tryingToMove && speed < stuckSpeedThreshold)
//         {
//             stuckTimer += Time.deltaTime;

//             if (stuckTimer >= stuckTimeToReset)
//             {
//                 agent.ResetPath();

//                 if (player != null)
//                 {
//                     state = EnemyState.Chase;
//                     agent.SetDestination(player.position);
//                 }
//                 else
//                 {
//                     state = EnemyState.Idle;
//                 }

//                 stuckTimer = 0f;
//             }
//         }
//         else
//         {
//             stuckTimer = 0f;
//         }
//     }

//     private void HandleIdle()
//     {
//         agent.isStopped = true;
//     }

//     private void HandlePatrol()
//     {
//         if (patrolPoints == null || patrolPoints.Length == 0)
//         {
//             state = EnemyState.Idle;
//             return;
//         }

//         agent.isStopped = false;
//         agent.speed = patrolSpeed;

//         if (!agent.hasPath)
//         {
//             agent.SetDestination(patrolPoints[patrolIndex].position);
//         }

//         if (agent.remainingDistance <= agent.stoppingDistance + 0.1f)
//         {
//             patrolWaitTimer += Time.deltaTime;
//             if (patrolWaitTimer >= patrolWaitTime)
//             {
//                 patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
//                 agent.SetDestination(patrolPoints[patrolIndex].position);
//                 patrolWaitTimer = 0f;
//             }
//         }
//     }

//     private void HandleInvestigate()
//     {
//         agent.isStopped = false;
//         agent.speed = patrolSpeed;
//         agent.SetDestination(lastKnownPosition);

//         if (agent.remainingDistance <= agent.stoppingDistance + 0.2f)
//         {
//             state = patrolPoints != null && patrolPoints.Length > 0 ? EnemyState.Patrol : EnemyState.Idle;
//         }
//     }

//     private void HandleChase()
//     {
//         if (player == null)
//         {
//             state = EnemyState.Idle;
//             return;
//         }

//         agent.isStopped = false;
//         agent.speed = chaseSpeed;
//         agent.SetDestination(player.position);
//     }

//     private void HandleAttack()
//     {
//         if (player == null)
//         {
//             state = EnemyState.Idle;
//             return;
//         }

//         float distance = Vector3.Distance(transform.position, player.position);
//         if (distance > attackRange * 1.1f)
//         {
//             state = EnemyState.Chase;
//             return;
//         }

//         agent.isStopped = true;
//         FaceTarget(player.position);

//         if (Time.time >= nextAttackTime)
//         {
//             nextAttackTime = Time.time + attackCooldown;
//             TryApplyDamage();
//             debugger?.OnMeleeAttack(attackDamage);
//         }
//     }

//     private void HandleRangedAttack()
//     {
//         if (player == null)
//         {
//             state = EnemyState.Idle;
//             return;
//         }

//         float distance = Vector3.Distance(transform.position, player.position);

//         // --- HARD CHECKS ---
//         if (!hasRangedAttack)
//         {
//             if (hasMeleeAttack && distance <= attackRange)
//             {
//                 state = EnemyState.Attack;
//             }
//             else
//             {
//                 state = EnemyState.Chase;
//             }
//             return;
//         }

//         if (hasMeleeAttack && distance <= attackRange)
//         {
//             state = EnemyState.Attack;
//             return;
//         }

//         if (distance > rangedAttackRange * 1.1f)
//         {
//             state = EnemyState.Chase;
//             return;
//         }

//         if (canRetreat && distance <= retreatTriggerDistance)
//         {
//             state = EnemyState.Retreat;
//             return;
//         }

//         // --- RANGED MOVEMENT ---
//         agent.isStopped = false;

//         float lowerBound = preferredRangedDistance - preferredDistanceTolerance;
//         float upperBound = preferredRangedDistance + preferredDistanceTolerance;

//         // A) REALLY too close → back off (using backoffDistance, not lowerBound)
//         if (distance < backoffDistance)
//         {
//             isRepositioning = false;
//             agent.speed = kiteSpeed;

//             Vector3 dirAway = (transform.position - player.position).normalized;
//             Vector3 kitingDest = transform.position + dirAway * strafeStepDistance;

//             if (NavMesh.SamplePosition(kitingDest, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
//             {
//                 agent.SetDestination(hit.position);
//             }
//             else
//             {
//                 agent.isStopped = true; // can't find a good backoff spot
//             }
//         }
//         // B) Too far from preferred → push in
//         else if (distance > upperBound)
//         {
//             isRepositioning = false;
//             agent.speed = chaseSpeed;
//             agent.SetDestination(player.position);
//         }
//         // C) In the “sweet spot” → stand & reposition tactically
//         else
//         {
//             ManageTacticalPositioning(distance);
//         }

//         // --- ACTIONS ---
//         FaceTarget(player.position);

//         if (Time.time >= nextAttackTime)
//         {
//             nextAttackTime = Time.time + rangedAttackCooldown;
//             FireRangedShot();
//         }
//     }



//     private void HandleRetreat()
//     {
//         if (player == null)
//         {
//             state = EnemyState.Idle;
//             return;
//         }

//         float distance = Vector3.Distance(transform.position, player.position);
//         if (distance >= preferredRangedDistance)
//         {
//             state = EnemyState.RangedAttack;
//             return;
//         }

//         agent.isStopped = false;
//         agent.speed = chaseSpeed;

//         Vector3 toPlayer = (player.position - transform.position).normalized;
//         Vector3 retreatDir = -toPlayer;
//         Vector3 targetPos = transform.position + retreatDir * retreatStepDistance;
//         agent.SetDestination(targetPos);

//         FaceTarget(player.position);
//     }

//     private void ManageTacticalPositioning(float distanceToPlayer)
//     {
//         // Periodic LOS check from weapon/eye point
//         if (Time.time > nextBlockedCheckTime)
//         {
//             nextBlockedCheckTime = Time.time + blockedCheckInterval;

//             if (!CheckAttackLineOfSight())
//             {
//                 if (!isRepositioning)
//                 {
//                     TriggerRepositioning(true); // urgent: blocked LOS
//                 }
//             }
//         }

//         if (isRepositioning)
//         {
//             agent.speed = patrolSpeed;

//             if (agent.remainingDistance <= agent.stoppingDistance || Time.time >= combatStanceTimer)
//             {
//                 isRepositioning = false;
//                 combatStanceTimer = Time.time + Random.Range(minTimeStanding, maxTimeStanding);
//             }
//         }
//         else
//         {
//             agent.isStopped = true;

//             if (canStrafe && Time.time >= combatStanceTimer)
//             {
//                 TriggerRepositioning(false); // routine move
//             }
//         }
//     }

//     private void TriggerRepositioning(bool urgent)
//     {
//         isRepositioning = true;
//         combatStanceTimer = Time.time + repositionDuration;

//         if (player == null)
//         {
//             isRepositioning = false;
//             return;
//         }

//         Vector3 toPlayer = (player.position - transform.position).normalized;
//         Vector3 right = Vector3.Cross(Vector3.up, toPlayer).normalized;

//         float directionSign = Random.value > 0.5f ? 1f : -1f;
//         Vector3 moveDir = right * directionSign;

//         Vector3 dest = transform.position + moveDir * strafeStepDistance;

//         if (NavMesh.SamplePosition(dest, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
//         {
//             agent.isStopped = false;
//             agent.SetDestination(hit.position);
//         }
//         else
//         {
//             // Try opposite side if first side invalid
//             dest = transform.position - moveDir * strafeStepDistance;
//             if (NavMesh.SamplePosition(dest, out NavMeshHit hit2, navMeshSampleRadius, NavMesh.AllAreas))
//             {
//                 agent.isStopped = false;
//                 agent.SetDestination(hit2.position);
//             }
//             else
//             {
//                 // Nothing valid: cancel reposition
//                 isRepositioning = false;
//                 combatStanceTimer = Time.time + Random.Range(minTimeStanding, maxTimeStanding);
//             }
//         }
//     }

//     private bool CheckAttackLineOfSight()
//     {
//         if (player == null) return false;

//         Vector3 origin = rangedFirePoint != null
//             ? rangedFirePoint.position
//             : transform.position + Vector3.up * 1.5f;

//         Vector3 target = player.position + Vector3.up * 1.0f;
//         Vector3 dir = (target - origin).normalized;

//         if (Physics.Raycast(origin, dir, out RaycastHit hit, rangedAttackRange, rangedHitMask))
//         {
//             return hit.collider.transform.root == player;
//         }

//         return false;
//     }

//     private void FireRangedShot()
//     {
//         if (player == null)
//         {
//             return;
//         }

//         Vector3 origin = rangedFirePoint != null
//             ? rangedFirePoint.position
//             : transform.position + Vector3.up * 1.6f;

//         Vector3 target = player.position + Vector3.up * 1.2f;
//         Vector3 direction = (target - origin).normalized;
//         direction = ApplySpread(direction);

//         debugger?.OnRangedShotFired(origin, direction, rangedAttackRange);

//         if (Physics.Raycast(origin, direction, out RaycastHit hit, rangedAttackRange, rangedHitMask))
//         {
//             IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
//             debugger?.OnRaycastHit(hit, damageable != null, rangedAttackDamage);

//             if (damageable != null)
//             {
//                 damageable.TakeDamage(rangedAttackDamage);
//             }
//         }
//         else
//         {
//             debugger?.OnRaycastMiss(origin, direction, rangedAttackRange);
//         }
//     }

//     private Vector3 ApplySpread(Vector3 direction)
//     {
//         if (rangedSpreadAngle <= 0f)
//         {
//             return direction;
//         }

//         float yaw = Random.Range(-rangedSpreadAngle, rangedSpreadAngle);
//         float pitch = Random.Range(-rangedSpreadAngle, rangedSpreadAngle);
//         Quaternion spreadRotation = Quaternion.Euler(pitch, yaw, 0f);
//         return spreadRotation * direction;
//     }

//     private void ResolvePlayerReference()
//     {
//         GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
//         if (playerObject == null)
//         {
//             return;
//         }

//         player = playerObject.transform;
//         CachePlayerDamageable();
//     }

//     private void CachePlayerDamageable()
//     {
//         if (player == null)
//         {
//             return;
//         }

//         playerDamageable = player.GetComponentInParent<IDamageable>();
//     }

//     private void TryApplyDamage()
//     {
//         if (player == null)
//         {
//             return;
//         }

//         if (playerDamageable == null)
//         {
//             CachePlayerDamageable();
//         }

//         if (playerDamageable != null)
//         {
//             playerDamageable.TakeDamage(attackDamage);
//             return;
//         }

//         player.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
//     }

//     private bool CanSeePlayer()
//     {
//         if (player == null) return false;

//         Vector3 origin = transform.position + Vector3.up * 1.6f;
//         Vector3 direction = player.position - origin;
//         float distance = direction.magnitude;

//         if (distance > viewDistance)
//         {
//             return false;
//         }

//         float angle = Vector3.Angle(transform.forward, direction);
//         if (angle > viewAngle * 0.5f)
//         {
//             return false;
//         }

//         if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, viewDistance, obstacleMask))
//         {
//             if (!hit.transform.CompareTag("Player"))
//             {
//                 return false;
//             }
//         }

//         if (playerMask.value != 0)
//         {
//             Collider[] hits = Physics.OverlapSphere(origin, viewDistance, playerMask);
//             bool found = false;
//             for (int i = 0; i < hits.Length; i++)
//             {
//                 if (hits[i].transform == player)
//                 {
//                     found = true;
//                     break;
//                 }
//             }

//             if (!found)
//             {
//                 return false;
//             }
//         }

//         debugger?.OnPlayerDetected(player, distance);
//         return true;
//     }

//     private void FaceTarget(Vector3 targetPosition)
//     {
//         Vector3 lookDirection = targetPosition - transform.position;
//         lookDirection.y = 0f;

//         if (lookDirection.sqrMagnitude < 0.01f)
//         {
//             return;
//         }

//         Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
//         transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
//     }

//     private void UpdateAnimator()
//     {
//         if (animator == null)
//         {
//             return;
//         }

//         animator.SetFloat("Speed", agent.velocity.magnitude);
//         animator.SetBool("IsAttacking", state == EnemyState.Attack || state == EnemyState.RangedAttack);
//     }

//     private void OnDrawGizmosSelected()
//     {
//         Gizmos.color = Color.yellow;
//         Gizmos.DrawWireSphere(transform.position, viewDistance);

//         Vector3 leftBoundary = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
//         Vector3 rightBoundary = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
//         Gizmos.DrawLine(transform.position, transform.position + leftBoundary * viewDistance);
//         Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewDistance);

//         Gizmos.color = Color.red;
//         Gizmos.DrawWireSphere(transform.position, attackRange);
//     }
// }
