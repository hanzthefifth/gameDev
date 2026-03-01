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
        [SerializeField] private float engagementRange = 12f;      // ideal combat range, set slightly lower than weapon range to account for player movement

        [SerializeField] private int sampleCount = 12;               // number of candidate spots per reposition
        [SerializeField] private float repositionSampleRadius = 6f;            // how far samples are from current position
        [SerializeField] private float minrepositionSampleRadius = 0.5f;

        [SerializeField, Range(0f, 1f)] private float predictionConfidenceThreshold = 0.7f;

        [SerializeField] private float personalSpaceRadius = 3.0f; // never pick positions closer than this to player

        [SerializeField, Tooltip("Max angle (deg) to either side of the forward-to-target direction allowed for samples. 180 = full ring.")]
        private float maxSampleSideAngle = 130f;    // restrict sampling arc so we don't pick points fully behind us

        [Header("Reposition Timing")]
        [SerializeField] private float repositionCooldown = 3f;      // ai must wait minimum time before repositioning again

        [SerializeField, Tooltip("How far from engagementRange we allow before we consider ourselves 'good enough' to stand and shoot.")]
        private float distanceTolerance = 2f;       // band around engagementRange where we hold position


        [Header("Micro-Strafe Settings")]
        [SerializeField] private bool enableStrafe = true;
        [SerializeField] private float strafeSpeed = 3.0f;
        [SerializeField] private float strafeDistance = 1.0f;       // how far per burst
        [SerializeField] private float minStrafePause = 0.5f;       // pause between bursts
        [SerializeField] private float maxStrafePause = 1.5f;
        [SerializeField] private float minStrafeDuration = 0.2f;
        [SerializeField] private float maxStrafeDuration = 0.4f;


        private float nextRepositionTime;
        [Header("Pathing")]
        [SerializeField] private float repathInterval = 0.25f; // seconds between nav path recalculations
        private float nextRepathTime = -1f;           // -1 so the very first repath fires immediately
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
                engagementRange = role.PreferredRange;
                repositionCooldown = role.RepositionFrequency;
                Debug.Log($"[TacticalMovement] {name} init: role={role}, engagementRange={engagementRange}");
            }

            // Tell the NavMeshAgent to naturally brake at engagementRange so momentum
            // doesn't carry the enemy through the player on the initial charge.
            if (agent != null)
                agent.stoppingDistance = Mathf.Max(0f, engagementRange - 0.1f);
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

            // 1) NO VISUAL: chase to last known / predicted position
            if (!threat.hasVisualContact)
            {
                TrySetDestination(GetDestinationAtPreferredRange(facingPosition));
                FaceDirection(toTarget);
                return;
            }

            // We DO have visual contact ------------------------------------------------

            // 1.5) Too close / inside personal space → back off directly.
            // Use a dedicated backoff rather than MoveDirectlyTowardPreferredRange,
            // which uses repositionSampleRadius as its step size and can overshoot badly
            // at very close range.
            if (distanceToThreat < personalSpaceRadius)
            {
                BackOffFromTarget(threat.target.position);
                FaceDirection(toTarget);
                return;
            }

            // 2) Outside tolerance band — navigate directly to engagementRange.
            // Uses GetDestinationAtPreferredRange so the agent gets one clean full
            // path rather than incremental hops. No repositionCooldown gate here.
            float distanceError = distanceToThreat - engagementRange;
            if (Mathf.Abs(distanceError) > distanceTolerance)
            {
                TrySetDestination(GetDestinationAtPreferredRange(threat.target.position));
                FaceDirection(toTarget);
                return;
            }

            // 4) Inside the tolerance band — tactical reposition or hold.
            bool cooldownReady = Time.time >= nextRepositionTime;
            if (cooldownReady)
            {
                bool moved = FindAndMoveToBestPosition(threat);
                if (!moved)
                    MoveDirectlyTowardPreferredRange(threat);
                nextRepositionTime = Time.time + repositionCooldown;
            }

            // If no tactical move was issued (or cooldown not ready), hold and strafe.
            if (agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                agent.isStopped = true;
                if (agent.hasPath)
                    agent.ResetPath();
                UpdateMicroStrafe(threat, distanceToThreat);
            }
            FaceDirection(toTarget);
        }

        // ---------------------------------------------------------------------------
        // FIX: Returns a world position that sits at engagementRange from the
        // target along the line between this enemy and the target. The NavMeshAgent
        // will stop naturally there instead of trying to reach the player's feet.
        // ---------------------------------------------------------------------------
        // Returns a point at exactly engagementRange from targetPosition,
        // on the line between this enemy and the target.
        // Works for both approach (too far) and retreat (too close).
        private Vector3 GetDestinationAtPreferredRange(Vector3 targetPosition)
        {
            Vector3 toSelf = transform.position - targetPosition;
            toSelf.y = 0f;

            if (toSelf.sqrMagnitude < 0.001f)
                toSelf = transform.forward; // fallback if exactly on top

            return targetPosition + toSelf.normalized * engagementRange;
        }

        private bool FindAndMoveToBestPosition(PerceptionSystem.ThreatInfo threat)
        {
            if (threat.target == null)
                return false;

            Vector3 targetPos = threat.target.position;
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
                float radius = Random.Range(minrepositionSampleRadius, repositionSampleRadius);

                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );

                Vector3 offsetDir = offset.normalized;
                float dot = Vector3.Dot(offsetDir, toTargetDir);
                if (dot < maxSideDot)
                    continue;

                Vector3 samplePos = transform.position + offset;

                if (NavMesh.SamplePosition(samplePos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    float distanceToTarget = Vector3.Distance(hit.position, targetPos);

                    if (distanceToTarget < personalSpaceRadius)
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

            const float minImprovement = 0.2f;
            if (foundBetter && bestScore >= currentScore + minImprovement)
            {
                TrySetDestination(bestPosition);
                return true;
            }

            return false;
        }

        // Steps away from the target every frame the player is inside personalSpaceRadius.
        // Uses agent.Move for immediate per-frame response instead of SetDestination,
        // which only fires once and stops reacting until the next cooldown.
        [Header("Personal Space Backoff")]
        // (speed exposed so you can tune how urgently the enemy retreats)
        [SerializeField] private float backoffSpeed = 3.5f;

        private void BackOffFromTarget(Vector3 targetPos)
        {
            Vector3 away = transform.position - targetPos;
            away.y = 0f;

            if (away.sqrMagnitude < 0.0001f)
                away = -transform.forward;

            away.Normalize();

            // Clear any existing path so the agent doesn't fight the manual move
            if (agent.hasPath)
                agent.ResetPath();
            agent.isStopped = false;

            // Move directly away this frame — NavMeshAgent.Move handles obstacle avoidance
            agent.Move(away * backoffSpeed * Time.deltaTime);
        }

        // Simple fixed-interval repath — what most games do.
        // Recalculates the path every repathInterval seconds regardless of
        // how much the target moved. Eliminates jitter without edge cases.
        private bool TrySetDestination(Vector3 destination)
        {
            if (Time.time < nextRepathTime)
                return false;

            agent.isStopped = false;
            agent.SetDestination(destination);
            nextRepathTime = Time.time + repathInterval;
            return true;
        }

        // Fallback used by FindAndMoveToBestPosition when no better sample is found.
        private void MoveDirectlyTowardPreferredRange(PerceptionSystem.ThreatInfo threat)
        {
            if (threat.target == null) return;
            TrySetDestination(GetDestinationAtPreferredRange(threat.target.position));
        }

        private Vector3 GetThreatFacingPosition(PerceptionSystem.ThreatInfo threat)
        {
            bool hasReliablePrediction =
                threat.ConfidenceNow >= predictionConfidenceThreshold &&
                threat.estimatedVelocity.sqrMagnitude > 0.01f;

            if (hasReliablePrediction)
                return threat.predictedPosition;

            if (threat.target != null)
                return threat.target.position;

            return threat.lastSeenPosition;
        }

        private float ScorePosition(Vector3 position, Vector3 targetPosition)
        {
            float score = 0f;

            float distance = Vector3.Distance(position, targetPosition);
            float distanceDeviation = Mathf.Abs(distance - engagementRange);
            float distanceScore = 1f - Mathf.Clamp01(distanceDeviation / 5f);
            score += distanceScore * 2f;

            int nearbyCount = Physics.OverlapSphereNonAlloc(
                position,
                3f,
                allyOverlapBuffer,
                enemyLayerMask
            );

            bool bufferSaturated = nearbyCount == allyOverlapBuffer.Length;
            float spacingScore = nearbyCount <= 1 ? 1f : 1f / nearbyCount;
            if (bufferSaturated) spacingScore *= 0.9f;
            score += spacingScore;

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

            float distOffset = Mathf.Abs(distanceToThreat - engagementRange);
            if (distOffset > distanceTolerance)
                return;

            float time = Time.time;

            if (time >= currentStrafeEndTime)
            {
                if (time < nextStrafeTime)
                    return;

                strafeDirection = (Random.value < 0.5f) ? -1 : 1;
                currentStrafeBurstDistance = Mathf.Max(0f, strafeDistance);
                currentStrafeMovedDistance = 0f;

                float minDuration = Mathf.Min(minStrafeDuration, maxStrafeDuration);
                float maxDuration = Mathf.Max(minStrafeDuration, maxStrafeDuration);
                currentStrafeEndTime = time + Random.Range(minDuration, maxDuration);

                float minPause = Mathf.Min(minStrafePause, maxStrafePause);
                float maxPause = Mathf.Max(minStrafePause, maxStrafePause);
                nextStrafeTime = currentStrafeEndTime + Random.Range(minPause, maxPause);
            }

            Vector3 toTarget = threat.target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                return;

            Vector3 forwardToTarget = toTarget.normalized;
            Vector3 right = new Vector3(forwardToTarget.z, 0, -forwardToTarget.x);

            float frameDistanceBudget = strafeSpeed * Time.deltaTime;
            float remainingBurstDistance = currentStrafeBurstDistance - currentStrafeMovedDistance;
            float moveDistance = Mathf.Min(frameDistanceBudget, Mathf.Max(0f, remainingBurstDistance));

            if (moveDistance <= 0f)
                return;

            agent.Move(right * (strafeDirection * moveDistance));
            currentStrafeMovedDistance += moveDistance;
        }
    }
}