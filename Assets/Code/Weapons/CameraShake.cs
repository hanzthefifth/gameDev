using System.Collections;
using UnityEngine;

namespace MyGame
{
    /// Attach to your camera GameObject (or a camera rig parent).
    /// Call Shake(intensity, duration) from anywhere to trigger a shake.
    ///
    /// Works by offsetting the local position with Perlin noise each frame.
    /// Smoothly fades out over the duration so it never snaps back.
    public class CameraShake : MonoBehaviour
    {
        // Seed offsets so X and Y noise tracks don't overlap.
        private const float NoiseOffsetX = 0f;
        private const float NoiseOffsetY = 100f;

        [Header("Shake Settings")]
        [Tooltip("How fast the noise moves — higher = jitterier.")]
        [SerializeField] private float noiseFrequency = 25f;

        private Vector3    originalLocalPosition;
        private Coroutine  activeShake;

        private void Awake()
        {
            originalLocalPosition = transform.localPosition;
        }

        /// Trigger a shake. If one is already running, the stronger intensity wins.
        public void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;

            if (activeShake != null)
                StopCoroutine(activeShake);

            activeShake = StartCoroutine(RunShake(intensity, duration));
        }

        private IEnumerator RunShake(float intensity, float duration)
        {
            float elapsed  = 0f;
            float noiseT   = Random.Range(0f, 100f); // random start in noise space each shake

            while (elapsed < duration)
            {
                // Fade from full intensity to zero over the duration.
                float t         = elapsed / duration;
                float currentAmp = Mathf.Lerp(intensity, 0f, t);

                // Sample Perlin noise for smooth random offset.
                float x = (Mathf.PerlinNoise(noiseT + NoiseOffsetX, 0f) - 0.5f) * 2f * currentAmp;
                float y = (Mathf.PerlinNoise(noiseT + NoiseOffsetY, 0f) - 0.5f) * 2f * currentAmp;

                transform.localPosition = originalLocalPosition + new Vector3(x, y, 0f);

                noiseT   += Time.unscaledDeltaTime * noiseFrequency;
                elapsed  += Time.unscaledDeltaTime; // unscaled so shake still works during hit stop
                yield return null;
            }

            transform.localPosition = originalLocalPosition;
            activeShake = null;
        }
    }
}