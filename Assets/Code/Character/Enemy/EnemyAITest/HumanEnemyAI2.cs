using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// ============================================================================
// SCENARIO: Enemy enters combat, maintains distance, relocates tactically
// ============================================================================
/*
 * NEXT STEPS:
 * - Add door volumes and crowding prevention
 * - Implement suppression system
 * - Add search patterns
 * - Create squad coordination
 */

namespace EnemyAI.Complete
{
    public enum AlertLevel { Relaxed, Alert, Combat }
    /// Main enemy controller that coordinates all subsystems
    /// This replaces your current HumanoidEnemyAI class
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(PerceptionSystem))]
    [RequireComponent(typeof(CombatStateMachine))]
    [RequireComponent(typeof(TacticalMovement))]
    [RequireComponent(typeof(WeaponSystem))]
    [RequireComponent(typeof(RoleProfile))]

    


    public class CombatAI : MonoBehaviour
    {
        [Header("Component References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField] private PerceptionSystem perception;
        [SerializeField] private CombatStateMachine stateMachine;
        [SerializeField] private TacticalMovement movement;
        [SerializeField] private WeaponSystem weapon;
        [SerializeField] private RoleProfile role;
        
        [Header("Settings")]
        [SerializeField] private Transform[] patrolPoints;
        
        
        private void Awake()
        {
            if (!agent) agent = GetComponent<NavMeshAgent>();
            if (!perception) perception = GetComponent<PerceptionSystem>();
            if (!stateMachine) stateMachine = GetComponent<CombatStateMachine>();
            if (!movement) movement = GetComponent<TacticalMovement>();
            if (!weapon) weapon = GetComponent<WeaponSystem>();
            if (!role) role = GetComponent<RoleProfile>();
            if (!animator) animator = GetComponentInChildren<Animator>();

            // Snap to NavMesh if needed
            if (agent != null && agent.enabled && !agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
                else
                {
                    Debug.LogWarning($"[CombatAI] {name} has no NavMesh under spawn position. Disabling AI.");
                    enabled = false;
                    return;
                }
            }

            // Wire up dependencies
            stateMachine.Initialize(this, perception, agent);
            movement.Initialize(agent, perception, role);
            weapon.Initialize(transform, agent);
        }
        
        private void Start()
        {
            // Set initial state
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                stateMachine.StartPatrol(patrolPoints);
            }
        }
        
        private void Update()
        {
            if(agent == null || !agent.enabled) return;
            stateMachine.Tick(); // Let state machine handle logic
            UpdateAnimations(); // Update animations
        }
        
        private void UpdateAnimations()
        {
            if (animator == null) return;
            
            animator.SetFloat("Speed", agent.velocity.magnitude);
            animator.SetBool("InCombat", stateMachine.IsInCombat);
        }
        
        // Public API for other systems
        public void OnTakeDamage(float damage, Vector3 damageOrigin)
        {
            perception.ReportDamage(damageOrigin, damage);
        }
        
        public void OnHearSound(Vector3 soundPosition, float intensity)
        {
            perception.OnSoundHeard(soundPosition, intensity);
        }
    }

}
