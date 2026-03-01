using UnityEngine;

namespace MyGame
{
    /// Abstract base class for all melee weapons.
    /// Sits between WeaponBehaviour and concrete types like MeleeWeapon, HeavyMeleeWeapon, ThrowableWeapon.
    ///
    /// Owns: attack cooldown, post-swing fire lockout, weapon-mesh animation trigger, audio helper.
    /// Does NOT own: hit detection, damage application, knockback — each subclass handles
    /// those in PerformHit() using whatever physics shape and multi-hit logic it needs.
    ///
    /// Subclasses must implement:
    ///   - PerformHit(Transform camera)   — full hit detection + damage for this weapon type
    ///   - GetAnimatorController()        — character anim controller for this weapon
    ///   - GetSpriteBody()                — HUD sprite
    ///   - Audio clip getters
    public abstract class MeleeWeaponBehaviour : WeaponBehaviour
    {
        // ── Shared config ─────────────────────────────────────────────────────────
        [Header("Melee Base")]
        [SerializeField] protected float attackCooldown   = 0.55f;
        [SerializeField] protected float postMeleeLockout = 0.35f;
        [SerializeField] protected bool  throwable        = false;

        [Header("Audio (Base)")]
        [SerializeField] protected AudioSource audioSource;

        // ── Internal ─────────────────────────────────────────────────────────────
        protected Animator weaponAnimator;
        private   float    nextAttackTime;

        // ── Unity ─────────────────────────────────────────────────────────────────
        protected override void Awake()
        {
            weaponAnimator = GetComponent<Animator>();
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// Whether this weapon can be thrown. Character checks this to fork input.
        public virtual bool IsThrowable => throwable;

        /// How long after AnimationEndedMelee before the player can fire a ranged weapon again.
        public float PostMeleeLockout => postMeleeLockout;

        /// Called by Character.PlayMeleeAnimation(). Gates on cooldown.
        /// Returns true if the attack was accepted.
        public bool TryAttack()
        {
            if (Time.time < nextAttackTime) return false;
            nextAttackTime = Time.time + attackCooldown;
            OnAttackStarted();
            return true;
        }

        /// Called by Character.OnMeleeHit() at the animation event impact frame.
        /// Delegates to the subclass to do its own hit detection and damage.
        public void ExecuteHit(Transform cameraTransform)
        {
            if (cameraTransform == null) return;
            PerformHit(cameraTransform);
        }

        // ── Abstract — subclasses must implement ──────────────────────────────────

        /// Full hit detection AND damage application for this weapon type.
        /// No shared helper — use whatever physics shape and damage logic fits:
        ///   MeleeWeapon      → SphereCast, single target
        ///   HeavyMeleeWeapon → OverlapBox arc, multiple targets
        ///   ThrowableWeapon  → spawn projectile, damage on impact
        protected abstract void PerformHit(Transform cameraTransform);

        // ── Virtual — subclasses may override ────────────────────────────────────

        /// Called the moment TryAttack succeeds, before the character animator plays.
        /// Default plays the weapon-mesh "Melee" clip. Override to add swing audio etc.
        protected virtual void OnAttackStarted()
        {
            if (weaponAnimator != null)
                weaponAnimator.Play("Melee", 0, 0f);
        }

        // ── Ranged concepts — sealed, not applicable to melee ─────────────────────
        // Hybrid weapons that both shoot and melee should NOT inherit from here.

        public sealed override void Fire(float spreadMultiplier = 1.0f) { }
        public sealed override void Reload()                             { }
        public sealed override void CancelReload()                       { }
        public sealed override void FillAmmunition(int amount)           { }
        public sealed override void EjectCasing()                        { }

        public sealed override bool  HasAmmunition()        => true;
        public sealed override bool  IsFull()               => true;
        public sealed override int   GetAmmunitionCurrent() => 1;
        public sealed override int   GetAmmunitionTotal()   => 1;
        public sealed override bool  IsAutomatic()          => false;
        public sealed override float GetRateOfFire()        => attackCooldown > 0f ? 60f / attackCooldown : 60f;

        public sealed override AudioClip GetAudioClipReload()      => null;
        public sealed override AudioClip GetAudioClipReloadEmpty() => null;
        public sealed override AudioClip GetAudioClipFireEmpty()   => null;

        public sealed override WeaponAttachmentManagerBehaviour GetAttachmentManager() => null;

        public override Animator GetAnimator() => weaponAnimator;

        // ── Audio helper — available to all subclasses ────────────────────────────
        protected void PlayClip(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }
    }
}