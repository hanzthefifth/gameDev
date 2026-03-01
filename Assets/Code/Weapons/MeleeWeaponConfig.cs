using UnityEngine;

namespace MyGame
{
    /// Base config ScriptableObject for all melee weapons.
    /// Subclass this for weapon-type-specific fields
    /// e.g. HeavyMeleeWeaponConfig adds sweep arc angle, ThrowableWeaponConfig adds projectile prefab.
    ///
    /// Create via: Assets > Create > MyGame > Melee Weapon Config
    [CreateAssetMenu(menuName = "MyGame/Weapons/Melee Weapon Config", fileName = "MeleeWeaponConfig")]
    public class MeleeWeaponConfig : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Combat Knife";

        [Header("Damage")]
        [Tooltip("Damage dealt on hit. Set above enemy max health for instakill.")]
        public float damage = 150f;

        [Header("Knockback")]
        [Tooltip("Force applied to props and non-damageable rigidbodies.")]
        public float knockbackForce = 8f;

        [Tooltip("Force applied to enemy ragdoll on the killing blow via RagdollController.ApplyImpact.")]
        public float ragdollKillForce = 500f;

        [Tooltip("Upward angle mixed into kill force so the body lifts slightly. 0 = purely forward.")]
        [Range(0f, 0.5f)]
        public float ragdollLiftBias = 0.25f;

        [Header("Hit Detection")]
        [Tooltip("Spherecast radius. Larger = more forgiving contact.")]
        public float hitRadius = 0.35f;

        [Tooltip("How far forward the hit scan reaches from the player camera.")]
        public float hitRange = 1.8f;

        [Tooltip("Layers that can be hit.")]
        public LayerMask hitMask = ~0;

        [Header("Timing")]
        [Tooltip("Minimum seconds between swings.")]
        public float attackCooldown = 0.55f;

        [Tooltip("Seconds after the swing ends before the player can fire a ranged weapon again.")]
        public float postMeleeLockout = 0.35f;

        [Header("Hit Stop")]
        [Tooltip("Frames to freeze on a regular hit. 0 to disable.")]
        public int hitStopFramesNormal = 3;

        [Tooltip("Frames to freeze on the killing blow.")]
        public int hitStopFramesKill = 5;

        [Header("Screen Shake")]
        public float screenShakeIntensityNormal = 0.05f;
        public float screenShakeIntensityKill   = 0.12f;
        public float screenShakeDuration        = 0.12f;

        [Header("Audio")]
        public AudioClip audioClipSwing;
        public AudioClip audioClipHit;
        [Tooltip("Played on the killing blow. Falls back to audioClipHit if unassigned.")]
        public AudioClip audioClipHitKill;
        [Tooltip("Played when hitting a non-damageable prop. Falls back to audioClipHit if unassigned.")]
        public AudioClip audioClipHitProp;
        public AudioClip audioClipHolster;
        public AudioClip audioClipUnholster;

        [Header("Animation")]
        [Tooltip("Character AnimatorController to use when this weapon is equipped.")]
        public RuntimeAnimatorController animatorController;
    }
}