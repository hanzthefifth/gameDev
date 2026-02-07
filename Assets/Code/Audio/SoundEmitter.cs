using UnityEngine;
using UnityEngine.UIElements;

namespace EnemyAI.Complete
{

    /// Attach this to any object that makes sound (player, weapons, doors, etc.)
    public class SoundEmitter : MonoBehaviour
    {
        [Header("Sound Settings")]
        [SerializeField] private bool debugSounds = false;
        [SerializeField] private float defaultGunshotIntensity = 10f;
        [SerializeField] private float defaultGunshotRange = 30f;

        

        /// Emit a sound that nearby AI can hear
        /// <param name="position">Where the sound originated</param>
        /// <param name="intensity">How loud/important (0-1). Higher = more alertness</param>
        /// <param name="range">How far the sound travels in units</param>
        public void EmitSound(Vector3 position, float intensity, float range)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.BroadcastSound(position, intensity, range);
                
                if (debugSounds)
                {
                    Debug.Log($"[SoundEmitter] Sound emitted at {position} | Intensity: {intensity} | Range: {range}");
                }
            }
            else
            {
                Debug.LogWarning("[SoundEmitter] SoundManager not found in scene!");
            }
        }
        

        /// Quick helper for footstep sounds
        public void EmitFootstep()
        {
            EmitSound(transform.position, 0.2f, 8f);
        }
        

        /// Quick helper for gunshot sounds
        public void EmitGunshot(float intensity, float range)
        {
            EmitSound(transform.position, defaultGunshotIntensity, defaultGunshotRange);
        }
        
        /// Quick helper for loud events (explosions, doors, etc)
        public void EmitLoudEvent()
        {
            EmitSound(transform.position, 1.0f, 50f);
        }
    }
}
