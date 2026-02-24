using UnityEngine;
using UnityEngine.AI;

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
        [SerializeField] private float preferredDistance = 12f;      // ideal combat radius

        [SerializeField] private int sampleCount = 12;               // number of candidate spots per reposition
        [SerializeField] private float sampleRadius = 6f;            // how far samples are from current position
        [SerializeField] private float minSampleRadius = 0.5f;

        [SerializeField, Range(0f, 1f)] private float predictionConfidenceThreshold = 0.7f;

        [SerializeField] private float minDistanceFromTarget = 3.0f; // never pick positions closer than this to player

        [SerializeField, Tooltip("Max angle (deg) to either side of the forward-to-target direction allowed for samples. 180 = full ring.")]
        private float maxSampleSideAngle = 130f;    // restrict sampling arc so we don't pick points fully behind us

        [Header("Reposition Timing")]
        [SerializeField] private float repositionInterval = 3f;      // ai must wait minimum time before repositioning again

        [SerializeField, Tooltip("How far from preferredDistance we allow before we consider ourselves 'good enough' to stand and shoot.")]
        private float distanceTolerance = 2f;       // band around preferredDistance where we hold position

        [SerializeField, Tooltip("If farther than this past preferredDistance, we just run straight toward the player instead of sampling.")]
        private float simpelChaseRange = 6f;   // how much farther than preferred before we use simple chase
        [Header("Micro-Strafe Settings")]
        [SerializeField] private bool enableStrafe = true;
        [SerializeField] private float strafeSpeed = 3.0f;
        [SerializeField] private float strafeDistance = 1.0f;       // how far per burst
        [SerializeField] private float minStrafePause = 0.5f;       // pause between bursts
        [SerializeField] private float maxStrafePause = 1.5f;
        [SerializeField] private float minStrafeDuration = 0.2f;
        [SerializeField] private float maxStrafeDuration = 0.4f;

        private float nextRepositionTime;
        private float nextStrafeTime = 0f;
        private float currentStrafeEndTime = 0f;
        private float currentStrafeBurstDistance = 0f;
        private float currentStrafeMovedDistance = 0f;
        private int strafeDirection = 0; // -1 = left, +1 = right

        [SerializeField, Min(1)] private int allyOverlapBufferSize = 16;
        private Collider[] allyOverlapBuffer;
        private int enemyLayerMask;

        private void Awake()
        {
            enemyLayerMask = LayerMask.GetMask("Enemy");
            allyOverlapBuffer = new Collider[Mathf.Max(1, allyOverlapBufferSize)];
        }
    

        public void Initialize(NavMeshAgent agent, PerceptionSystem perception, RoleProfile role)
        {
            this.agent = agent;
            this.perception = perception;
            this.role = role;

            if (role != null)
            {
                preferredDistance = role.PreferredRange;
                repositionInterval = role.RepositionFrequency;
                Debug.Log($"[TacticalMovement] {name} init: role={role}, preferredDistance={preferredDistance}");


            }
        }

        public void UpdateCombatPosition()
        {
            var threat = perception.CurrentThreat;

            if (threat == null)
            {
                agent.isStopped = true;
                if (agent.hasPath)
                    agent.ResetPath();
                return;
            }

            // Decide which position we conceptually face
            Vector3 facingPosition = GetThreatFacingPosition(threat);
            Vector3 toTarget = facingPosition - transform.position;
            toTarget.y = 0f;
            float distanceToThreat = toTarget.magnitude;

            // 1) NO VISUAL: chase to last known / predicted position, keep looking that way
            if (!threat.hasVisualContact)
            {
                agent.isStopped = false;
                agent.SetDestination(facingPosition);
                FaceDirection(toTarget);
                return;
            }

            // We DO have visual contact ---------------------------------------------------------

            // 1.5) Too close / inside "personal space" → back off directly
            if (distanceToThreat < minDistanceFromTarget)
            {
                MoveDirectlyTowardPreferredRange(threat, distanceToThreat);
                FaceDirection(toTarget);
                return;
            }

            // 2) Too far: simple chase until we get into a rough band around preferredDistance.
            if (distanceToThreat > preferredDistance + simpelChaseRange)
            {
                if (threat.target != null)
                {
                    agent.isStopped = false;
                    agent.SetDestination(threat.target.position);
                }
                FaceDirection(toTarget);
                return;
            }

            // 3) Decide whether to reposition tactically or stand and shoot.
            bool needsReposition =
                Time.time >= nextRepositionTime &&                             // time gate
                Mathf.Abs(distanceToThreat - preferredDistance) > distanceTolerance; // distance gate

            if (needsReposition)
            {
                // Try to find a better combat ring position
                bool moved = FindAndMoveToBestPosition(threat);
                if (!moved)
                {
                    // Fallback: if nothing better is found, just move a bit toward ideal distance
                    MoveDirectlyTowardPreferredRange(threat, distanceToThreat);
                }

                nextRepositionTime = Time.time + repositionInterval;

                // Always face the target while moving (strafing looks intentional)
                FaceDirection(toTarget);
            }
            else
            {
                // 4) We're in a good band around our ideal distance → stand & shoot.
                //    No continuous pathing here: we stop and clear the path so the
                //    agent is "idle" from NavMesh's perspective.
                agent.isStopped = true;
                if (agent.hasPath)
                {
                    agent.ResetPath();
                }
                // Micro strafing: local, no path changes
                UpdateMicroStrafe(threat, distanceToThreat);
                FaceDirection(toTarget);
            }
        }

        private bool FindAndMoveToBestPosition(PerceptionSystem.ThreatInfo threat)
        {
            if (threat.target == null)
                return false;

            Vector3 targetPos = threat.target.position;

            // Score current position so we only move if we find something better.
            float currentScore = ScorePosition(transform.position, targetPos);

            Vector3 toTarget = (targetPos - transform.position);
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.0001f)
                return false;

            Vector3 toTargetDir = toTarget.normalized;

            Vector3 bestPosition = transform.position;
            float bestScore = currentScore;
            bool foundBetter = false;

            float maxSideDot = Mathf.Cos(maxSampleSideAngle * Mathf.Deg2Rad);

            for (int i = 0; i < sampleCount; i++)
            {
                float angle = (i / (float)sampleCount) * 360f * Mathf.Deg2Rad;
                float radius = Random.Range(minSampleRadius, sampleRadius);

                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );

                // Normalize for direction tests
                Vector3 offsetDir = offset.normalized;

                // Reject samples that are too far behind us relative to the target
                float dot = Vector3.Dot(offsetDir, toTargetDir);
                if (dot < maxSideDot) // e.g. if maxSampleSideAngle = 130, this removes ~50° hard-back arc
                    continue;

                Vector3 samplePos = transform.position + offset;

                if (NavMesh.SamplePosition(samplePos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    float distanceToTarget = Vector3.Distance(hit.position, targetPos);

                    // Never stand on top of the player
                    if (distanceToTarget < minDistanceFromTarget)
                        continue;

                    float score = ScorePosition(hit.position, targetPos);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPosition = hit.position;
                        foundBetter = true;
                    }
                }
            }

            // Only move if this is meaningfully better than where we are now
            const float minImprovement = 0.2f;
            if (foundBetter && bestScore >= currentScore + minImprovement)
            {
                agent.isStopped = false;
                agent.SetDestination(bestPosition);
                return true;
            }

            return false;
        }

        private void MoveDirectlyTowardPreferredRange(PerceptionSystem.ThreatInfo threat, float currentDistance)
        {
            if (threat.target == null)
                return;

            Vector3 targetPos = threat.target.position;
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                return;

            Vector3 dirToTarget = toTarget.normalized;

            // // Positive delta => we are too close and need to move away.
            float delta = currentDistance - preferredDistance;
            // // How far we want to move this step
            float moveAmount = Mathf.Clamp(Mathf.Abs(delta), 0f, sampleRadius);
            

            // Decide direction:
            // - Too close => move away from target
            // - Too far  => move toward target
            Vector3 moveDir = (delta > 0f) ? dirToTarget : -dirToTarget;

            Vector3 desiredPos = transform.position + moveDir * moveAmount;

            // Ensure the new position is not inside minDistanceFromTarget
            float newDist = Vector3.Distance(desiredPos, targetPos);
            if (newDist < minDistanceFromTarget)
            {
                Vector3 away = (transform.position - targetPos);
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f)
                {
                    away.Normalize();
                    desiredPos = targetPos + away * minDistanceFromTarget;
                }
            }

            if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
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

            if (threat.target != null)
                return threat.target.position;

            return threat.lastSeenPosition;
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
            //Collider[] nearby = Physics.OverlapSphere(position, 3f, LayerMask.GetMask("Enemy"));
            //Collider[] nearby = Physics.OverlapSphereNonAlloc(position, 3f, LayerMask.GetMask("Enemy"));
            //float spacingScore = nearby.Length <= 1 ? 1f : 1f / nearby.Length;
            //score += spacingScore;

            // 2) Spacing from allies - NON-ALLOC version
            int nearbyCount = Physics.OverlapSphereNonAlloc(
                position,
                3f,
                allyOverlapBuffer,
                enemyLayerMask
            );

            // Optional: saturation handling (buffer full means we may have missed some colliders)
            bool bufferSaturated = nearbyCount == allyOverlapBuffer.Length;
            float spacingScore = nearbyCount <= 1 ? 1f : 1f / nearbyCount;

            // Small penalty if saturated (keeps scoring conservative)
            if (bufferSaturated)
                spacingScore *= 0.9f;

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

        private void UpdateMicroStrafe(PerceptionSystem.ThreatInfo threat, float distanceToThreat)
        {
            if (!enableStrafe || threat == null || threat.target == null)
                return;

            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;

            // Only strafe when we're close to our preferred combat range.
            float distOffset = Mathf.Abs(distanceToThreat - preferredDistance);
            if (distOffset > distanceTolerance)
                return;

            // Time-based burst strafing
            float time = Time.time;

            if (time >= currentStrafeEndTime)
            {
                // Not currently strafing: wait until nextStrafeTime to start a new burst
                if (time < nextStrafeTime)
                    return;

                // Start a new strafe burst
                strafeDirection = (Random.value < 0.5f) ? -1 : 1;
                currentStrafeBurstDistance = Mathf.Max(0f, strafeDistance);
                currentStrafeMovedDistance = 0f;

                float minDuration = Mathf.Min(minStrafeDuration, maxStrafeDuration);
                float maxDuration = Mathf.Max(minStrafeDuration, maxStrafeDuration);
                float duration = Random.Range(minDuration, maxDuration);
                currentStrafeEndTime = time + duration;

                float minPause = Mathf.Min(minStrafePause, maxStrafePause);
                float maxPause = Mathf.Max(minStrafePause, maxStrafePause);
                float pause = Random.Range(minPause, maxPause);
                nextStrafeTime = currentStrafeEndTime + pause;
            }

            // Actually move sideways during the active strafe window
            Vector3 toTarget = threat.target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                return;

            Vector3 forwardToTarget = toTarget.normalized;
            Vector3 right = new Vector3(forwardToTarget.z, 0, -forwardToTarget.x); // perpendicular on XZ

            // Compute a small step this frame
            float frameDistanceBudget = strafeSpeed * Time.deltaTime;
            float remainingBurstDistance = currentStrafeBurstDistance - currentStrafeMovedDistance;
            float moveDistance = Mathf.Min(frameDistanceBudget, Mathf.Max(0f, remainingBurstDistance));

            if (moveDistance <= 0f)
                return;

            Vector3 step = right * (strafeDirection * moveDistance);

            // Try moving using NavMeshAgent.Move so we don't touch SetDestination
            agent.Move(step);
            currentStrafeMovedDistance += moveDistance;
        }


    }
}
