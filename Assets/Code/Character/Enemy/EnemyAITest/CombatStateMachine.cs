using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace EnemyAI.Complete
{
    // ========================================================================
    // STATE MACHINE - Coordinates behavior
    // ========================================================================
    public class CombatStateMachine : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Patrol,
            Investigate,
            Combat,
            Search
        }

        private State currentState;
        private CombatAI owner;
        private PerceptionSystem perception;
        private NavMeshAgent agent; 
        private TacticalMovement cachedTacticalMovement; // Cached components (avoid GetComponent every frame)
        private WeaponSystem cachedWeaponSystem;
        private RoleProfile cachedRoleProfile;

        [Header("State Settings")]
        [SerializeField] private float searchDuration = 4.0f;
        [SerializeField] private float minSearchTime = 1.0f;
        [SerializeField] private float investigateWaitTime = 1.5f; // Wait/look at spot
        [SerializeField] private float minInvestigateDuration = 3.0f; // Minimum total time to stay in Investigate

        [Header("Stuck Detection")]
        [SerializeField] private float stuckSpeedThreshold = 0.05f;
        [SerializeField] private float stuckTimeToReset = 2f;

        // State-specific data
        private Transform[] patrolPoints;
        private int patrolIndex = 0;
        private Vector3 investigatePosition;
        private float stuckTimer;
        private float searchTimer;
        private float investigateTimer;       // time waiting at investigate position
        private float investigateStateTime;   // total time in Investigate this entry

        public bool IsInCombat => currentState == State.Combat;
        public State CurrentState => currentState;

        private void Awake()
        {
            cachedTacticalMovement = GetComponent<TacticalMovement>();
            cachedWeaponSystem = GetComponent<WeaponSystem>();
            cachedRoleProfile = GetComponent<RoleProfile>();
        }

        public void Initialize(CombatAI owner, PerceptionSystem perception, NavMeshAgent agent)
        {
            this.owner = owner;
            this.perception = perception;
            this.agent = agent;
            currentState = State.Idle;
        }

        public void StartPatrol(Transform[] points)
        {
            patrolPoints = points;
            ChangeState(State.Patrol);
        }

        public void Tick()
        {
            State newState = DetermineState();

            if (newState != currentState)
            {
                ChangeState(newState);
            }

            ExecuteState();
            CheckIfStuck();
        }

        private State DetermineState()
        {
            // State selection logic based on perception

            // --- Commitment for Investigate ---
            // While in Investigate we generally stay there until the dedicated
            // investigate logic decides to switch us to Search/Patrol, unless
            // we regain a clear visual threat (then we go to Combat).
            if (currentState == State.Investigate)
            {
                if (perception.HasThreat && perception.CurrentThreat.hasVisualContact)
                {
                    return State.Combat;
                }

                // Let ExecuteInvestigate drive transitions out of Investigate.
                return State.Investigate;
            }

            // --- Soft lock for Search ---
            if (currentState == State.Search)
            {
                // 1) Reacquired strong LOS? Go straight back to Combat.
                if (perception.HasThreat && perception.CurrentThreat.hasVisualContact)
                {
                    return State.Combat;
                }

                // 2) Haven't searched long enough? Stay in Search no matter what.
                if (searchTimer < minSearchTime)
                {
                    return State.Search;
                }

                // 3) Still within overall searchDuration and still have some memory?
                if (searchTimer < searchDuration &&
                    perception.HasThreat &&
                    perception.CurrentThreat.ConfidenceNow > 0.05f)
                {
                    return State.Search;
                }

                // If we get here:
                // - Either searchTimer >= searchDuration, or
                // - We've totally lost threat memory
                // fall through to "normal" logic to decide next state.
            }

            // --- Threat driven logic ---
            if (perception.HasThreat)
            {
                var threat = perception.CurrentThreat;

                if (threat.hasVisualContact)
                {
                    return State.Combat;
                }
                else if (threat.ConfidenceNow > 0.3f)
                {
                    return State.Investigate;
                }
                else if (threat.ConfidenceNow > 0.1f)
                {
                    return State.Search;
                }
            }

            // No threat - check alertness
            if (perception.GetAlertLevel() == AlertLevel.Alert)
            {
                return State.Investigate;
            }

            // Default patrol/idle
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                return State.Patrol;
            }

            return State.Idle;
        }

        private void ChangeState(State newState)
        {
            if (currentState == newState)
                return;

            Debug.Log($"[StateMachine] State change: {currentState} → {newState}");

            // Exit current state (if needed)
            OnExitState(currentState);

            // Enter new state
            currentState = newState;
            OnEnterState(newState);
        }

        private void OnEnterState(State state)
        {
            switch (state)
            {
                case State.Combat:
                    agent.isStopped = false; // Allow movement for repositioning
                    perception.ClearRecentSound();
                    break;

                case State.Investigate:
                    investigatePosition = perception.CurrentThreat?.lastSeenPosition
                        ?? (perception.HasRecentSound ? perception.LastHeardSoundPosition : transform.position);

                    agent.isStopped = false;
                    agent.SetDestination(investigatePosition);

                    investigateTimer = 0f;
                    investigateStateTime = 0f;

                    Debug.Log($"[StateMachine] Investigating position: {investigatePosition}");
                    break;

                case State.Patrol:
                    agent.isStopped = false;
                    if (patrolPoints != null && patrolPoints.Length > 0)
                    {
                        agent.SetDestination(patrolPoints[patrolIndex].position);
                    }
                    break;

                case State.Search:
                    searchTimer = 0f;
                    agent.isStopped = false;

                    if (perception.CurrentThreat != null)
                    {
                        // Try prediction first
                        Vector3 predictedSpot =
                            perception.CurrentThreat.GetPredictedNavMeshPosition(2.0f, agent);

                        // Validate prediction is reasonable
                        float distanceToPrediction = Vector3.Distance(transform.position, predictedSpot);
                        if (distanceToPrediction > 50f)
                        {
                            predictedSpot = perception.CurrentThreat.lastSeenPosition;
                        }

                        Debug.Log($"[StateMachine] Searching predicted position: {predictedSpot}");
                        agent.SetDestination(predictedSpot);
                    }
                    break;
            }
        }

        private void OnExitState(State state)
        {
            // Currently unused, but kept for future cleanup hooks.
        }

        private void ExecuteState()
        {
            switch (currentState)
            {
                case State.Idle:
                    ExecuteIdle();
                    break;

                case State.Patrol:
                    ExecutePatrol();
                    break;

                case State.Investigate:
                    ExecuteInvestigate();
                    break;

                case State.Combat:
                    ExecuteCombat();
                    break;

                case State.Search:
                    ExecuteSearch();
                    break;
            }
        }

        private void ExecuteIdle()
        {
            agent.isStopped = true;
            if (agent.hasPath)
                agent.ResetPath();
        }

        private void ExecutePatrol()
        {
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                // Reached waypoint, move to next
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[patrolIndex].position);
            }
        }

        private void ExecuteInvestigate()
        {
            // Track how long we've been in Investigate this entry
            investigateStateTime += Time.deltaTime;

            // ------------------------------------------------------------------
            // 1. LOGIC PHASE: Decide Priority (Sound vs. Last Known Visual)
            // ------------------------------------------------------------------
            bool shouldFollowSound = false;

            // Check if we have a valid sound in memory
            if (perception.HasRecentSound)
            {
                if (perception.CurrentThreat == null)
                {
                    // No visual threat at all → follow sound
                    shouldFollowSound = true;
                }
                else if (perception.lastHeardSoundTime > perception.CurrentThreat.lastUpdateTime + 0.5f)
                {
                    // Sound is newer than last visual update → prefer sound
                    shouldFollowSound = true;
                }
            }

            // ------------------------------------------------------------------
            // 2. MOVEMENT PHASE: Set Destination
            // ------------------------------------------------------------------
            Vector3 targetPos = Vector3.zero;
            bool hasValidDestination = false;

            if (shouldFollowSound)
            {
                targetPos = perception.LastHeardSoundPosition;
                hasValidDestination = true;
            }
            else if (perception.CurrentThreat != null)
            {
                targetPos = perception.CurrentThreat.lastSeenPosition;
                hasValidDestination = true;
            }

            if (hasValidDestination)
            {
                float distToNewTarget = Vector3.Distance(investigatePosition, targetPos);

                // Only repath if changed significantly
                if (distToNewTarget > 1.0f)
                {
                    investigatePosition = targetPos;
                    agent.SetDestination(investigatePosition);
                    agent.isStopped = false;
                    investigateTimer = 0f; // reset "waiting at spot" timer
                }
            }

            // ------------------------------------------------------------------
            // 3. ROTATION PHASE: Face movement direction
            // ------------------------------------------------------------------
            if (agent.velocity.magnitude > 0.1f)
            {
                Vector3 moveDir = agent.velocity.normalized;
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        Time.deltaTime * 5f
                    );
                }
            }

            // ------------------------------------------------------------------
            // 4. ARRIVAL PHASE: Wait and Scan
            // ------------------------------------------------------------------
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
            {
                // Increment the timer while standing still at the point
                investigateTimer += Time.deltaTime;

                // Optional: add look-around animation/rotation here
                // transform.Rotate(0, Mathf.Sin(Time.time * 2f) * 0.3f, 0);

                // Only allow leaving Investigate if both:
                // - We've waited at the spot long enough, AND
                // - We've spent at least minInvestigateDuration in this state.
                if (investigateTimer >= investigateWaitTime &&
                    investigateStateTime >= minInvestigateDuration)
                {
                    // If we still suspect a threat (Confidence > 0.1), try to Search (predictive)
                    if (perception.CurrentThreat != null &&
                        perception.CurrentThreat.ConfidenceNow > 0.1f)
                    {
                        ChangeState(State.Search);
                    }
                    else
                    {
                        // Completely lost - give up and Patrol
                        perception.alertness = 0f;
                        ChangeState(State.Patrol);
                    }
                }
            }
        }

        private void ExecuteCombat()
        {
            // Handle movement (repositioning)
            if (cachedTacticalMovement != null)
            {
                cachedTacticalMovement.UpdateCombatPosition();
            }
            
            // Handle weapon engagement
            if (cachedWeaponSystem != null && perception.CurrentThreat != null && perception.CurrentThreat.hasVisualContact) //could use or's and return, then check the attack flags
            {
                float distance = Vector3.Distance(transform.position, perception.CurrentThreat.target.position);
                // Debug.Log($"[Combat] {name} threatDist={distance:F1}, hasVisual={perception.CurrentThreat.hasVisualContact}");
                // Debug.Log($"[{name}] CanShoot={role.CanShoot} CanMelee={role.CanMelee} dist={distance:F1}");

                //Choose melee or ranged based on distance
                if (cachedRoleProfile.CanMelee &&  distance <= cachedWeaponSystem.meleeRange)
                {
                    cachedWeaponSystem.EngageMelee(perception.CurrentThreat.target);
                }
                if(cachedRoleProfile.CanShoot)
                {
                    //Debug.Log("[Combat] Calling EngageTarget");
                    cachedWeaponSystem.EngageTarget(perception.CurrentThreat);
                }

            }
        }

        private void ExecuteSearch()
        {
            searchTimer += Time.deltaTime;

            // Face movement direction while moving (for natural look)
            if (agent.velocity.magnitude > 0.1f)
            {
                Vector3 moveDir = agent.velocity.normalized;
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        Time.deltaTime * 3f
                    );
                }
            }

            // If we've searched long enough, give up
            if (searchTimer > searchDuration)
            {
                Debug.Log("[StateMachine] Search timeout, returning to patrol");
                perception.alertness = 0f;
                ChangeState(State.Patrol);
            }
        }

        private void CheckIfStuck()
        {
            if (agent == null || !agent.enabled) return;

            bool tryingToMove = !agent.isStopped &&
                                agent.hasPath &&
                                !agent.pathPending &&
                                agent.remainingDistance > 0.5f;

            if (tryingToMove && agent.velocity.magnitude < stuckSpeedThreshold)
            {
                stuckTimer += Time.deltaTime;

                if (stuckTimer >= stuckTimeToReset)
                {
                    Debug.LogWarning($"[StateMachine] {name} is stuck, resetting");
                    agent.ResetPath();

                    // Return to safe state
                    if (patrolPoints != null && patrolPoints.Length > 0)
                    {
                        ChangeState(State.Patrol);
                    }
                    else
                    {
                        ChangeState(State.Idle);
                    }

                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
    }
}
