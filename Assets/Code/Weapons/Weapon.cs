using UnityEngine;
using EnemyAI.Complete;

namespace MyGame
{
    /// Weapon. Handles firing, reloading, damage, and weapon-side animation.
    public class Weapon : WeaponBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Config")]
        [Tooltip("Optional data asset that defines damage, range, etc. for this weapon.")]
        [SerializeField] public WeaponConfig weaponConfig;

        [Header("Firing")]
        [Tooltip("Is this weapon automatic? Holding fire will continuously shoot.")]
        [SerializeField] private bool automatic;
        [Tooltip("How fast the projectiles travel.")]
        [SerializeField] private float projectileImpulse = 400.0f;
        [Tooltip("Rounds per minute. Determines fire rate.")]
        [SerializeField] private int roundsPerMinutes = 200;
        [Tooltip("Layer mask for hit detection.")]
        [SerializeField] private LayerMask mask;
        [Tooltip("Max distance for accurate line-trace hits.")]
        [SerializeField] private float maximumDistance = 500.0f;

        [Header("Damage")]
        [Tooltip("Damage per hit. Overridden by WeaponConfig if assigned.")]
        [SerializeField] private float damage = 20f;

        [Header("AI Hearing")]
        [Tooltip("Gunshot loudness for AI (0–1).")]
        [SerializeField, Range(0f, 1f)] private float gunshotSoundIntensity = 1f;
        [Tooltip("Gunshot hearing range for AI.")]
        [SerializeField, Min(0f)] private float gunshotSoundRange = 45f;

        [Header("Animation")]
        [Tooltip("Ejection port transform — where casings spawn.")]
        [SerializeField] private Transform socketEjection;

        [Header("Resources")]
        [SerializeField] private GameObject prefabCasing;
        [SerializeField] private GameObject prefabProjectile;
        [Tooltip("AnimatorController used by the player character when wielding this weapon.")]
        [SerializeField] public RuntimeAnimatorController controller;
        [SerializeField] private Sprite spriteBody;

        [Header("Audio Clips Holster")]
        [SerializeField] private AudioClip audioClipHolster;
        [SerializeField] private AudioClip audioClipUnholster;

        [Header("Audio Clips Reloads")]
        [SerializeField] private AudioClip audioClipReload;
        [SerializeField] private AudioClip audioClipReloadEmpty;

        [Header("Audio Clips Other")]
        [SerializeField] private AudioClip audioClipFireEmpty;

        #endregion

        #region FIELDS

        private Animator                         animator;
        private WeaponAttachmentManagerBehaviour attachmentManager;
        private int                              ammunitionCurrent;

        private MagazineBehaviour magazineBehaviour;
        private MuzzleBehaviour   muzzleBehaviour;

        private IGameModeService   gameModeService;
        private CharacterBehaviour characterBehaviour;
        private Transform          playerCamera;
        private SoundEmitter       soundEmitter;

        #endregion

        #region UNITY

        protected override void Awake()
        {
            animator          = GetComponent<Animator>();
            attachmentManager = GetComponent<WeaponAttachmentManagerBehaviour>();
            soundEmitter      = GetComponent<SoundEmitter>();

            gameModeService    = ServiceLocator.Current.Get<IGameModeService>();
            characterBehaviour = gameModeService.GetPlayerCharacter();
            playerCamera       = characterBehaviour.GetCameraWorld().transform;
        }

        protected override void Start()
        {
            if (weaponConfig != null)
            {
                damage               = weaponConfig.baseDamage;
                maximumDistance      = weaponConfig.maxDistance;
                gunshotSoundIntensity = weaponConfig.gunshotSoundIntensity;
                gunshotSoundRange    = weaponConfig.gunshotSoundRange;

                Debug.Log($"[Weapon] {name} using WeaponConfig '{weaponConfig.displayName}' " +
                          $"(damage={damage}, maxDist={maximumDistance}, " +
                          $"soundIntensity={gunshotSoundIntensity}, soundRange={gunshotSoundRange})");
            }

            magazineBehaviour = attachmentManager.GetEquippedMagazine();
            muzzleBehaviour   = attachmentManager.GetEquippedMuzzle();

            if (magazineBehaviour != null)
                ammunitionCurrent = magazineBehaviour.GetAmmunitionTotal();
            else
            {
                ammunitionCurrent = int.MaxValue;
                Debug.LogWarning($"{name}: No magazineBehaviour found. Treating as infinite ammo.");
            }
        }

        #endregion

        #region GETTERS

        public override Animator  GetAnimator()    => animator;
        public override Sprite    GetSpriteBody()  => spriteBody;

        public override AudioClip GetAudioClipHolster()      => audioClipHolster;
        public override AudioClip GetAudioClipUnholster()    => audioClipUnholster;
        public override AudioClip GetAudioClipReload()       => audioClipReload;
        public override AudioClip GetAudioClipReloadEmpty()  => audioClipReloadEmpty;
        public override AudioClip GetAudioClipFireEmpty()    => audioClipFireEmpty;
        public override AudioClip GetAudioClipFire()         => muzzleBehaviour.GetAudioClipFire();

        public override int   GetAmmunitionCurrent() => ammunitionCurrent;
        public override int   GetAmmunitionTotal()   => magazineBehaviour != null
                                                            ? magazineBehaviour.GetAmmunitionTotal()
                                                            : int.MaxValue;
        public override float GetRateOfFire()        => roundsPerMinutes;
        public override bool  IsAutomatic()          => automatic;
        public override bool  IsFull()               => ammunitionCurrent == magazineBehaviour.GetAmmunitionTotal();
        public override bool  HasAmmunition()        => ammunitionCurrent > 0;

        public override RuntimeAnimatorController        GetAnimatorController()  => controller;
        public override WeaponAttachmentManagerBehaviour GetAttachmentManager()   => attachmentManager;

        #endregion

        #region METHODS

        public override void Reload()
        {
            if (animator == null) return;
            animator.Play(HasAmmunition() ? "Reload" : "Reload Empty", 0, 0.0f);
        }

        public override void CancelReload()
        {
            if (animator == null) return;
            animator.CrossFade("Idle", 0.1f, 0, 0);
        }

        public override void Fire(float spreadMultiplier = 1.0f)
        {
            if (muzzleBehaviour == null || playerCamera == null) return;
            if (!HasAmmunition()) return;

            Transform muzzleSocket = muzzleBehaviour.GetSocket();
            animator.Play("Fire", 0, 0.0f);

            ammunitionCurrent = Mathf.Clamp(
                ammunitionCurrent - 1,
                0,
                magazineBehaviour != null ? magazineBehaviour.GetAmmunitionTotal() : ammunitionCurrent);

            muzzleBehaviour.Effect();

            if (soundEmitter != null && weaponConfig != null)
                soundEmitter.EmitGunshot(weaponConfig.gunshotSoundIntensity, weaponConfig.gunshotSoundRange);

            Quaternion rotation = Quaternion.LookRotation(
                playerCamera.forward * 1000.0f - muzzleSocket.position);

            if (Physics.Raycast(new Ray(playerCamera.position, playerCamera.forward),
                                out RaycastHit hit, maximumDistance, mask))
            {
                rotation = Quaternion.LookRotation(hit.point - muzzleSocket.position);
                ApplyDamage(hit);
            }

            GameObject projectile = Instantiate(prefabProjectile, muzzleSocket.position, rotation);
            projectile.GetComponent<Rigidbody>().linearVelocity =
                projectile.transform.forward * projectileImpulse;
        }

        private void ApplyDamage(RaycastHit hit)
        {
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                // Skip damage on already-dead enemies — projectile physics handles
                // moving the ragdoll, no need to route through the damage pipeline.
                if (damageable.IsDead) return;

                damageable.TakeDamage(damage);

                CombatAI enemyAI = hit.collider.GetComponentInParent<CombatAI>();
                if (enemyAI != null)
                    enemyAI.OnTakeDamage(damage, transform.position, transform);
            }
        }

        public override void FillAmmunition(int amount)
        {
            if (magazineBehaviour == null) return;
            ammunitionCurrent = amount != 0
                ? Mathf.Clamp(ammunitionCurrent + amount, 0, GetAmmunitionTotal())
                : magazineBehaviour.GetAmmunitionTotal();
        }

        public override void EjectCasing()
        {
            if (prefabCasing != null && socketEjection != null)
                Instantiate(prefabCasing, socketEjection.position, socketEjection.rotation);
        }

        #endregion
    }
}