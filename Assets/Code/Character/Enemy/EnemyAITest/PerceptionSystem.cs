// using UnityEngine;
// using UnityEngine.AI;
// using System.Collections.Generic;

// namespace EnemyAI.Complete{
// // ========================================================================
//     // PERCEPTION SYSTEM - Detects and remembers threats
//     // ========================================================================
    
//     public class PerceptionSystem : MonoBehaviour
//     {
//         [System.Serializable]
//         public class ThreatInfo
//         {
//             [System.Serializable]
//             public struct PositionSample
//             {
//                 public Vector3 position;
//                 public float timestamp;

//                 public PositionSample(Vector3 position, float timestamp)
//                 {
//                     this.position = position;
//                     this.timestamp = timestamp;
//                 }
//             }
            
//             public Vector3 GetPredictedNavMeshPosition(float timeAhead, NavMeshAgent agentReference)
//             {
//                 // 1. Linear prediction
//                 Vector3 rawPrediction = lastSeenPosition + (estimatedVelocity * timeAhead);
                
//                 // 2. Sample NavMesh to find nearest valid floor point
//                 NavMeshHit hit;
//                 if (NavMesh.SamplePosition(rawPrediction, out hit, 4.0f, NavMesh.AllAreas))
//                 {
//                     return hit.position;
//                 }
                
//                 // 3. If prediction is off-mesh (e.g. over a ledge), trace path to edge
//                 NavMeshPath path = new NavMeshPath();
//                 if (agentReference.CalculatePath(rawPrediction, path) && path.status == NavMeshPathStatus.PathPartial)
//                 {
//                     // Return the last reachable point (the edge they jumped off)
//                     return path.corners[path.corners.Length - 1];
//                 }

//                 return lastSeenPosition;
//             }


//             public Transform target;
//             public Vector3 lastSeenPosition;
//             public float confidence;        // 0-1, decays over time
//             public float lastUpdateTime;
//             public bool hasVisualContact;
//             public Vector3 estimatedVelocity;
//             public bool seenThisFrame;

//             private readonly List<PositionSample> positionSamples = new List<PositionSample>();
//             private int maxSamples = 6;
//             private float predictionLookAheadTime = 0.35f;
            
//             public float ConfidenceNow => 
//                 Mathf.Max(0, confidence - (Time.time - lastUpdateTime) * 0.15f);

//             public Vector3 predictedPosition =>
//                 lastSeenPosition + estimatedVelocity * predictionLookAheadTime;

//             public void ConfigurePrediction(float lookAheadTime, int maxSampleCount)
//             {
//                 predictionLookAheadTime = Mathf.Max(0f, lookAheadTime);
//                 maxSamples = Mathf.Max(2, maxSampleCount);

//                 while (positionSamples.Count > maxSamples)
//                 {
//                     positionSamples.RemoveAt(0);
//                 }
//             }

//             public void AddPositionSample(Vector3 samplePosition, float sampleTime)
//             {
//                 positionSamples.Add(new PositionSample(samplePosition, sampleTime));

//                 while (positionSamples.Count > maxSamples)
//                 {
//                     positionSamples.RemoveAt(0);
//                 }

//                 estimatedVelocity = ComputeSmoothedVelocity();
//             }

//             private Vector3 ComputeSmoothedVelocity()
//             {
//                 if (positionSamples.Count < 2)
//                 {
//                     return Vector3.zero;
//                 }

//                 Vector3 velocitySum = Vector3.zero;
//                 float totalWeight = 0f;

//                 for (int i = 1; i < positionSamples.Count; i++)
//                 {
//                     PositionSample previous = positionSamples[i - 1];
//                     PositionSample current = positionSamples[i];
//                     float dt = current.timestamp - previous.timestamp;

//                     if (dt <= Mathf.Epsilon)
//                     {
//                         continue;
//                     }

//                     Vector3 segmentVelocity = (current.position - previous.position) / dt;
//                     float weight = i; // Heavier weighting on recent movement
//                     velocitySum += segmentVelocity * weight;
//                     totalWeight += weight;
//                 }

//                 if (totalWeight <= Mathf.Epsilon)
//                 {
//                     return Vector3.zero;
//                 }
//                 return velocitySum / totalWeight;
//             }


            
//         }// end of ThreadInfo class
        
//         [Header("Detection Settings")]
//         [SerializeField] private float visionRange = 25f;
//         [SerializeField] private float visionAngle = 120f;
//         [SerializeField] private float hearingRange = 15f;
//         [SerializeField] private LayerMask targetLayer = 1 << 6; // what can be detected
//         [SerializeField] private LayerMask obstacleLayer = 1 << 0; // what can block los
//         [SerializeField, Min(2)] private int positionSampleBufferSize = 6; //number of movement samples used. High = smooth but laggyr prediction; lower = twitchier but responsive
//         [SerializeField, Min(0f)] private float threatPredictionLookAheadTime = 0.35f; //look ahdead horizon, high = more lead, but overshoot risk
        
//         [Header("Dynamic Vision Cone")]
//         [SerializeField] private float relaxedVisionAngle = 90f;    // Normal patrol vision
//         [SerializeField] private float alertVisionAngle = 160f;     // Investigating vision
//         [SerializeField] private float combatVisionAngle = 120f;    // Combat vision
//         [SerializeField] private float searchVisionAngle = 200f;    // Searching vision (almost 360°)
//         [SerializeField] private float visionAngleTransitionSpeed = 2f; // How fast cone widens/narrows
        
//         [Header("Alertness")]
//         public float alertness = 0f;  // 0 = calm, 1 = full combat
//         private const float ALERT_THRESHOLD = 0.4f;
//         private const float COMBAT_THRESHOLD = 0.7f;

//         // Sound investigation memory (used when no direct threat is known)
//         private Vector3 lastHeardSoundPosition;
//         public float lastHeardSoundTime = -999f;
//         [SerializeField] private float soundMemoryDuration = 4f;
        
//         private Dictionary<Transform, ThreatInfo> threats = new Dictionary<Transform, ThreatInfo>();
//         private ThreatInfo primaryThreat;
        
//         // Dynamic vision cone tracking
//         private float currentVisionAngle;
//         private float targetVisionAngle;
        
//         // Public accessors
//         public ThreatInfo CurrentThreat => primaryThreat;
//         public bool HasThreat => primaryThreat != null && primaryThreat.ConfidenceNow > 0.2f;
//         public float CurrentVisionAngle => currentVisionAngle; // For debug visualization
//         public bool HasRecentSound => Time.time - lastHeardSoundTime <= soundMemoryDuration;
//         public Vector3 LastHeardSoundPosition => lastHeardSoundPosition;
        
//         public AlertLevel GetAlertLevel()
//         {
//             if (alertness < ALERT_THRESHOLD) return AlertLevel.Relaxed;
//             if (alertness < COMBAT_THRESHOLD) return AlertLevel.Alert;
//             return AlertLevel.Combat;
//         }
        
//         private void Awake()
//         {
//             currentVisionAngle = relaxedVisionAngle;
//             targetVisionAngle = relaxedVisionAngle;
            
//             // Register with sound manager
//             if (SoundManager.Instance != null)
//             {
//                 SoundManager.Instance.RegisterListener(this);
//             }
//         }
        
//         private void OnDestroy()
//         {
//             // Unregister when destroyed
//             if (SoundManager.Instance != null)
//             {
//                 SoundManager.Instance.UnregisterListener(this);
//             }
//         }
        
//         private void Update()
//         {
//             UpdateDynamicVisionCone();
//             ScanForTargets();
//             UpdateThreats();
//             DecayAlertness();
//         }
        
//         /// Dynamically adjusts vision cone based on alertness and whether we're searching
//         private void UpdateDynamicVisionCone()
//         {
//             // Determine target angle based on state
//             if (!HasThreat)
//             {
//                 // No threat - use relaxed angle
//                 targetVisionAngle = relaxedVisionAngle;
//             }
//             else
//             {
//                 var threat = CurrentThreat;
                
//                 if (threat.hasVisualContact)
//                 {
//                     // In combat - use combat angle (medium)
//                     targetVisionAngle = combatVisionAngle;
//                 }
//                 else if (threat.ConfidenceNow > 0.3f)
//                 {
//                     // Investigating - widen to alert angle
//                     targetVisionAngle = alertVisionAngle;
//                 }
//                 else if (threat.ConfidenceNow > 0.1f)
//                 {
//                     // Searching - very wide angle (almost 360°)
//                     targetVisionAngle = searchVisionAngle;
//                 }
//                 else
//                 {
//                     // Lost them - return to relaxed
//                     targetVisionAngle = relaxedVisionAngle;
//                 }
//             }
            
//             // Smoothly transition to target angle
//             currentVisionAngle = Mathf.Lerp(
//                 currentVisionAngle,
//                 targetVisionAngle,
//                 Time.deltaTime * visionAngleTransitionSpeed
//             );
//         }
        
//         private void ScanForTargets()
//         {
//             // Reset per-frame flags
//             foreach (var kvp in threats)
//             {
//                 kvp.Value.seenThisFrame = false;
//             }

//             Collider[] potentials = Physics.OverlapSphere( // Find potential targets in range
//                 transform.position, 
//                 visionRange, 
//                 targetLayer
//             );
            
//             foreach (Collider col in potentials)
//             {
//                 Transform target = col.transform;
                
//                 // Check if in DYNAMIC view cone (uses currentVisionAngle instead of fixed visionAngle)
//                 Vector3 toTarget = target.position - transform.position;
//                 float angle = Vector3.Angle(transform.forward, toTarget);
                
//                 if (angle > currentVisionAngle * 0.5f)
//                     continue;
                
//                 // Check line of sight
//                 Vector3 origin = transform.position + Vector3.up * 1.6f;
//                 Vector3 targetPoint = target.position + Vector3.up * 1.0f;
//                 Vector3 direction = (targetPoint - origin).normalized;
//                 float distance = Vector3.Distance(origin, targetPoint);
                
//                 bool hasLOS = !Physics.Raycast(
//                     origin, 
//                     direction, 
//                     distance, 
//                     obstacleLayer
//                 );
                
//                 if (hasLOS)
//                 {
//                     RegisterThreat(target, target.position, 1.0f, true);
//                     BoostAlertness(0.5f);
//                 }
//             }
        
//             foreach (var kvp in threats) // After processing potentials, anything not seen this frame loses visual
//             {
//                 ThreatInfo info = kvp.Value;
//                 if (!info.seenThisFrame)
//                 {
//                     info.hasVisualContact = false;
//                 }
//             }
//         }
        
//         private void RegisterThreat(Transform target, Vector3 position, 
//             float confidence, bool visual)
//         {
//             if (!threats.ContainsKey(target))
//             {
//                 threats[target] = new ThreatInfo
//                 {
//                     target = target,
//                     lastSeenPosition = position,
//                     confidence = confidence,
//                     lastUpdateTime = Time.time,
//                     hasVisualContact = visual,
//                     estimatedVelocity = Vector3.zero,
//                     //adding
//                     seenThisFrame = true
//                 };

//                 threats[target].ConfigurePrediction(threatPredictionLookAheadTime, positionSampleBufferSize);
//                 threats[target].AddPositionSample(position, Time.time);
//             }
//             else
//             {
//                 ThreatInfo info = threats[target];
//                 info.lastSeenPosition = position;
//                 info.confidence = Mathf.Min(1f, info.confidence + confidence);
//                 info.lastUpdateTime = Time.time;
//                 info.hasVisualContact = visual;
//                 info.seenThisFrame = true;
//                 info.ConfigurePrediction(threatPredictionLookAheadTime, positionSampleBufferSize);
//                 info.AddPositionSample(position, Time.time);
//             }
//         }
        
//         private void UpdateThreats()
//         {
//             // Find highest priority threat
//             ThreatInfo best = null;
//             float bestScore = 0f;
            
//             List<Transform> toRemove = new List<Transform>();
            
//             foreach (var kvp in threats)
//             {
//                 ThreatInfo info = kvp.Value;
//                 float conf = info.ConfidenceNow;
                
//                 if (conf < 0.05f)
//                 {
//                     toRemove.Add(kvp.Key);
//                     continue;
//                 }
                
//                 // Score based on confidence, proximity, visual contact
//                 float dist = Vector3.Distance(transform.position, info.lastSeenPosition);
//                 float proximity = 1f - Mathf.Clamp01(dist / visionRange);
//                 float visualBonus = info.hasVisualContact ? 1.0f : 0.3f;
                
//                 float score = conf * proximity * visualBonus;
                
//                 if (score > bestScore)
//                 {
//                     bestScore = score;
//                     best = info;
//                 }
//             }
            
//             // Cleanup
//             foreach (Transform t in toRemove)
//             {
//                 threats.Remove(t);
//             }
            
//             primaryThreat = best;
//         }
        
//         /// Called when this AI takes damage - attempts to locate attacker
//         /// <param name="damageOrigin">Position where damage came from</param>
//         /// <param name="damage">Amount of damage taken</param>
//         /// <param name="attacker">Optional: direct reference to attacker if available</param>
//         public void ReportDamage(Vector3 damageOrigin, float damage, Transform attacker = null)
//         {
//             // Instantly boost alertness
//             float alertBoost = Mathf.Clamp01(damage / 50f) * 0.8f; // Scale with damage
//             BoostAlertness(alertBoost);
            
//             // If we have a direct reference to the attacker, register them immediately
//             if (attacker != null)
//             {
//                 // High confidence since we were just hit
//                 RegisterThreat(attacker, attacker.position, 0.9f, false);
//                 Debug.Log($"[Perception] Registered attacker directly: {attacker.name}");
//                 return;
//             }
            
//             // No direct reference - try to find attacker by scanning in damage direction
//             Vector3 damageDirection = (damageOrigin - transform.position).normalized;
            
//             // Try to find the attacker with a raycast in damage direction
//             Vector3 scanOrigin = transform.position + Vector3.up * 1.6f;
//             float maxScanDistance = 50f;
            
//             RaycastHit hit;
//             if (Physics.Raycast(scanOrigin, damageDirection, out hit, maxScanDistance, targetLayer))
//             {
//                 // Found something on the target layer
//                 Transform potentialThreat = hit.transform;
                
//                 // Register as threat with medium-high confidence
//                 RegisterThreat(potentialThreat, hit.point, 0.7f, false);
//                 Debug.Log($"[Perception] Found potential attacker via raycast: {potentialThreat.name}");
//                 return;
//             }
            
//             // Couldn't find attacker directly - create a memory of the damage location
//             // This will cause AI to investigate the area
//             if (primaryThreat == null)
//             {
//                 // Boost alertness significantly and the next visual scan might pick them up
//                 BoostAlertness(0.5f);
//                 Debug.Log($"[Perception] Took damage from {damageOrigin} but couldn't find attacker");
//             }
//         }
        


//         /// Called when a sound is heard - processes based on distance and intensity
//         public void OnSoundHeard(Vector3 position, float intensity)
//         {
//             // FIX: Ignore silent sounds immediately
//             if (intensity <= 0.01f) return;
//             float distance = Vector3.Distance(transform.position, position);
//             // NEW: If we can see our primary threat, and the sound comes from them, 
//             // don't treat it as a separate "Mystery Sound" to investigate later.
//             if (HasThreat && primaryThreat.hasVisualContact)
//             {
//                 float distToThreat = Vector3.Distance(position, primaryThreat.target.position);
//                 if (distToThreat < 3.0f) 
//                 {
//                     // Just boost alertness, don't record a new investigation point
//                     BoostAlertness(intensity); 
//                     return; 
//                 }
//             }
            
//             if (distance > hearingRange)
//                 return;
            
//             // Calculate alert boost based on:
//             // - Sound intensity (louder = more alert)
//             // - Proximity (closer = more alert)
//             float proximityFactor = 1f - (distance / hearingRange);
//             float alertBoost = intensity * proximityFactor * 1f;  //changed form 0.4f
//             BoostAlertness(alertBoost);

//             // Keep a short memory of the last heard sound for investigation movement.
//             // We prefer higher-intensity sounds, but always refresh stale memory.
//             bool shouldRefreshSoundMemory = !HasRecentSound || intensity >= 0.35f;
//             if (shouldRefreshSoundMemory)
//             {
//                 lastHeardSoundPosition = position;
//                 lastHeardSoundTime = Time.time;
//             }
            
//             // For very loud sounds (gunshots), try to look toward the sound
//             if (intensity > 0.5f)
//             {
//                 // Try to scan in that direction to see if we can spot anything
//                 Vector3 toSound = (position - transform.position).normalized;
                
//                 // If we don't have a current threat and sound is significant, 
//                 // we might want to investigate
//                 if (!HasThreat && intensity > 0.6f)
//                 {
//                     // This will cause AI to become alert and potentially investigate
//                     Debug.Log($"[Perception] Heard loud sound (intensity: {intensity:F2}) at distance {distance:F1}m");
//                 }
//             }
//         }


//         public void ClearRecentSound()
//         {
//             // Reset the time so HasRecentSound returns false
//             lastHeardSoundTime = -999f;
//         }
        
//         private void BoostAlertness(float amount)
//         {
//             alertness = Mathf.Min(1f, alertness + amount);
//         }

        
//         private void DecayAlertness()
//         {
//             if (HasThreat)
//                 return; // Don't decay while we have an active threat
            
//             alertness = Mathf.Max(0f, alertness - Time.deltaTime * 0.1f);
//         }
        
//         // Debug visualization
//         private void OnDrawGizmosSelected()
//         {
//             if (!Application.isPlaying)
//             {
//                 currentVisionAngle = visionAngle;
//             }
            
//             // Vision range circle
//             Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
//             DrawWireArc(transform.position, visionRange, currentVisionAngle);
            
//             // Vision cone lines
//             Gizmos.color = Color.yellow;
//             Vector3 leftBoundary = Quaternion.Euler(0, -currentVisionAngle * 0.5f, 0) * transform.forward;
//             Vector3 rightBoundary = Quaternion.Euler(0, currentVisionAngle * 0.5f, 0) * transform.forward;
//             Gizmos.DrawLine(transform.position, transform.position + leftBoundary * visionRange);
//             Gizmos.DrawLine(transform.position, transform.position + rightBoundary * visionRange);
            
//             // Current threat
//             if (primaryThreat != null)
//             {
//                 Gizmos.color = primaryThreat.hasVisualContact ? Color.red : Color.orange;
//                 Gizmos.DrawLine(transform.position + Vector3.up, primaryThreat.lastSeenPosition + Vector3.up);
//                 Gizmos.DrawWireSphere(primaryThreat.lastSeenPosition, 0.5f);
//             }
//         }
        
//         private void DrawWireArc(Vector3 center, float radius, float angle)
//         {
//             int segments = 30;
//             float angleStep = angle / segments;
//             Vector3 prevPoint = center + Quaternion.Euler(0, -angle * 0.5f, 0) * transform.forward * radius;
            
//             for (int i = 1; i <= segments; i++)
//             {
//                 float currentAngle = -angle * 0.5f + angleStep * i;
//                 Vector3 newPoint = center + Quaternion.Euler(0, currentAngle, 0) * transform.forward * radius;
//                 Gizmos.DrawLine(prevPoint, newPoint);
//                 prevPoint = newPoint;
//             }
//         }
//     }
// }
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace EnemyAI.Complete
{
    // ========================================================================
    // PERCEPTION SYSTEM - Detects and remembers threats
    // ========================================================================
    public class PerceptionSystem : MonoBehaviour
    {
        [System.Serializable]
        public class ThreatInfo
        {
            [System.Serializable]
            public struct PositionSample
            {
                public Vector3 position;
                public float timestamp;

                public PositionSample(Vector3 position, float timestamp)
                {
                    this.position = position;
                    this.timestamp = timestamp;
                }
            }

            public Transform target;
            public Vector3 lastSeenPosition;
            public float confidence;        // 0-1, decays over time
            public float lastUpdateTime;
            public bool hasVisualContact;
            public Vector3 estimatedVelocity;
            public bool seenThisFrame;

            private readonly List<PositionSample> positionSamples = new List<PositionSample>();
            private int maxSamples = 6;
            private float predictionLookAheadTime = 0.35f;

            public float ConfidenceNow =>
                Mathf.Max(0, confidence - (Time.time - lastUpdateTime) * 0.15f);

            public Vector3 predictedPosition =>
                lastSeenPosition + estimatedVelocity * predictionLookAheadTime;

            public void ConfigurePrediction(float lookAheadTime, int maxSampleCount)
            {
                predictionLookAheadTime = Mathf.Max(0f, lookAheadTime);
                maxSamples = Mathf.Max(2, maxSampleCount);

                while (positionSamples.Count > maxSamples)
                {
                    positionSamples.RemoveAt(0);
                }
            }

            public void AddPositionSample(Vector3 samplePosition, float sampleTime)
            {
                positionSamples.Add(new PositionSample(samplePosition, sampleTime));

                while (positionSamples.Count > maxSamples)
                {
                    positionSamples.RemoveAt(0);
                }

                estimatedVelocity = ComputeSmoothedVelocity();
            }

            private Vector3 ComputeSmoothedVelocity()
            {
                if (positionSamples.Count < 2)
                    return Vector3.zero;

                Vector3 velocitySum = Vector3.zero;
                float totalWeight = 0f;

                for (int i = 1; i < positionSamples.Count; i++)
                {
                    PositionSample previous = positionSamples[i - 1];
                    PositionSample current = positionSamples[i];
                    float dt = current.timestamp - previous.timestamp;

                    if (dt <= Mathf.Epsilon)
                        continue;

                    Vector3 segmentVelocity = (current.position - previous.position) / dt;
                    float weight = i; // Heavier weighting on recent movement
                    velocitySum += segmentVelocity * weight;
                    totalWeight += weight;
                }

                if (totalWeight <= Mathf.Epsilon)
                    return Vector3.zero;

                return velocitySum / totalWeight;
            }

            // Optional helper if you want to project onto NavMesh:
            public Vector3 GetPredictedNavMeshPosition(float timeAhead, NavMeshAgent agentReference)
            {
                // 1. Linear prediction
                Vector3 rawPrediction = lastSeenPosition + (estimatedVelocity * timeAhead);

                // 2. Sample NavMesh to find nearest valid floor point
                if (NavMesh.SamplePosition(rawPrediction, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
                    return hit.position;

                // 3. If prediction is off-mesh (e.g. over a ledge), trace path to edge
                NavMeshPath path = new NavMeshPath();
                if (agentReference.CalculatePath(rawPrediction, path) &&
                    path.status == NavMeshPathStatus.PathPartial &&
                    path.corners.Length > 0)
                {
                    // Return the last reachable point (the edge they jumped off)
                    return path.corners[path.corners.Length - 1];
                }

                return lastSeenPosition;
            }
        }

        // ====================================================================
        // CONFIG
        // ====================================================================

        [Header("Detection Settings")]
        [SerializeField] private float visionRange = 25f;
        [SerializeField] private float visionAngle = 120f;
        [SerializeField] private float hearingRange = 15f;
        [SerializeField] private LayerMask targetLayer = 1 << 6; // what can be detected
        [SerializeField] private LayerMask obstacleLayer = 1 << 0; // what can block LOS
        [SerializeField, Min(2)] private int positionSampleBufferSize = 6;
        [SerializeField, Min(0f)] private float threatPredictionLookAheadTime = 0.35f;

        [Header("Dynamic Vision Cone")]
        [SerializeField] private float relaxedVisionAngle = 90f;    // Normal patrol vision
        [SerializeField] private float alertVisionAngle = 160f;     // Investigating vision
        [SerializeField] private float combatVisionAngle = 120f;    // Combat vision
        [SerializeField] private float searchVisionAngle = 200f;    // Searching vision (almost 360°)
        [SerializeField] private float visionAngleTransitionSpeed = 2f; // How fast cone widens/narrows

        [Header("Alertness")]
        [Tooltip("0 = calm, 1 = maximum alarm.")]
        public float alertness = 0f;

        [Header("Alertness Thresholds (Hysteresis)")]
        [SerializeField, Range(0f, 1f)] private float alertEnterThreshold  = 0.4f;
        [SerializeField, Range(0f, 1f)] private float alertExitThreshold   = 0.2f;
        [SerializeField, Range(0f, 1f)] private float combatEnterThreshold = 0.7f;
        [SerializeField, Range(0f, 1f)] private float combatExitThreshold  = 0.5f;

        [Header("Alertness Decay")]
        [SerializeField, Tooltip("Base decay rate per second when relaxing.")]
        private float baseAlertnessDecayRate = 0.05f;
        [SerializeField, Tooltip("Multiplier applied in Alert state.")]
        private float alertDecayMultiplier = 0.5f;
        [SerializeField, Tooltip("Multiplier applied in Combat state. 0 = no decay while in combat.")]
        private float combatDecayMultiplier = 0.25f;

        // Sound investigation memory (used when no direct threat is known)
        private Vector3 lastHeardSoundPosition;
        public float lastHeardSoundTime = -999f;
        [SerializeField] private float soundMemoryDuration = 4f;

        [Header("Hearing Tuning")]
        [SerializeField, Range(0f, 1f)] private float minAudibleSoundIntensity = 0.02f;
        [SerializeField, Range(0f, 1f)] private float minInvestigateSoundIntensity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float loudSoundThreshold = 0.6f;

        private Dictionary<Transform, ThreatInfo> threats =
            new Dictionary<Transform, ThreatInfo>();
        private ThreatInfo primaryThreat;

        // Dynamic vision cone tracking
        private float currentVisionAngle;
        private float targetVisionAngle;

        // Internal cached alert level used for hysteresis
        private AlertLevel currentAlertLevel = AlertLevel.Relaxed;

        // Public accessors
        public ThreatInfo CurrentThreat => primaryThreat;
        public bool HasThreat => primaryThreat != null && primaryThreat.ConfidenceNow > 0.2f;
        public float CurrentVisionAngle => currentVisionAngle; // For debug visualization
        public bool HasRecentSound => Time.time - lastHeardSoundTime <= soundMemoryDuration;
        public Vector3 LastHeardSoundPosition => lastHeardSoundPosition;

        public AlertLevel GetAlertLevel()
        {
            // Hysteresis-based level selection so we don't flicker around thresholds
            switch (currentAlertLevel)
            {
                case AlertLevel.Relaxed:
                    if (alertness >= alertEnterThreshold)
                        currentAlertLevel = AlertLevel.Alert;
                    break;

                case AlertLevel.Alert:
                    if (alertness >= combatEnterThreshold)
                    {
                        currentAlertLevel = AlertLevel.Combat;
                    }
                    else if (alertness < alertExitThreshold)
                    {
                        currentAlertLevel = AlertLevel.Relaxed;
                    }
                    break;

                case AlertLevel.Combat:
                    if (alertness < combatExitThreshold)
                        currentAlertLevel = AlertLevel.Alert;
                    break;
            }

            return currentAlertLevel;
        }

        // ====================================================================
        // LIFECYCLE
        // ====================================================================

        private void Awake()
        {
            currentVisionAngle = relaxedVisionAngle;
            targetVisionAngle = relaxedVisionAngle;

            // Register with sound manager
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.RegisterListener(this);
            }
        }

        private void OnDestroy()
        {
            // Unregister when destroyed
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.UnregisterListener(this);
            }
        }

        private void Update()
        {
            UpdateDynamicVisionCone();
            ScanForTargets();
            UpdateThreats();
            DecayAlertness();
        }

        // ====================================================================
        // VISION
        // ====================================================================

        /// Dynamically adjusts vision cone based on alertness and whether we're searching
        private void UpdateDynamicVisionCone()
        {
            // Determine target angle based on state
            if (!HasThreat)
            {
                // No threat - use relaxed angle
                targetVisionAngle = relaxedVisionAngle;
            }
            else
            {
                var threat = CurrentThreat;

                if (threat.hasVisualContact)
                {
                    // In combat - use combat angle (medium)
                    targetVisionAngle = combatVisionAngle;
                }
                else if (threat.ConfidenceNow > 0.3f)
                {
                    // Investigating - widen to alert angle
                    targetVisionAngle = alertVisionAngle;
                }
                else if (threat.ConfidenceNow > 0.1f)
                {
                    // Searching - very wide angle (almost 360°)
                    targetVisionAngle = searchVisionAngle;
                }
                else
                {
                    // Lost them - return to relaxed
                    targetVisionAngle = relaxedVisionAngle;
                }
            }

            // Smoothly transition to target angle
            currentVisionAngle = Mathf.Lerp(
                currentVisionAngle,
                targetVisionAngle,
                Time.deltaTime * visionAngleTransitionSpeed
            );
        }

        private void ScanForTargets()
        {
            // Reset per-frame flags
            foreach (var kvp in threats)
            {
                kvp.Value.seenThisFrame = false;
            }

            // Find potential targets in range
            Collider[] potentials = Physics.OverlapSphere(
                transform.position,
                visionRange,
                targetLayer
            );

            foreach (Collider col in potentials)
            {
                Transform target = col.transform;

                // Check if in DYNAMIC view cone (uses currentVisionAngle instead of fixed visionAngle)
                Vector3 toTarget = target.position - transform.position;
                float angle = Vector3.Angle(transform.forward, toTarget);

                if (angle > currentVisionAngle * 0.5f)
                    continue;

                // Check line of sight
                Vector3 origin = transform.position + Vector3.up * 1.6f;
                Vector3 targetPoint = target.position + Vector3.up * 1.0f;
                Vector3 direction = (targetPoint - origin).normalized;
                float distance = Vector3.Distance(origin, targetPoint);

                bool hasLOS = !Physics.Raycast(
                    origin,
                    direction,
                    distance,
                    obstacleLayer
                );

                if (hasLOS)
                {
                    RegisterThreat(target, target.position, 1.0f, true);
                    BoostAlertness(0.5f);
                }
            }

            // After processing potentials, anything not seen this frame loses visual
            foreach (var kvp in threats)
            {
                ThreatInfo info = kvp.Value;
                if (!info.seenThisFrame)
                {
                    info.hasVisualContact = false;
                }
            }
        }

        private void RegisterThreat(Transform target, Vector3 position,
            float confidence, bool visual)
        {
            if (!threats.ContainsKey(target))
            {
                threats[target] = new ThreatInfo
                {
                    target = target,
                    lastSeenPosition = position,
                    confidence = confidence,
                    lastUpdateTime = Time.time,
                    hasVisualContact = visual,
                    estimatedVelocity = Vector3.zero,
                    seenThisFrame = true
                };

                threats[target].ConfigurePrediction(threatPredictionLookAheadTime, positionSampleBufferSize);
                threats[target].AddPositionSample(position, Time.time);
            }
            else
            {
                ThreatInfo info = threats[target];
                info.lastSeenPosition = position;
                info.confidence = Mathf.Min(1f, info.confidence + confidence);
                info.lastUpdateTime = Time.time;
                info.hasVisualContact = visual;
                info.seenThisFrame = true;
                info.ConfigurePrediction(threatPredictionLookAheadTime, positionSampleBufferSize);
                info.AddPositionSample(position, Time.time);
            }
        }

        private void UpdateThreats()
        {
            // Find highest priority threat
            ThreatInfo best = null;
            float bestScore = 0f;

            List<Transform> toRemove = new List<Transform>();

            foreach (var kvp in threats)
            {
                ThreatInfo info = kvp.Value;
                float conf = info.ConfidenceNow;

                if (conf < 0.05f)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Score based on confidence, proximity, visual contact
                float dist = Vector3.Distance(transform.position, info.lastSeenPosition);
                float proximity = 1f - Mathf.Clamp01(dist / visionRange);
                float visualBonus = info.hasVisualContact ? 1.0f : 0.3f;

                float score = conf * proximity * visualBonus;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = info;
                }
            }

            // Cleanup
            foreach (Transform t in toRemove)
            {
                threats.Remove(t);
            }

            primaryThreat = best;
        }

        // ====================================================================
        // DAMAGE -> PERCEPTION
        // ====================================================================

        /// Called when this AI takes damage - attempts to locate attacker
        public void ReportDamage(Vector3 damageOrigin, float damage, Transform attacker = null)
        {
            // Instantly boost alertness
            float alertBoost = Mathf.Clamp01(damage / 50f) * 0.8f; // Scale with damage
            BoostAlertness(alertBoost);

            // If we have a direct reference to the attacker, register them immediately
            if (attacker != null)
            {
                RegisterThreat(attacker, attacker.position, 0.9f, false);
                Debug.Log($"[Perception] Registered attacker directly: {attacker.name}");
                return;
            }

            // No direct reference - try to find attacker by scanning in damage direction
            Vector3 damageDirection = (damageOrigin - transform.position).normalized;

            Vector3 scanOrigin = transform.position + Vector3.up * 1.6f;
            float maxScanDistance = 50f;

            if (Physics.Raycast(scanOrigin, damageDirection, out RaycastHit hit, maxScanDistance, targetLayer))
            {
                Transform potentialThreat = hit.transform;

                RegisterThreat(potentialThreat, hit.point, 0.7f, false);
                Debug.Log($"[Perception] Found potential attacker via raycast: {potentialThreat.name}");
                return;
            }

            // Couldn't find attacker directly - create a memory of the damage location
            if (primaryThreat == null)
            {
                BoostAlertness(0.5f);
                Debug.Log($"[Perception] Took damage from {damageOrigin} but couldn't find attacker");
            }
        }

        // ====================================================================
        // SOUND -> PERCEPTION
        // ====================================================================

        /// Called when a sound is heard - processes based on distance and intensity
        public void OnSoundHeard(Vector3 position, float intensity)
        {
            // 1. Ignore extremely quiet sounds
            if (intensity < minAudibleSoundIntensity)
                return;

            float distance = Vector3.Distance(transform.position, position);

            // 2. If we already see our primary threat and the sound is near them,
            // just bump alertness instead of creating a new investigation point.
            if (HasThreat && primaryThreat.hasVisualContact)
            {
                float distToThreat = Vector3.Distance(position, primaryThreat.target.position);
                if (distToThreat < 3.0f)
                {
                    BoostAlertness(intensity);
                    return;
                }
            }

            // 3. Ignore sounds that are outside hearing range
            if (distance > hearingRange)
                return;

            // 4. Calculate alert boost based on intensity and proximity
            float proximityFactor = 1f - (distance / hearingRange);
            float alertBoost = intensity * proximityFactor * 0.4f;
            BoostAlertness(alertBoost);

            // 5. Only update "investigation" memory for sufficiently strong sounds
            bool shouldRefreshSoundMemory =
                !HasRecentSound || intensity >= minInvestigateSoundIntensity;

            if (shouldRefreshSoundMemory)
            {
                lastHeardSoundPosition = position;
                lastHeardSoundTime = Time.time;
            }

            // 6. Optional: handle very loud sounds (for debug / future behavior hooks)
            if (!HasThreat && intensity >= loudSoundThreshold)
            {
                Debug.Log($"[Perception] Heard loud sound (intensity: {intensity:F2}) at distance {distance:F1}m");
            }
        }

        public void ClearRecentSound()
        {
            // Reset the time so HasRecentSound returns false
            lastHeardSoundTime = -999f;
        }

        // ====================================================================
        // ALERTNESS HELPERS
        // ====================================================================

        private void BoostAlertness(float amount)
        {
            alertness = Mathf.Min(1f, alertness + amount);
        }

        private void DecayAlertness()
        {
            // Choose a decay rate based on current alert level.
            // This makes the AI calm down slowly after being spooked, and
            // optionally not at all while in active combat.
            AlertLevel level = GetAlertLevel();

            // Optionally, don't decay at all while we have a solid combat threat
            if (HasThreat && level == AlertLevel.Combat)
                return;

            float rate = baseAlertnessDecayRate;

            switch (level)
            {
                case AlertLevel.Alert:
                    rate *= alertDecayMultiplier;
                    break;
                case AlertLevel.Combat:
                    rate *= combatDecayMultiplier;
                    break;
            }

            if (rate <= 0f)
                return;

            alertness = Mathf.Max(0f, alertness - Time.deltaTime * rate);
        }

        // ====================================================================
        // DEBUG DRAWING
        // ====================================================================

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                currentVisionAngle = visionAngle;
            }

            // Vision range circle
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            DrawWireArc(transform.position, visionRange, currentVisionAngle);

            // Vision cone lines
            Gizmos.color = Color.yellow;
            Vector3 leftBoundary = Quaternion.Euler(0, -currentVisionAngle * 0.5f, 0) * transform.forward;
            Vector3 rightBoundary = Quaternion.Euler(0, currentVisionAngle * 0.5f, 0) * transform.forward;
            Gizmos.DrawLine(transform.position, transform.position + leftBoundary * visionRange);
            Gizmos.DrawLine(transform.position, transform.position + rightBoundary * visionRange);

            // Current threat
            if (primaryThreat != null)
            {
                Gizmos.color = primaryThreat.hasVisualContact ? Color.red : Color.yellow;
                Gizmos.DrawLine(transform.position + Vector3.up, primaryThreat.lastSeenPosition + Vector3.up);
                Gizmos.DrawWireSphere(primaryThreat.lastSeenPosition, 0.5f);
            }
        }

        private void DrawWireArc(Vector3 center, float radius, float angle)
        {
            int segments = 30;
            float angleStep = angle / segments;
            Vector3 prevPoint = center + Quaternion.Euler(0, -angle * 0.5f, 0) * transform.forward * radius;

            for (int i = 1; i <= segments; i++)
            {
                float currentAngle = -angle * 0.5f + angleStep * i;
                Vector3 newPoint = center + Quaternion.Euler(0, currentAngle, 0) * transform.forward * radius;
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
    }
}
