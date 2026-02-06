using UnityEngine;
using UnityEngine.AI;

namespace EnemyAI.Complete
{
    /// <summary>
    /// Visual debugging for the AI system
    /// Attach to any enemy to see what they're thinking
    /// </summary>
    public class AIDebugVisualizer : MonoBehaviour
    {
        [Header("What to Show")]
        [SerializeField] private bool showVisionCone = true;
        [SerializeField] private bool showCurrentTarget = true;
        [SerializeField] private bool showLastKnownPosition = true;
        [SerializeField] private bool showPredictedPosition = true;
        [SerializeField] private bool showNavPath = true;
        [SerializeField] private bool showStateInfo = true;
        [SerializeField] private bool showAlertness = true;
        
        private PerceptionSystem perception;
        private CombatStateMachine stateMachine;
        private NavMeshAgent agent;
        
        private void Awake()
        {
            perception = GetComponent<PerceptionSystem>();
            stateMachine = GetComponent<CombatStateMachine>();
            agent = GetComponent<NavMeshAgent>();
        }
        
        private void OnDrawGizmos()
        {
            if (perception == null) perception = GetComponent<PerceptionSystem>();
            if (stateMachine == null) stateMachine = GetComponent<CombatStateMachine>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            
            if (perception == null) return;
            
            // Vision cone
            if (showVisionCone)
            {
                DrawVisionCone();
            }
            
            // Alertness indicator
            if (showAlertness)
            {
                DrawAlertnessBar();
            }
            
            // Current threat
            if (showCurrentTarget && perception.CurrentThreat != null)
            {
                DrawThreatInfo();
            }
            
            // NavMesh path
            if (showNavPath && agent != null && agent.hasPath)
            {
                DrawNavPath();
            }
        }
        
        private void DrawVisionCone()
        {
            // Get vision settings via reflection or assume defaults
            float visionRange = 25f;
            float visionAngle = 120f;
            
            // Vision range circle
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            DrawWireArc(transform.position, visionRange, visionAngle);
            
            // Vision cone lines
            Gizmos.color = Color.yellow;
            Vector3 leftBoundary = Quaternion.Euler(0, -visionAngle * 0.5f, 0) * transform.forward;
            Vector3 rightBoundary = Quaternion.Euler(0, visionAngle * 0.5f, 0) * transform.forward;
            Gizmos.DrawLine(transform.position, transform.position + leftBoundary * visionRange);
            Gizmos.DrawLine(transform.position, transform.position + rightBoundary * visionRange);
        }
        
        private void DrawAlertnessBar()
        {
            Vector3 barPos = transform.position + Vector3.up * 3f;
            float barWidth = 2f;
            float barHeight = 0.2f;
            
            // Background (black)
            Gizmos.color = Color.black;
            Gizmos.DrawCube(barPos, new Vector3(barWidth, barHeight, 0.05f));
            
            // Alertness fill (green -> yellow -> red)
            float alertness = perception.alertness;
            Color alertColor = Color.Lerp(Color.green, Color.red, alertness);
            Gizmos.color = alertColor;
            Vector3 fillSize = new Vector3(barWidth * alertness, barHeight, 0.06f);
            Vector3 fillPos = barPos + Vector3.left * (barWidth * 0.5f * (1f - alertness));
            Gizmos.DrawCube(fillPos, fillSize);
        }
        
        private void DrawThreatInfo()
        {
            var threat = perception.CurrentThreat;
            
            // Line to target
            if (threat.hasVisualContact && threat.target != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position + Vector3.up, threat.target.position + Vector3.up);
            }
            
            // Last known position
            if (showLastKnownPosition)
            {
                Gizmos.color = threat.hasVisualContact ? Color.red : Color.orange;
                Gizmos.DrawWireSphere(threat.lastSeenPosition, 0.5f);
                Gizmos.DrawLine(
                    threat.lastSeenPosition, 
                    threat.lastSeenPosition + Vector3.up * 2f
                );
            }
            
            // Predicted position
            if (showPredictedPosition && threat.estimatedVelocity.sqrMagnitude > 0.01f)
            {
                Gizmos.color = Color.cyan;
                Vector3 predicted = threat.predictedPosition;
                Gizmos.DrawWireSphere(predicted, 0.3f);
                
                // Velocity arrow
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(threat.lastSeenPosition, predicted);
                DrawArrow(threat.lastSeenPosition, threat.estimatedVelocity.normalized * 2f, 0.3f);
            }
            
            // Confidence indicator
            float confidence = threat.ConfidenceNow;
            Gizmos.color = Color.Lerp(Color.gray, Color.white, confidence);
            Gizmos.DrawWireCube(
                threat.lastSeenPosition + Vector3.up * 2.5f, 
                Vector3.one * (0.3f + confidence * 0.5f)
            );
        }
        
        private void DrawNavPath()
        {
            if (agent.path.corners.Length < 2) return;
            
            Gizmos.color = Color.magenta;
            for (int i = 0; i < agent.path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(agent.path.corners[i], agent.path.corners[i + 1]);
                Gizmos.DrawWireSphere(agent.path.corners[i], 0.2f);
            }
            
            // Destination
            if (agent.hasPath)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(agent.destination, 0.5f);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            if (!showStateInfo || stateMachine == null) return;
            
            // Draw state name in scene view
            #if UNITY_EDITOR
            var style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            
            Vector3 labelPos = transform.position + Vector3.up * 2.5f;
            string stateText = $"State: {stateMachine.CurrentState}\nAlertness: {perception.alertness:F2}";
            
            if (perception.CurrentThreat != null)
            {
                stateText += $"\nConfidence: {perception.CurrentThreat.ConfidenceNow:F2}";
                stateText += $"\nHas LOS: {perception.CurrentThreat.hasVisualContact}";
            }
            
            UnityEditor.Handles.Label(labelPos, stateText, style);
            #endif
        }
        
        // Helper methods
        private void DrawWireArc(Vector3 center, float radius, float angle)
        {
            int segments = 20;
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
        
        private void DrawArrow(Vector3 pos, Vector3 direction, float arrowHeadLength = 0.25f)
        {
            Gizmos.DrawRay(pos, direction);
            
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * new Vector3(0, 0, 1);
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * new Vector3(0, 0, 1);
            Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
            Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
        }
    }
}