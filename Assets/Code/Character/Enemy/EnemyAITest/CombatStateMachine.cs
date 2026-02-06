using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace EnemyAI.Complete{
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
        [Header("Stuck Detection")]
        [SerializeField] private float stuckSpeedThreshold = 0.05f;
        [SerializeField] private float stuckTimeToReset = 1.5f;
        
        // State-specific data
        private Transform[] patrolPoints;
        private int patrolIndex = 0;
        private Vector3 investigatePosition;
        private float stuckTimer;
        
        public bool IsInCombat => currentState == State.Combat;
        
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
                else
                {
                    return State.Search;
                }
            }
            
            // No threat
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
                    agent.isStopped = true;
                    break;
                
                case State.Investigate:
                    investigatePosition = perception.CurrentThreat?.lastSeenPosition 
                        ?? transform.position;
                    agent.SetDestination(investigatePosition);
                    break;
                
                case State.Patrol:
                    if (patrolPoints != null && patrolPoints.Length > 0)
                    {
                        agent.SetDestination(patrolPoints[patrolIndex].position);
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
            if (!agent.pathPending && agent.remainingDistance < 1f)
            {
                // Reached investigation point
                // Look around, then return to patrol or search
            }
        }
        
        private void ExecuteCombat()
        {
            // This is handled by TacticalMovement and WeaponSystem
            // State machine just coordinates
            
            var movement = GetComponent<TacticalMovement>();
            var weapon = GetComponent<WeaponSystem>();
            if (weapon != null && perception.CurrentThreat?.hasVisualContact == true)
            {
                float distance = Vector3.Distance(transform.position, perception.CurrentThreat.target.position);
                
                if (distance <= 2.5f) // Melee range
                {
                    weapon.EngageMelee(perception.CurrentThreat.target);
                }
                else
                {
                    weapon.EngageTarget(perception.CurrentThreat);
                }
            }
            
            if (movement != null)
            {
                movement.UpdateCombatPosition();
            }
            
            if (weapon != null && perception.CurrentThreat?.hasVisualContact == true)
            {
                weapon.EngageTarget(perception.CurrentThreat);
            }
        }
        
        private void ExecuteSearch()
        {
            // Search pattern around last known position
            // Implementation depends on requirements
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
                    agent.ResetPath();
                    currentState = State.Idle;
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
