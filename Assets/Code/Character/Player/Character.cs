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

        [Header("Melee (No Animation Fallback)")]
        [Tooltip("If the character animator has no Melee Attack state, the hit fires after " +
                 "this delay and the swing ends after attackCooldown. Set to match your " +
                 "eventual impact frame time so it feels right. Remove once animations exist.")]
        [SerializeField] private float meleeHitFallbackDelay = 0.15f;

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
        private bool        reloading;
        private bool        reloadAmmoFilled;
        private ReloadPhase reloadPhase = ReloadPhase.None;

        // ── Melee state ──────────────────────────────────────────────────────────────
        private bool  meleeing;
        private float meleeFireLockoutUntil;
        private MeleeWeaponBehaviour equippedMeleeWeapon;
        private bool  hasMeleeAnimatorState; // detected once on weapon equip

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
            playerHealth        = GetComponent<PlayerHealth>();
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
            if (playerHealth != null && playerHealth.IsDead)
            {
                holdingButtonFire = false;
                holdingButtonAim  = false;
                holdingButtonRun  = false;
                axisMovement      = default;
                axisLook          = default;
                return;
            }

            aiming  = holdingButtonAim && CanAim();
            running = holdingButtonRun && CanRun();

            // Auto-fire only for ranged weapons
            if (holdingButtonFire && equippedMeleeWeapon == null)
            {
                if (CanPlayAnimationFire() && equippedWeapon.HasAmmunition() && equippedWeapon.IsAutomatic())
                {
                    if (Time.time - lastShotTime > 60.0f / equippedWeapon.GetRateOfFire())
                        Fire();
                }
            }

            if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null)
            {
                UpdateAnimator();
            }
        }

        protected override void LateUpdate()
        {
            if (equippedWeapon == null || equippedWeaponScope == null) return;
            if (characterKinematics != null)
                characterKinematics.Compute();
        }

        // ── Getters ──────────────────────────────────────────────────────────────────
        public override Camera             GetCameraWorld()        => cameraWorld;
        public override InventoryBehaviour GetInventory()          => inventory;
        public override bool               IsCrosshairVisible()    => !aiming && !holstered;
        public override bool               IsRunning()             => running;
        public override bool               IsAiming()             => aiming;
        public override bool               IsCursorLocked()       => cursorLocked;
        public override bool               IsTutorialTextVisible() => tutorialTextVisible;
        public override Vector2            GetInputMovement()      => axisMovement;
        public override Vector2            GetInputLook()          => axisLook;
        public override bool               IsReloading()           => reloading;
        public override bool               IsReloadAmmoFilled()    => reloadAmmoFilled;
        public override bool               IsMeleeAttacking()      => meleeing;

        public override bool GetInputJump()
            => playerInput != null && playerInput.actions["Jump"].triggered;

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
            //characterAnimator.runtimeAnimatorController = equippedWeapon.GetAnimatorController();
            // Only replace controller if weapon actually provides one.
            RuntimeAnimatorController controller = equippedWeapon.GetAnimatorController();
            if (controller != null)
            {
                characterAnimator.runtimeAnimatorController = controller;
            }

            equippedMeleeWeapon = equippedWeapon as MeleeWeaponBehaviour;

            // Detect whether the current animator controller has a "Melee Attack" state.
            // If it doesn't we fall back to a timed coroutine so testing works without
            // any animator setup at all.
            hasMeleeAnimatorState = equippedMeleeWeapon != null && HasAnimatorState("Melee Attack", layerActions);

            weaponAttachmentManager = equippedWeapon.GetAttachmentManager();
            if (weaponAttachmentManager == null) return;
            equippedWeaponScope    = weaponAttachmentManager.GetEquippedScope();
            equippedWeaponMagazine = weaponAttachmentManager.GetEquippedMagazine();
        }

        /// Returns true if the current animator controller has a state with the given
        /// name on the given layer. Safe to call with any layer index including -1.
        private bool HasAnimatorState(string stateName, int layer)
        {
            if (characterAnimator == null) return false;
            if (layer < 0) return false;

            // AnimatorController stores states in its layers. We probe by trying to
            // read the state hash — if it transitions without error the state exists.
            // The safest runtime check is IsName on the current state info, but that
            // only works while playing the state. Instead we use the RuntimeAnimatorController
            // layers via the UnityEditor API (editor only), so at runtime we do a
            // lightweight probe: attempt a CrossFade to a dummy normalizedTime and catch
            // the result via a short HasState workaround.
            //
            // Unity doesn't expose HasState at runtime, so we check the animator's
            // parameter/state count indirectly: try StringToHash and assume valid if
            // the controller was assigned from a MeleeWeaponConfig (which means the
            // designer intentionally set it up). If no controller is assigned we know
            // there's no state.
            if (characterAnimator.runtimeAnimatorController == null) return false;

            // Best available runtime check: does the animator have any clips at all?
            // We rely on the fact that a properly set up melee controller will have
            // a clip named "Melee Attack". Check AnimationClips on the controller.
            var clips = characterAnimator.runtimeAnimatorController.animationClips;
            if (clips == null || clips.Length == 0) return false;

            foreach (var clip in clips)
            {
                if (clip != null && clip.name == stateName)
                    return true;
            }

            return false;
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
            !(inspecting || reloading || aiming || (holdingButtonFire && equippedWeapon != null && equippedWeapon.HasAmmunition()))
            && axisMovement.sqrMagnitude > 0.01f;

        // ── Reload ───────────────────────────────────────────────────────────────────
        private void PlayReloadAnimation()
        {
            reloading        = true;
            reloadAmmoFilled = false;
            reloadPhase      = ReloadPhase.Start;
            equippedWeapon.Reload();

            string startState = equippedWeapon.HasAmmunition() ? "Reload" : "Reload Empty";
            characterAnimator.Play(startState, layerActions, 0.0f);
        }

        public void OnReloadPhaseStartEnded()
        {
            if (!reloading) return;
            reloadPhase = ReloadPhase.End;
            string endState = equippedWeapon.HasAmmunition() ? "Reload End" : "Reload Empty End";
            characterAnimator.Play(endState, layerActions, 0.0f);
        }

        public override void CancelReload()
        {
            if (!reloading) return;

            reloading        = false;
            reloadPhase      = ReloadPhase.None;
            reloadAmmoFilled = false;

            characterAnimator.CrossFade("Idle", 0.15f, layerActions, 0);
            equippedWeapon?.CancelReload();
        }

        private void Inspect()
        {
            inspecting = true;
            characterAnimator.CrossFade("Inspect", 0.0f, layerActions, 0);
        }

        // ── Melee ────────────────────────────────────────────────────────────────────
        private void PlayMeleeAnimation()
        {
            if (equippedMeleeWeapon == null) return;
            if (!equippedMeleeWeapon.TryAttack()) return;

            meleeing = true;
            if (reloading) CancelReload();

            if (hasMeleeAnimatorState)
            {
                // Full path — animation events drive OnMeleeHit and AnimationEndedMelee.
                characterAnimator.CrossFade("Melee Attack", 0.05f, layerActions, 0);
            }
            else
            {
                // No animator state yet — fire hit after a short delay then end the swing.
                // This lets you test the full damage/ragdoll/hitstop pipeline without
                // any animator setup. Remove this branch (or keep it) once animations exist.
                StartCoroutine(MeleeFallbackRoutine());
            }
        }

        /// Fallback coroutine used when no "Melee Attack" animator state exists.
        /// Simulates the two animation events: OnMeleeHit and AnimationEndedMelee.
        private IEnumerator MeleeFallbackRoutine()
        {
            // Wait for the "impact frame" — equivalent to where OnMeleeHit would fire.
            yield return new WaitForSeconds(meleeHitFallbackDelay);
            OnMeleeHit();

            // Wait out the remaining swing time before ending.
            float remainingTime = (equippedMeleeWeapon != null ? equippedMeleeWeapon.PostMeleeLockout : 0.3f);
            yield return new WaitForSeconds(remainingTime);
            AnimationEndedMelee();
        }

        /// Animation event — fires at the impact frame of "Melee Attack".
        public override void OnMeleeHit()
        {
            equippedMeleeWeapon?.ExecuteHit(cameraWorld.transform);
        }

        /// Animation event — fires at the very last frame of "Melee Attack".
        public override void AnimationEndedMelee()
        {
            meleeing = false;

            if (equippedMeleeWeapon != null)
                meleeFireLockoutUntil = Time.time + equippedMeleeWeapon.PostMeleeLockout;

            // Only snap animator if we actually played a state.
            if (hasMeleeAnimatorState)
                characterAnimator.CrossFade("Idle", 0.15f, layerActions, 0);
        }

        private IEnumerator Equip(int index = 0)
        {
            if (reloading) CancelReload();

            // If we have a valid animator and holster layer exists,
            // use the animation flow. Otherwise skip waiting.
            bool canHolsterAnimate =
                characterAnimator != null &&
                characterAnimator.runtimeAnimatorController != null &&
                layerHolster >= 0;

            if (!holstered && canHolsterAnimate)
            {
                SetHolstered(holstering = true);

                // Timeout safety in case animation event never fires
                float timeout = 1.0f;
                float startTime = Time.time;

                yield return new WaitUntil(() =>
                    !holstering || Time.time - startTime > timeout);
            }

            // Ensure we never get stuck
            holstering = false;

            SetHolstered(false);

            if (canHolsterAnimate)
                characterAnimator.Play("Unholster", layerHolster, 0);

            inventory.Equip(index);
            RefreshWeaponSetup();
        }

        // ── Input handlers ───────────────────────────────────────────────────────────
        public void OnTryFire(InputAction.CallbackContext context)
        {
            if (!cursorLocked) return;

            // Melee weapon equipped — mouse 1 swings instead of firing.
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
        public override void EjectCasing() => equippedWeapon?.EjectCasing();

        public override void SetActiveMagazine(int active)
            => equippedWeaponMagazine.gameObject.SetActive(active != 0);

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

        public override void AnimationEndedInspect() => inspecting = false;
        public override void AnimationEndedHolster() => holstering = false;
    }
}