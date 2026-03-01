using UnityEngine;

namespace MyGame
{
    /// Concrete melee weapon: knife, short blade — tight forward spherecast, single target.
    ///
    /// Animation contract (Character Animator, Layer Actions):
    ///   "Melee Attack"        — swing clip
    ///       "OnMeleeHit"          animation event at impact frame
    ///       "AnimationEndedMelee" animation event on last frame
    ///
    /// Weapon Animator (this GameObject, optional cosmetic):
    ///   "Melee" — weapon-mesh swing
    ///   "Idle"  — resting pose
    ///
    /// Hit flow:
    ///   Live enemy  → TakeDamage → hit stop (normal) → screen shake (normal)
    ///                 If killing blow: ragdoll force + hit stop (kill) + screen shake (kill)
    ///   Dead enemy  → ragdoll force only (no damage)
    ///   Prop/RB     → AddForceAtPosition + hit stop (normal)
    public class MeleeWeapon : MeleeWeaponBehaviour
    {
        [Header("Config")]
        [SerializeField] private MeleeWeaponConfig config;

        [Header("Animation")]
        [SerializeField] private RuntimeAnimatorController animatorController;

        [Header("Screen Shake")]
        [Tooltip("Assign a CameraShake component here, or leave null to skip shake.")]
        [SerializeField] private CameraShake cameraShake;

        // ---unity
        protected override void Awake()
        {
            base.Awake();

            if (config != null)
            {
                attackCooldown   = config.attackCooldown;
                postMeleeLockout = config.postMeleeLockout;

                if (animatorController == null)
                    animatorController = config.animatorController;
            }

            // Auto-find CameraShake if not assigned.
            if (cameraShake == null)
                cameraShake = FindFirstObjectByType<CameraShake>();
        }

        // ── WeaponBehaviour identity ──────────────────────────────────────────────
        [SerializeField] private Sprite placeholderSprite;
        public override Sprite GetSpriteBody() => placeholderSprite;
        public override RuntimeAnimatorController GetAnimatorController() => animatorController;

        public override AudioClip GetAudioClipHolster()   => config != null ? config.audioClipHolster   : null;
        public override AudioClip GetAudioClipUnholster() => config != null ? config.audioClipUnholster : null;
        public override AudioClip GetAudioClipFire()      => config != null ? config.audioClipSwing     : null;

        // ── MeleeWeaponBehaviour ──────────────────────────────────────────────────
        protected override void OnAttackStarted()
        {
            base.OnAttackStarted();
            if (config != null) PlayClip(config.audioClipSwing);
            Debug.Log("Swing triggered");
        }

        /// Spherecast straight forward — hits the first target in range.
        protected override void PerformHit(Transform cameraTransform)
        {
            if (config == null) return;

            Vector3 origin    = cameraTransform.position;
            Vector3 direction = cameraTransform.forward;

            if (!Physics.SphereCast(origin, config.hitRadius, direction,
                                    out RaycastHit hit, config.hitRange, config.hitMask))
                return;

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                if (damageable.IsDead)
                {
                    // Already dead — just push the ragdoll, no damage or hit stop.
                    ApplyRagdollForce(hit, direction, config.ragdollKillForce);
                    return;
                }

                // Deal damage first so IsDead updates immediately.
                damageable.TakeDamage(config.damage);

                var combatAI = hit.collider.GetComponentInParent<EnemyAI.Complete.CombatAI>();
                if (combatAI != null)
                    combatAI.OnTakeDamage(config.damage, origin, cameraTransform);

                if (damageable.IsDead)
                {
                    // Killing blow — big feedback + ragdoll launch.
                    PlayClip(config.audioClipHitKill != null ? config.audioClipHitKill : config.audioClipHit);
                    TriggerHitStop(config.hitStopFramesKill);
                    TriggerScreenShake(config.screenShakeIntensityKill, config.screenShakeDuration);
                    ApplyRagdollForce(hit, direction, config.ragdollKillForce);
                }
                else
                {
                    // Hit but alive — lighter feedback, no ragdoll.
                    PlayClip(config.audioClipHit);
                    TriggerHitStop(config.hitStopFramesNormal);
                    TriggerScreenShake(config.screenShakeIntensityNormal, config.screenShakeDuration);
                }
            }
            else
            {
                // Not a damageable — push any rigidbody (props, physics objects, doors, etc.)
                if (hit.rigidbody != null)
                {
                    hit.rigidbody.AddForceAtPosition(
                        direction * config.knockbackForce,
                        hit.point,
                        ForceMode.Impulse);
                }

                PlayClip(config.audioClipHitProp != null ? config.audioClipHitProp : config.audioClipHit);
                TriggerHitStop(config.hitStopFramesNormal);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// Applies ragdoll kill force — forward direction biased slightly upward
        /// so the body lifts and flies back L4D2 style.
        private void ApplyRagdollForce(RaycastHit hit, Vector3 direction, float force)
        {
            RagdollController ragdoll = hit.collider.GetComponentInParent<RagdollController>();
            if (ragdoll == null) return;

            Vector3 impulseDir = (direction + Vector3.up * config.ragdollLiftBias).normalized;
            ragdoll.ApplyImpact(impulseDir * force, hit.point, hit.rigidbody);
        }

        private void TriggerHitStop(int frames)
        {
            if (frames <= 0) return;
            if (HitStop.Instance != null)
                HitStop.Instance.Trigger(frames);
        }

        private void TriggerScreenShake(float intensity, float duration)
        {
            if (cameraShake != null)
                cameraShake.Shake(intensity, duration);
        }
    }
}