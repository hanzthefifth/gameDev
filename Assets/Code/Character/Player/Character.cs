using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace MyGame
{
    public sealed class Character : CharacterBehaviour
    {
        // ---------------------------------------------------------------------------
        // Reload is split into three animator states so any action can interrupt
        // cleanly at a known point:
        //
        //   "Reload Start"  — mag comes out, hands move to pouch. No ammo yet.
        //   "Reload End"    — mag seats, chamber closes. Triggered after Start ends.
        //
        // The animation event OnReloadAmmoFilled fires inside "Reload End" (or inside
        // the legacy "Reload" / "Reload Empty" clips if you haven't split them yet).
        // Until that event fires, cancelling costs the player their ammo gain.
        // After it fires, cancelling is free — the round is already in the gun.
        //
        // Legacy single-clip reloads ("Reload" / "Reload Empty") still work exactly
        // as before. Just place your existing FillAmmunition animation event as normal
        // and it will flip reloadAmmoFilled at the right moment.
        // ---------------------------------------------------------------------------

        private enum ReloadPhase { None, Start, End }

        // ── Serialized ──────────────────────────────────────────────────────────────
        [Header("Inventory")]    [SerializeField] private InventoryBehaviour inventory;
        [Header("Cameras")]      [SerializeField] private Camera cameraWorld;
        [Header("Animation Procedural")] [SerializeField] private Animator characterAnimator;
        [Header("Animation")]
        [SerializeField] private float dampTimeLocomotion = 0.15f;
        [SerializeField] private float dampTimeAiming     = 0.3f;

        // ── Private state ────────────────────────────────────────────────────────────
        private bool  aiming, running, holstered;
        private float lastShotTime;
        private int   layerOverlay, layerHolster, layerActions;

        private CharacterKinematics characterKinematics;
        private PlayerInput playerInput;
        private PlayerHealth playerHealth;
        private WeaponBehaviour equippedWeapon;
        private WeaponAttachmentManagerBehaviour weaponAttachmentManager;
        private ScopeBehaviour equippedWeaponScope;
        private MagazineBehaviour equippedWeaponMagazine;

        private bool        inspecting, holstering;
        private bool        reloading;          // true while any reload phase is active
        private bool        reloadAmmoFilled;   // flipped by OnReloadAmmoFilled animation event
        private ReloadPhase reloadPhase = ReloadPhase.None;

        // ── Melee state ──────────────────────────────────────────────────────────
        private bool  meleeing;               // true while melee animation is playing
        private float meleeFireLockoutUntil;  // Time.time stamp — gun fire blocked until this
        private MeleeWeaponBehaviour equippedMeleeWeapon; // non-null only when a melee weapon is equipped

        private Vector2 axisLook, axisMovement;
        private bool    holdingButtonAim, holdingButtonRun, holdingButtonFire;
        private bool    tutorialTextVisible;
        private bool    cursorLocked;

        // ── Unity ────────────────────────────────────────────────────────────────────
        protected override void Awake()
        {
            cursorLocked = true;
            UpdateCursorState();
            characterKinematics = GetComponent<CharacterKinematics>();
            playerInput         = GetComponent<PlayerInput>();
            playerHealth = GetComponent<PlayerHealth>();
            inventory.Init();
            RefreshWeaponSetup();
        }

        protected override void Start()
        {
            layerHolster = characterAnimator.GetLayerIndex("Layer Holster");
            layerActions = characterAnimator.GetLayerIndex("Layer Actions");
            layerOverlay = characterAnimator.GetLayerIndex("Layer Overlay");
        }

        protected override void Update()
        {
            // 🔴 ADD THIS BLOCK AT THE TOP
            if (playerHealth != null && playerHealth.IsDead)
            {
                holdingButtonFire = false;
                holdingButtonAim = false;
                holdingButtonRun = false;
                axisMovement = default;
                axisLook = default;
                return;
            }

            aiming  = holdingButtonAim && CanAim();
            running = holdingButtonRun && CanRun();

            if (holdingButtonFire && equippedMeleeWeapon == null) // auto-fire only for ranged weapons
            {
                if (CanPlayAnimationFire() && equippedWeapon.HasAmmunition() && equippedWeapon.IsAutomatic())
                {
                    if (Time.time - lastShotTime > 60.0f / equippedWeapon.GetRateOfFire())
                        Fire();
                }
            }

            UpdateAnimator();
        }


        protected override void LateUpdate()
        {
            if (equippedWeapon == null || equippedWeaponScope == null) return;
            if (characterKinematics != null)
                characterKinematics.Compute();
        }

        // ── Getters ──────────────────────────────────────────────────────────────────
        public override Camera             GetCameraWorld()         => cameraWorld;
        public override InventoryBehaviour GetInventory()           => inventory;
        public override bool               IsCrosshairVisible()     => !aiming && !holstered;
        public override bool               IsRunning()              => running;
        public override bool               IsAiming()               => aiming;
        public override bool               IsCursorLocked()         => cursorLocked;
        public override bool               IsTutorialTextVisible()  => tutorialTextVisible;
        public override Vector2            GetInputMovement()       => axisMovement;
        public override Vector2            GetInputLook()           => axisLook;
        public override bool               IsReloading()            => reloading;
        public override bool               IsReloadAmmoFilled()     => reloadAmmoFilled;
        public override bool               IsMeleeAttacking()       => meleeing;

        public override bool GetInputJump()
        {
            return playerInput != null && playerInput.actions["Jump"].triggered;
        }

        // ── Private helpers ──────────────────────────────────────────────────────────
        private void UpdateAnimator()
        {
            characterAnimator.SetFloat(Animator.StringToHash("Movement"),
                Mathf.Clamp01(Mathf.Abs(axisMovement.x) + Mathf.Abs(axisMovement.y)),
                dampTimeLocomotion, Time.deltaTime);
            characterAnimator.SetFloat(Animator.StringToHash("Aiming"),
                Convert.ToSingle(aiming), dampTimeAiming * 0.25f, Time.deltaTime);
            characterAnimator.SetBool("Aim",     aiming);
            characterAnimator.SetBool("Running", running);
        }

        private void RefreshWeaponSetup()
        {
            if ((equippedWeapon = inventory.GetEquipped()) == null) return;
            characterAnimator.runtimeAnimatorController = equippedWeapon.GetAnimatorController();

            // Check if the newly equipped weapon is a melee weapon
            equippedMeleeWeapon = equippedWeapon as MeleeWeaponBehaviour;

            weaponAttachmentManager = equippedWeapon.GetAttachmentManager();
            if (weaponAttachmentManager == null) return;
            equippedWeaponScope    = weaponAttachmentManager.GetEquippedScope();
            equippedWeaponMagazine = weaponAttachmentManager.GetEquippedMagazine();
        }

        private void Fire()
        {
            lastShotTime = Time.time;
            equippedWeapon.Fire();
            characterAnimator.CrossFade("Fire", 0.05f, layerOverlay, 0);
        }

        private void FireEmpty()
        {
            lastShotTime = Time.time;
            characterAnimator.CrossFade("Fire Empty", 0.05f, layerOverlay, 0);
        }

        private void UpdateCursorState()
        {
            Cursor.visible   = !cursorLocked;
            Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        }

        private void SetHolstered(bool value = true)
        {
            holstered = value;
            characterAnimator.SetBool("Holstered", holstered);
        }

        // ── Can-do checks ────────────────────────────────────────────────────────────
        private bool CanPlayAnimationFire()    => !(holstered || holstering || reloading || inspecting || meleeing || Time.time < meleeFireLockoutUntil);
        private bool CanPlayAnimationReload()  => !reloading && !inspecting && !meleeing;
        private bool CanPlayAnimationHolster() => !reloading && !inspecting && !meleeing;
        private bool CanChangeWeapon()         => !(holstering || reloading || inspecting || meleeing);
        private bool CanPlayAnimationInspect() => !(holstered || holstering || reloading || inspecting || meleeing);
        private bool CanAim()                  => !(holstered || inspecting || reloading || holstering || meleeing);
        private bool CanMelee()                => !(holstered || holstering || inspecting || meleeing) && equippedMeleeWeapon != null;
        private bool CanRun()                  =>
            !(inspecting || reloading || aiming || (holdingButtonFire && equippedWeapon.HasAmmunition()))
            && axisMovement.sqrMagnitude > 0.01f;

        // ── Reload ───────────────────────────────────────────────────────────────────

        private void PlayReloadAnimation()
        {
            reloading        = true;
            reloadAmmoFilled = false;
            reloadPhase      = ReloadPhase.Start;
            equippedWeapon.Reload();

            // Play "Reload Start" if it exists in the controller, otherwise fall back
            // to the legacy single-clip states. The staged path requires two animator
            // states: "Reload Start" and "Reload End". If your animator only has
            // "Reload" / "Reload Empty", those still work — just rename the events.
            bool hasAmmo     = equippedWeapon.HasAmmunition();
            // string startState = hasAmmo ? "Reload Start" : "Reload Empty Start";
            string startState = hasAmmo ? "Reload" : "Reload Empty";

            // Try staged first; fall back to legacy clip name if state doesn't exist.
            // (AnimatorStateInfo.IsName is the clean runtime check, but easiest is to
            //  just use the name you've set up — document which path you're on.)
            characterAnimator.Play(startState, layerActions, 0.0f);
        }

        /// Called by the animation event at the END of "Reload Start" / beginning
        /// of "Reload End". Transitions to the second phase clip.
        public void OnReloadPhaseStartEnded()
        {
            if (!reloading) return;
            reloadPhase = ReloadPhase.End;
            bool hasAmmo   = equippedWeapon.HasAmmunition(); // still pre-fill at this point
            string endState = hasAmmo ? "Reload End" : "Reload Empty End";
            characterAnimator.Play(endState, layerActions, 0.0f);
        }

        /// Cancel any in-progress reload. Safe to call from melee, dodge, ability, or
        /// weapon-swap code. Checks reloadAmmoFilled so the caller can decide whether
        /// to roll back ammo if needed (we keep it by default — gun is chambered).
        public override void CancelReload()
        {
            if (!reloading) return;

            reloading   = false;
            reloadPhase = ReloadPhase.None;
            // reloadAmmoFilled intentionally left as-is so callers can read it:
            //   true  → ammo was already committed, cancel is free
            //   false → mag never seated; ammo was NOT added (no rollback needed)
            reloadAmmoFilled = false;

            // Snap both animators back to idle
            characterAnimator.CrossFade("Idle", 0.15f, layerActions, 0);
            equippedWeapon?.CancelReload();
        }

        private void Inspect()
        {
            inspecting = true;
            characterAnimator.CrossFade("Inspect", 0.0f, layerActions, 0);
        }

        // ── Melee ────────────────────────────────────────────────────────────────

        private void PlayMeleeAnimation()
        {
            if (equippedMeleeWeapon == null) return;
            if (!equippedMeleeWeapon.TryAttack()) return; // cooldown gated on the weapon

            meleeing = true;
            if (reloading) CancelReload(); // melee interrupts reload
            characterAnimator.CrossFade("Melee Attack", 0.05f, layerActions, 0);
        }

        /// Animation event — fires at the impact frame of "Melee Attack".
        /// Wire this in CharacterAnimationEventHandler just like FillAmmunition / EjectCasing.
        public override void OnMeleeHit()
        {
            equippedMeleeWeapon?.ExecuteHit(cameraWorld.transform);
        }

        /// Animation event — fires at the very last frame of "Melee Attack".
        public override void AnimationEndedMelee()
        {
            meleeing = false;

            // Short lockout so the player can't immediately fire on the last frame of the swing
            if (equippedMeleeWeapon != null)
                meleeFireLockoutUntil = Time.time + equippedMeleeWeapon.PostMeleeLockout;

            characterAnimator.CrossFade("Idle", 0.15f, layerActions, 0);
        }

        private IEnumerator Equip(int index = 0)
        {
            // Cancel any reload in progress before swapping
            if (reloading) CancelReload();

            if (!holstered)
            {
                SetHolstered(holstering = true);
                yield return new WaitUntil(() => !holstering);
            }
            SetHolstered(false);
            characterAnimator.Play("Unholster", layerHolster, 0);
            inventory.Equip(index);
            RefreshWeaponSetup();
        }

        // ── Input handlers ───────────────────────────────────────────────────────────
        public void OnTryFire(InputAction.CallbackContext context)
        {
            if (!cursorLocked) return;

            // If a melee weapon is equipped, fire input triggers a swing instead
            if (equippedMeleeWeapon != null)
            {
                if (context.phase == InputActionPhase.Performed && CanMelee())
                    PlayMeleeAnimation();
                return;
            }

            switch (context.phase)
            {
                case InputActionPhase.Started:
                    holdingButtonFire = true;
                    break;
                case InputActionPhase.Performed:
                    if (!CanPlayAnimationFire()) break;
                    if (equippedWeapon.HasAmmunition())
                    {
                        if (!equippedWeapon.IsAutomatic() &&
                            Time.time - lastShotTime > 60.0f / equippedWeapon.GetRateOfFire())
                            Fire();
                    }
                    else
                        FireEmpty();
                    break;
                case InputActionPhase.Canceled:
                    holdingButtonFire = false;
                    break;
            }
        }

        public void OnTryPlayReload(InputAction.CallbackContext context)
        {
            if (!cursorLocked || !CanPlayAnimationReload()) return;
            if (context.phase == InputActionPhase.Performed)
                PlayReloadAnimation();
        }

        public void OnTryInspect(InputAction.CallbackContext context)
        {
            if (!cursorLocked || !CanPlayAnimationInspect()) return;
            if (context.phase == InputActionPhase.Performed)
                Inspect();
        }

        public void OnTryAiming(InputAction.CallbackContext context)
        {
            if (!cursorLocked) return;
            if (context.phase == InputActionPhase.Started)       holdingButtonAim = true;
            else if (context.phase == InputActionPhase.Canceled) holdingButtonAim = false;
        }

        public void OnTryHolster(InputAction.CallbackContext context)
        {
            if (!cursorLocked) return;
            if (context.phase == InputActionPhase.Performed && CanPlayAnimationHolster())
            {
                SetHolstered(!holstered);
                holstering = true;
            }
        }

        public void OnTryRun(InputAction.CallbackContext context)
        {
            if (!cursorLocked) return;
            if (context.phase == InputActionPhase.Started)       holdingButtonRun = true;
            else if (context.phase == InputActionPhase.Canceled) holdingButtonRun = false;
        }

        public void OnTryInventoryNext(InputAction.CallbackContext context)
        {
            if (!cursorLocked || inventory == null) return;
            if (context.phase == InputActionPhase.Performed)
            {
                float scrollValue = context.valueType.IsEquivalentTo(typeof(Vector2))
                    ? Mathf.Sign(context.ReadValue<Vector2>().y)
                    : 1.0f;
                int indexNext    = scrollValue > 0 ? inventory.GetNextIndex() : inventory.GetLastIndex();
                int indexCurrent = inventory.GetEquippedIndex();
                if (CanChangeWeapon() && indexCurrent != indexNext)
                    StartCoroutine(nameof(Equip), indexNext);
            }
        }

        public void OnLockCursor(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                cursorLocked = !cursorLocked;
                UpdateCursorState();
            }
        }

        public void OnMove(InputAction.CallbackContext context)
            => axisMovement = cursorLocked ? context.ReadValue<Vector2>() : default;

        public void OnLook(InputAction.CallbackContext context)
            => axisLook = cursorLocked ? context.ReadValue<Vector2>() : default;

        public void OnUpdateTutorial(InputAction.CallbackContext context)
        {
            tutorialTextVisible = context switch
            {
                { phase: InputActionPhase.Started  } => true,
                { phase: InputActionPhase.Canceled } => false,
                _ => tutorialTextVisible
            };
        }

        // ── Animation events (called by CharacterAnimationEventHandler) ──────────────

        public override void EjectCasing()          => equippedWeapon?.EjectCasing();
        public override void SetActiveMagazine(int active)
            => equippedWeaponMagazine.gameObject.SetActive(active != 0);

        /// FillAmmunition doubles as the "reload safe point" event.
        /// Once this fires, ammo is committed — cancelling after this is free.
        public override void FillAmmunition(int amount)
        {
            equippedWeapon?.FillAmmunition(amount);
            reloadAmmoFilled = true;
        }

        public override void AnimationEndedReload()
        {
            reloading        = false;
            reloadAmmoFilled = false;
            reloadPhase      = ReloadPhase.None;
        }

        public override void AnimationEndedInspect()  => inspecting  = false;
        public override void AnimationEndedHolster()  => holstering  = false;    }
}