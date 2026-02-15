using UnityEngine;
using System.Collections.Generic;

namespace EnemyAI.Complete
{
    /// <summary>
    /// Global manager that relays sound events to all AI perception systems
    /// Add this to a GameObject in your scene (only need one)
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }
        
        [Header("Settings")]
        [SerializeField] private bool visualizeDebugSounds = false;
        [SerializeField] private float debugVisualizationDuration = 1f;
        
        private List<PerceptionSystem> listeners = new List<PerceptionSystem>();
        
        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        /// <summary>
        /// Register an AI perception system to receive sound events
        /// </summary>
        public void RegisterListener(PerceptionSystem listener)
        {
            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
                Debug.Log($"[SoundManager] Registered listener: {listener.name}");

            }
        }
        
        /// <summary>..
        /// Unregister an AI perception system (called when AI is destroyed)
        /// </summary>
        public void UnregisterListener(PerceptionSystem listener)
        {
            listeners.Remove(listener);
        }
        
        /// <summary>
        /// Broadcast a sound to all nearby AI
        /// </summary>
        /// <param name="position">Where the sound occurred</param>
        /// <param name="intensity">How loud/alerting (0-1)</param>
        /// <param name="range">Maximum distance AI can hear it</param>
        public void BroadcastSound(Vector3 position, float intensity, float range)
        {
            // Clean up any null references
            listeners.RemoveAll(l => l == null);
            
            foreach (var listener in listeners)
            {
                if (listener == null || !listener.enabled)
                    continue;
                
                float distance = Vector3.Distance(listener.transform.position, position);
                
                // Only notify if within range
                if (distance <= range)
                {
                    listener.OnSoundHeard(position, intensity);
                }
            }
            
            // Debug visualization
            if (visualizeDebugSounds)
            {
                Debug.DrawLine(position, position + Vector3.up * 3f, Color.yellow, debugVisualizationDuration);
                
                // Draw sphere to show range
                DrawDebugSphere(position, range, Color.yellow, debugVisualizationDuration);
            }
        }
        
        private void DrawDebugSphere(Vector3 center, float radius, Color color, float duration)
        {
            // Draw a circle on XZ plane
            int segments = 20;
            float angleStep = 360f / segments;
            
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;
                
                Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);
                
                Debug.DrawLine(p1, p2, color, duration);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            if (listeners == null)
                return;
            
            // Draw connections to all registered listeners
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            foreach (var listener in listeners)
            {
                if (listener != null)
                {
                    Gizmos.DrawLine(transform.position, listener.transform.position);
                }
            }
        }
    }
}
