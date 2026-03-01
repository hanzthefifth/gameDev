using System.Collections;
using UnityEngine;

namespace MyGame
{
    /// Singleton that freezes Time.timeScale for a fixed number of physics frames,
    /// then restores it. Attach to any persistent GameObject in your scene (e.g. GameManager).
    ///
    /// Usage:
    ///   HitStop.Instance.Trigger(frames);
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        private Coroutine activeRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// Freeze for <paramref name="frames"/> fixed-update ticks then resume.
        /// If a hit stop is already running it is replaced by the longer of the two.
        public void Trigger(int frames)
        {
            if (frames <= 0) return;

            // If already frozen, only extend — never shorten a running stop.
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                Time.timeScale = 0f;
            }

            activeRoutine = StartCoroutine(RunHitStop(frames));
        }

        private IEnumerator RunHitStop(int frames)
        {
            Time.timeScale = 0f;

            // WaitForSecondsRealtime is unaffected by timeScale.
            float frameDuration = Time.fixedDeltaTime;
            yield return new WaitForSecondsRealtime(frameDuration * frames);

            Time.timeScale = 1f;
            activeRoutine  = null;
        }
    }
}