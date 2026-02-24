// using UnityEngine;
// using EnemyAI.Complete;

// namespace MyGame
// {
//     /// Weapon. This class handles most of the things that weapons need.
//     public class Weapon : WeaponBehaviour
//     {
//         #region FIELDS SERIALIZED
        

//         [Header("Config")]
//         [Tooltip("Optional data asset that defines damage, range, etc. for this weapon.")]
//         [SerializeField] public WeaponConfig weaponConfig;
//         [Header("Firing")]
//         [Tooltip("Is this weapon automatic? If yes, then holding down the firing button will continuously fire.")]
//         [SerializeField] private bool automatic;
//         [Tooltip("How fast the projectiles are.")]
//         [SerializeField] private float projectileImpulse = 400.0f;
//         [Tooltip("Amount of shots this weapon can shoot in a minute. It determines how fast the weapon shoots.")]
//         [SerializeField] private int roundsPerMinutes = 200;
//         [Tooltip("Mask of things recognized when firing.")]
//         [SerializeField] private LayerMask mask;
//         [Tooltip("Maximum distance at which this weapon can fire accurately. Shots beyond this distance will not use linetracing for accuracy.")]
//         [SerializeField] private float maximumDistance = 500.0f;
//         [Header("Damage")]
//         [Tooltip("Damage dealt when a shot hits a damageable target. If a WeaponConfig is assigned, this will be overridden by config.baseDamage at runtime.")]
//         [SerializeField] private float damage = 20f;
//         [Tooltip("Force applied to rigidbodies when a shot hits. If a WeaponConfig is assigned, this will be overridden by config.impactForce at runtime.")]
//         [SerializeField] private float impactForce = 30f;
//         [Header("AI Hearing")]
//         [Tooltip("How loud this weapon is to AI hearing (0-1). Pistols lower, shotguns higher.")]
//         [SerializeField, Range(0f, 1f)] private float gunshotSoundIntensity = 1f;
//         [Tooltip("How far this weapon's gunshot can be heard by AI.")]
//         [SerializeField, Min(0f)] private float gunshotSoundRange = 45f;
//         [Header("Animation")]
//         [Tooltip("Transform that represents the weapon's ejection port, meaning the part of the weapon that casings shoot from.")]
//         [SerializeField] private Transform socketEjection;
//         [Header("Resources")]
//         [Tooltip("Casing Prefab.")]
//         [SerializeField] private GameObject prefabCasing;
//         [Tooltip("Projectile Prefab. This is the prefab spawned when the weapon shoots.")]
//         [SerializeField] private GameObject prefabProjectile;
//         [Tooltip("The AnimatorController a player character needs to use while wielding this weapon.")]
//         [SerializeField] public RuntimeAnimatorController controller;
//         [Tooltip("Weapon Body Texture.")]
//         [SerializeField] private Sprite spriteBody;
//         [Header("Audio Clips Holster")]
//         [Tooltip("Holster Audio Clip.")]
//         [SerializeField] private AudioClip audioClipHolster;
//         [Tooltip("Unholster Audio Clip.")]
//         [SerializeField] private AudioClip audioClipUnholster;
//         [Header("Audio Clips Reloads")]
//         [Tooltip("Reload Audio Clip.")]
//         [SerializeField] private AudioClip audioClipReload;
//         [Tooltip("Reload Empty Audio Clip.")]
//         [SerializeField] private AudioClip audioClipReloadEmpty;
//         [Header("Audio Clips Other")]
//         [Tooltip("AudioClip played when this weapon is fired without any ammunition.")]
//         [SerializeField] private AudioClip audioClipFireEmpty;

//         #endregion

//         #region FIELDS

//         /// Weapon Animator.
//         private Animator animator;

//         private WeaponAttachmentManagerBehaviour attachmentManager;
//         private int ammunitionCurrent;

//         #region Attachment Behaviours
//         private MagazineBehaviour magazineBehaviour; /// Equipped Magazine Reference.
//         private MuzzleBehaviour muzzleBehaviour;      /// Equipped Muzzle Reference.
//         #endregion

//         private IGameModeService gameModeService;
//         private CharacterBehaviour characterBehaviour;
//         private Transform playerCamera;
//         private SoundEmitter soundEmitter;

//         #endregion

//         #region UNITY

//         protected override void Awake()
//         {
//             animator = GetComponent<Animator>();
//             attachmentManager = GetComponent<WeaponAttachmentManagerBehaviour>();
//             soundEmitter = GetComponent<SoundEmitter>();

//             //Cache the MyGame mode service.
//             gameModeService = ServiceLocator.Current.Get<IGameModeService>();
//             //Cache the player character.
//             characterBehaviour = gameModeService.GetPlayerCharacter();
//             //Cache the world camera. We use this in line traces.
//             playerCamera = characterBehaviour.GetCameraWorld().transform;
//         }

//         #region Cache Attachment References
//         protected override void Start()
//         {
//             // Apply config values if present
//             if (weaponConfig != null)
//             {
//                 damage = weaponConfig.baseDamage;
//                 impactForce = weaponConfig.impactForce;
//                 maximumDistance = weaponConfig.maxDistance;
//                 gunshotSoundIntensity = weaponConfig.gunshotSoundIntensity;
//                 gunshotSoundRange = weaponConfig.gunshotSoundRange;

//                 // Optional debugging
//                 Debug.Log($"[Weapon] {name} using WeaponConfig '{weaponConfig.displayName}' " +
//                           $"(damage={damage}, force={impactForce}, maxDistance={maximumDistance}, soundIntensity={gunshotSoundIntensity}, soundRange={gunshotSoundRange})");
//             }

//             // Get attachments.
//             magazineBehaviour = attachmentManager.GetEquippedMagazine();
//             muzzleBehaviour   = attachmentManager.GetEquippedMuzzle();

//             // If we have a magazine, initialize from it. Otherwise infinite or 0.
//             if (magazineBehaviour != null)
//             {
//                 ammunitionCurrent = magazineBehaviour.GetAmmunitionTotal();
//             }
//             else
//             {
//                 ammunitionCurrent = int.MaxValue;
//                 Debug.LogWarning($"{name}: No magazineBehaviour found. Treating as infinite ammo.");
//             }
//         }
//         #endregion

//         #endregion

//         #region GETTERS

//         public override Animator GetAnimator() => animator;

//         public override Sprite GetSpriteBody() => spriteBody;

//         public override AudioClip GetAudioClipHolster() => audioClipHolster;
//         public override AudioClip GetAudioClipUnholster() => audioClipUnholster;
//         public override AudioClip GetAudioClipReload() => audioClipReload;
//         public override AudioClip GetAudioClipReloadEmpty() => audioClipReloadEmpty;
//         public override AudioClip GetAudioClipFireEmpty() => audioClipFireEmpty;
//         public override AudioClip GetAudioClipFire() => muzzleBehaviour.GetAudioClipFire();

//         public override int GetAmmunitionCurrent() => ammunitionCurrent;

//         public override int GetAmmunitionTotal()
//         {
//             if (magazineBehaviour == null)
//                 return int.MaxValue;

//             return magazineBehaviour.GetAmmunitionTotal();
//         }

//         public override float GetRateOfFire() => roundsPerMinutes;
//         public override bool IsAutomatic() => automatic;
//         public override bool IsFull() => ammunitionCurrent == magazineBehaviour.GetAmmunitionTotal();
//         public override bool HasAmmunition() => ammunitionCurrent > 0;

//         public override RuntimeAnimatorController GetAnimatorController() => controller;
//         public override WeaponAttachmentManagerBehaviour GetAttachmentManager() => attachmentManager;

//         #endregion

//         #region METHODS

//         // public override void Reload()
//         // {
//         //     if (animator == null)
//         //         return;

//         //     // Weapon animator handles mechanical mag movement
//         //     animator.SetBool("reloading", true);
//         // }
//         public override void Reload() { animator.Play(HasAmmunition() ? "Reload" : "Reload Empty", 0, 0.0f); }


//         public override void Fire(float spreadMultiplier = 1.0f)
//         {
//             if (muzzleBehaviour == null || playerCamera == null)
//                 return;
//             if(!HasAmmunition())
//                 return;

//             Transform muzzleSocket = muzzleBehaviour.GetSocket();

//             const string stateName = "Fire";
//             animator.Play(stateName, 0, 0.0f);

//             ammunitionCurrent = Mathf.Clamp(
//                 ammunitionCurrent - 1,
//                 0,
//                 magazineBehaviour != null ? magazineBehaviour.GetAmmunitionTotal() : ammunitionCurrent
//             );
//             muzzleBehaviour.Effect();
            
//             // FIX: Pass actual config values to the emitter
//             if (soundEmitter != null && weaponConfig != null)
//             {
//                 // If weaponConfig.soundIntensity is 0, the AI will now ignore it
//                 soundEmitter.EmitGunshot(weaponConfig.gunshotSoundIntensity, weaponConfig.gunshotSoundRange);
//             }

//             Quaternion rotation =
//                 Quaternion.LookRotation(playerCamera.forward * 1000.0f - muzzleSocket.position);

//             if (Physics.Raycast(new Ray(playerCamera.position, playerCamera.forward),
//                                 out RaycastHit hit, maximumDistance, mask))
//             {
//                 rotation = Quaternion.LookRotation(hit.point - muzzleSocket.position);
//                 ApplyDamage(hit);
//             }

//             GameObject projectile =
//                 Instantiate(prefabProjectile, muzzleSocket.position, rotation);

//             projectile.GetComponent<Rigidbody>().linearVelocity =
//                 projectile.transform.forward * projectileImpulse;
//         }


//         private void ApplyDamage(RaycastHit hit)
//         {
//             IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
//             if (damageable != null)
//             {
//                 damageable.TakeDamage(damage);
//                 CombatAI enemyAI = hit.collider.GetComponentInParent<CombatAI>();
//                 Debug.Log($"3. Found CombatAI: {enemyAI != null}");
            
//                 if (enemyAI != null)
//                 {
//                     enemyAI.OnTakeDamage(damage, transform.position, transform);
//                     Debug.Log("4. Called OnTakeDamage!");
//                 }

//                 // Later you can route type too if you extend IDamageable:
//                 // damageable.TakeDamage(damage, weaponConfig?.damageType ?? DamageType.Bullet);
//             }

//             Rigidbody hitBody = hit.rigidbody;
//             if (hitBody != null)
//             {
//                 hitBody.AddForceAtPosition(
//                     playerCamera.forward * impactForce,
//                     hit.point,
//                     ForceMode.Impulse);
//             }
//         }

//         public override void FillAmmunition(int amount)
//         {
//             if (magazineBehaviour == null)
//                 return;

//             ammunitionCurrent = amount != 0
//                 ? Mathf.Clamp(ammunitionCurrent + amount, 0, GetAmmunitionTotal())
//                 : magazineBehaviour.GetAmmunitionTotal();
//         }

//         public override void EjectCasing()
//         {
//             if (prefabCasing != null && socketEjection != null)
//                 Instantiate(prefabCasing, socketEjection.position, socketEjection.rotation);
//         }

//         #endregion
//     }
// }
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
        [Tooltip("Impact force on rigidbodies. Overridden by WeaponConfig if assigned.")]
        [SerializeField] private float impactForce = 30f;

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
                impactForce          = weaponConfig.impactForce;
                maximumDistance      = weaponConfig.maxDistance;
                gunshotSoundIntensity = weaponConfig.gunshotSoundIntensity;
                gunshotSoundRange    = weaponConfig.gunshotSoundRange;

                Debug.Log($"[Weapon] {name} using WeaponConfig '{weaponConfig.displayName}' " +
                          $"(damage={damage}, force={impactForce}, maxDist={maximumDistance}, " +
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
            // Play the weapon-side reload animation. For staged reloads the weapon
            // animator can also split into "Reload Start" / "Reload End" to match the
            // character animator, but a single clip is fine if the mechanics look right.
            animator.Play(HasAmmunition() ? "Reload" : "Reload Empty", 0, 0.0f);
        }

        /// Snap the weapon animator back to idle. Called by Character.CancelReload().
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
                damageable.TakeDamage(damage);

                CombatAI enemyAI = hit.collider.GetComponentInParent<CombatAI>();
                Debug.Log($"3. Found CombatAI: {enemyAI != null}");
                if (enemyAI != null)
                {
                    enemyAI.OnTakeDamage(damage, transform.position, transform);
                    Debug.Log("4. Called OnTakeDamage!");
                }
            }

            if (hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(
                    playerCamera.forward * impactForce, hit.point, ForceMode.Impulse);
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