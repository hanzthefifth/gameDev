using UnityEngine;

namespace MyGame
{
    [CreateAssetMenu(
        menuName = "FPS/Weapon Config",
        fileName = "NewWeaponConfig")]
    public class WeaponConfig : ScriptableObject
    {
        [Header("Identity")]
        public string weaponId;
        public string displayName;

        [Header("Damage")]
        public float baseDamage = 20f;
        public DamageType damageType = DamageType.Bullet;

        [Tooltip("Force applied to rigidbodies when hit.")]
        public float impactForce = 30f;

        [Header("Range")]
        [Tooltip("Maximum distance for accurate hitscan.")]
        public float maxDistance = 500f;
        [Header("Sound")]
        public float gunshotSoundIntensity = 1f;
        public float gunshotSoundRange = 45f;
    }
}
