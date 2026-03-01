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
    public class MeleeWeapon : MeleeWeaponBehaviour
    {
        [Header("Config")]
        [SerializeField] private MeleeWeaponConfig config;

        [Header("Animation")]
        [SerializeField] private RuntimeAnimatorController animatorController;

        // ── Unity ─────────────────────────────────────────────────────────────────
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
        }

        // ── WeaponBehaviour identity ──────────────────────────────────────────────
        public override Sprite                    GetSpriteBody()         => null;
        public override RuntimeAnimatorController GetAnimatorController() => animatorController;

        public override AudioClip GetAudioClipHolster()   => config != null ? config.audioClipHolster   : null;
        public override AudioClip GetAudioClipUnholster() => config != null ? config.audioClipUnholster : null;
        public override AudioClip GetAudioClipFire()      => config != null ? config.audioClipSwing     : null;

        // ── MeleeWeaponBehaviour ──────────────────────────────────────────────────
        protected override void OnAttackStarted()
        {
            base.OnAttackStarted();
            if (config != null) PlayClip(config.audioClipSwing);
        }

        /// Spherecast straight forward — hits the first target in range.
        /// Owns full hit detection and damage; no shared helper involved.
        protected override void PerformHit(Transform cameraTransform)
        {
            if (config == null) return;

            Vector3 origin    = cameraTransform.position;
            Vector3 direction = cameraTransform.forward;

            if (!Physics.SphereCast(origin, config.hitRadius, direction,
                                    out RaycastHit hit, config.hitRange, config.hitMask))
                return;

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                Vector3 force = direction.normalized * config.knockbackForce;
                damageable.TakeDamage(config.damage, force, hit.point, hit.rigidbody);

                var combatAI = hit.collider.GetComponentInParent<EnemyAI.Complete.CombatAI>();
                if (combatAI != null)
                    combatAI.OnTakeDamage(config.damage, origin, cameraTransform);

                PlayClip(config.audioClipHit);
            }
            else if (hit.rigidbody != null)
            {
                hit.rigidbody.AddForceAtPosition(
                    direction * config.knockbackForce,
                    hit.point,
                    ForceMode.Impulse);
            }
        }
    }
}