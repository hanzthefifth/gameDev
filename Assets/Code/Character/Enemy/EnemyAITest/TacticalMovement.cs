using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace EnemyAI.Complete
{
    // ========================================================================
    // TACTICAL MOVEMENT - Handles positioning in combat
    // ========================================================================
    
    public class TacticalMovement : MonoBehaviour
    {
        private NavMeshAgent agent;
        private PerceptionSystem perception;
        private RoleProfile role;
        
        [Header("Movement Settings")]
        [SerializeField] private float preferredDistance = 12f;
        [SerializeField] private int sampleCount = 12;
        [SerializeField] private float sampleRadius = 6f;
        [SerializeField, Range(0f, 1f)] private float predictionConfidenceThreshold = 0.7f;
        
        private float nextRepositionTime;
        private float repositionInterval = 3f;
        
        public void Initialize(NavMeshAgent agent, PerceptionSystem perception, RoleProfile role)
        {
            this.agent = agent;
            this.perception = perception;
            this.role = role;
            
            if (role != null)
            {
                preferredDistance = role.PreferredRange;
                repositionInterval = role.RepositionFrequency;
            }
        }
        
        public void UpdateCombatPosition()
        {
            var threat = perception.CurrentThreat;
            
            if (threat == null || !threat.hasVisualContact)
            {
                agent.isStopped = true;
                return;
            }
            
            float distanceToThreat = Vector3.Distance(
                transform.position, 
                threat.target.position
            );

            Vector3 facingPosition = GetThreatFacingPosition(threat);
            
            // Check if we need to reposition
            bool needsReposition = 
                Time.time >= nextRepositionTime ||
                Mathf.Abs(distanceToThreat - preferredDistance) > 4f;
            
            if (needsReposition)
            {
                FindAndMoveToBestPosition(threat);
                nextRepositionTime = Time.time + repositionInterval;
            }
            else
            {
                // Hold position and face target
                agent.isStopped = true;
                
                // FIX: Calculate direction, not pass position directly
                Vector3 directionToFace = facingPosition - transform.position;
                FaceDirection(directionToFace);
            }
        }
        
        private void FindAndMoveToBestPosition(PerceptionSystem.ThreatInfo threat)
        {
            Vector3 bestPosition = transform.position;
            float bestScore = 0f;
            
            // Generate sample points in a ring
            for (int i = 0; i < sampleCount; i++)
            {
                float angle = (i / (float)sampleCount) * 360f * Mathf.Deg2Rad;
                float radius = Random.Range(3f, sampleRadius);
                
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );
                
                Vector3 samplePos = transform.position + offset;
                
                if (NavMesh.SamplePosition(samplePos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    float score = ScorePosition(hit.position, threat.target.position);
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPosition = hit.position;
                    }
                }
            }
            
            // Move to best position
            if (bestScore > 0)
            {
                agent.isStopped = false;
                agent.SetDestination(bestPosition);
            }
        }

        private Vector3 GetThreatFacingPosition(PerceptionSystem.ThreatInfo threat)
        {
            bool hasReliablePrediction =
                threat.ConfidenceNow >= predictionConfidenceThreshold &&
                threat.estimatedVelocity.sqrMagnitude > 0.01f;

            if (hasReliablePrediction)
            {
                return threat.predictedPosition;
            }

            return threat.target != null ? threat.target.position : threat.lastSeenPosition;
        }
        
        private float ScorePosition(Vector3 position, Vector3 targetPosition)
        {
            float score = 0f;
            
            // 1. Distance score - prefer positions at preferred distance
            float distance = Vector3.Distance(position, targetPosition);
            float distanceDeviation = Mathf.Abs(distance - preferredDistance);
            float distanceScore = 1f - Mathf.Clamp01(distanceDeviation / 5f);
            score += distanceScore * 2f;
            
            // 2. Spacing from allies - avoid crowding
            Collider[] nearby = Physics.OverlapSphere(position, 3f, LayerMask.GetMask("Enemy"));
            float spacingScore = nearby.Length <= 1 ? 1f : 1f / nearby.Length;
            score += spacingScore;
            
            // 3. Line of sight - prefer positions with clear shot
            Vector3 origin = position + Vector3.up * 1.5f;
            Vector3 target = targetPosition + Vector3.up * 1.0f;
            bool hasLOS = !Physics.Linecast(origin, target, LayerMask.GetMask("Default"));
            score += hasLOS ? 1f : 0f;
            
            return score;
        }
        
        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    targetRotation, 
                    Time.deltaTime * 8f
                );
            }
        }
    }
}