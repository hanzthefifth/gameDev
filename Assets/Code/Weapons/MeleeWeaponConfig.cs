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
        [Tooltip("Damage dealt on hit.")]
        public float damage = 45f;

        [Tooltip("Knockback impulse magnitude applied to hit targets.")]
        public float knockbackForce = 8f;

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

        [Header("Audio")]
        public AudioClip audioClipSwing;
        public AudioClip audioClipHit;
        public AudioClip audioClipHolster;
        public AudioClip audioClipUnholster;

        [Header("Animation")]
        [Tooltip("Character AnimatorController to use when this weapon is equipped.")]
        public RuntimeAnimatorController animatorController;
    }
}