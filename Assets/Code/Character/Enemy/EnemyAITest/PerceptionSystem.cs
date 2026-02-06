using UnityEngine;
using System.Collections.Generic;

namespace EnemyAI.Complete{
// ========================================================================
    // PERCEPTION SYSTEM - Detects and remembers threats
    // ========================================================================
    
    public class PerceptionSystem : MonoBehaviour
    {
        [System.Serializable]
        public class ThreatInfo
        {
            public Transform target;
            public Vector3 lastSeenPosition;
            public float confidence;        // 0-1, decays over time
            public float lastUpdateTime;
            public bool hasVisualContact;
            
            public float ConfidenceNow => 
                Mathf.Max(0, confidence - (Time.time - lastUpdateTime) * 0.15f);
        }
        
        [Header("Detection Settings")]
        [SerializeField] private float visionRange = 25f;
        [SerializeField] private float visionAngle = 120f;
        [SerializeField] private float hearingRange = 15f;
        [SerializeField] private LayerMask targetLayer = 1 << 6; // Player layer
        [SerializeField] private LayerMask obstacleLayer = 1 << 0; // Default layer
        
        [Header("Alertness")]
        public float alertness = 0f;  // 0 = calm, 1 = full combat
        private const float ALERT_THRESHOLD = 0.4f;
        private const float COMBAT_THRESHOLD = 0.7f;
        
        private Dictionary<Transform, ThreatInfo> threats = new Dictionary<Transform, ThreatInfo>();
        private ThreatInfo primaryThreat;
        
        // Public accessors
        public ThreatInfo CurrentThreat => primaryThreat;
        public bool HasThreat => primaryThreat != null && primaryThreat.ConfidenceNow > 0.2f;
        public AlertLevel GetAlertLevel()
        {
            if (alertness < ALERT_THRESHOLD) return AlertLevel.Relaxed;
            if (alertness < COMBAT_THRESHOLD) return AlertLevel.Alert;
            return AlertLevel.Combat;
        }
        
        private void Update()
        {
            ScanForTargets();
            UpdateThreats();
            DecayAlertness();
        }
        
        private void ScanForTargets()
        {
            // Find potential targets in range
            Collider[] potentials = Physics.OverlapSphere(
                transform.position, 
                visionRange, 
                targetLayer
            );
            
            foreach (Collider col in potentials)
            {
                Transform target = col.transform;
                
                // Check if in view cone
                Vector3 toTarget = target.position - transform.position;
                float angle = Vector3.Angle(transform.forward, toTarget);
                
                if (angle > visionAngle * 0.5f)
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
                    hasVisualContact = visual
                };
            }
            else
            {
                ThreatInfo info = threats[target];
                info.lastSeenPosition = position;
                info.confidence = Mathf.Min(1f, info.confidence + confidence);
                info.lastUpdateTime = Time.time;
                info.hasVisualContact = visual;
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
        
        public void ReportDamage(Vector3 origin, float damage)
        {
            // Instantly go on alert
            BoostAlertness(0.8f);
            
            // Try to find attacker
            // (In real implementation, damage would pass attacker reference)
            Vector3 approximatePosition = origin;
            
            // If we don't have a primary threat, investigate damage source
            if (primaryThreat == null)
            {
                // Create low-confidence memory
                // Actual target would be found through subsequent scans
            }
        }
        
        public void OnSoundHeard(Vector3 position, float intensity)
        {
            float distance = Vector3.Distance(transform.position, position);
            
            if (distance > hearingRange)
                return;
            
            // Boost alertness based on proximity and intensity
            float alertBoost = intensity * (1f - distance / hearingRange) * 0.3f;
            BoostAlertness(alertBoost);
        }
        
        private void BoostAlertness(float amount)
        {
            alertness = Mathf.Min(1f, alertness + amount);
        }
        
        private void DecayAlertness()
        {
            if (HasThreat)
                return; // Don't decay while we have an active threat
            
            alertness = Mathf.Max(0f, alertness - Time.deltaTime * 0.1f);
        }
    }
}