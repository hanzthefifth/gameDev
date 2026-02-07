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
        
        [Header("State Settings")]
        [SerializeField] private float searchDuration = 4.0f;
        [SerializeField] private float investigateWaitTime = 1.5f; //wait/look around time when arriving at last seen point befor switching to search/patrol
        [SerializeField] private float combatToInvestigateDelay = 0.5f; // NEW: Hysteresis
        
        [Header("Stuck Detection")]
        [SerializeField] private float stuckSpeedThreshold = 0.05f; //im trying to move but nearly not moving, speed cutoff
        [SerializeField] private float stuckTimeToReset = 2f; //how long before path reset + safe fallback state
        
        // State-specific data
        private Transform[] patrolPoints;
        private int patrolIndex = 0;
        private Vector3 investigatePosition;
        private float stuckTimer;
        private float searchTimer;
        private float investigateTimer;
        //private float lostVisualContactTime; // NEW: Track when we lost LOS
        
        public bool IsInCombat => currentState == State.Combat;
        public State CurrentState => currentState; // For debugging
        
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
            
            // Exit current state
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
                    agent.isStopped = false; // Allow movement for repositioning (TacticalMovement will manage ResetPath when holding)
                    break;
                
                case State.Investigate:
                    investigatePosition = perception.CurrentThreat?.lastSeenPosition 
                        ?? transform.position;
                    agent.isStopped = false;
                    agent.SetDestination(investigatePosition);
                    investigateTimer = 0f;
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
                        Vector3 predictedSpot = perception.CurrentThreat.GetPredictedNavMeshPosition(2.0f, agent);
                        
                        // Validate prediction is reasonable
                        float distanceToPrediction = Vector3.Distance(transform.position, predictedSpot);
                        if (distanceToPrediction > 50f) // Sanity check
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
            // Cleanup if needed
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
            // Face movement direction while moving
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
            // When we arrive at investigation point
            if (!agent.pathPending && agent.remainingDistance < 1.0f)
            {
                investigateTimer += Time.deltaTime;
                
                // No extra rotation needed; vision cone handles scanning
                
                // After waiting, transition to search or patrol
                if (investigateTimer >= investigateWaitTime)
                {
                    if (perception.CurrentThreat != null && perception.CurrentThreat.ConfidenceNow > 0.1f)
                    {
                        ChangeState(State.Search);
                    }
                    else
                    {
                        // Lost them completely, return to patrol
                        perception.alertness = 0f;
                        ChangeState(State.Patrol);
                    }
                }
            }
        }
        
        private void ExecuteCombat()
        {
            var movement = GetComponent<TacticalMovement>();
            var weapon = GetComponent<WeaponSystem>();
            
            // Handle movement (repositioning)
            if (movement != null)
            {
                movement.UpdateCombatPosition();
            }
            
            // Handle weapon engagement
            if (weapon != null && perception.CurrentThreat != null && perception.CurrentThreat.hasVisualContact)
            {
                float distance = Vector3.Distance(transform.position, perception.CurrentThreat.target.position);
                
                // Choose melee or ranged based on distance
                if (distance <= 2.5f)
                {
                    weapon.EngageMelee(perception.CurrentThreat.target);
                }
                else
                {
                    weapon.EngageTarget(perception.CurrentThreat);
                }
            }
        }
        
        private void ExecuteSearch()
        {
            searchTimer += Time.deltaTime;
            
            // No extra look-around rotation; wide cone handles search
            
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
                        Time.deltaTime * 3f // Slower rotation for searching
                    );
                }
            }

            // If we've searched long enough, give up
            if (searchTimer > searchDuration)
            {
                Debug.Log("[StateMachine] Search timeout, returning to patrol");
                perception.alertness = 0f; // Reset alertness
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