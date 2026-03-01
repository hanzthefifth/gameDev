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
        public float baseDamage = 100f;
        public DamageType damageType = DamageType.Bullet;

        [Header("Range")]
        [Tooltip("Maximum distance for accurate hitscan.")]
        public float maxDistance = 500f;

        [Header("Sound")]
        public float gunshotSoundIntensity = 1f;
        public float gunshotSoundRange = 45f;
    }
}