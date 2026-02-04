using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace Game.LowPolyShooterPack
{
    public sealed class Character : CharacterBehaviour
    {
        // Serialized fields
        [Header("Inventory")][SerializeField] private InventoryBehaviour inventory;
        [Header("Cameras")][SerializeField] private Camera cameraWorld;
        [Header("Animation")]
        [SerializeField] private float dampTimeLocomotion = 0.15f;
        [SerializeField] private float dampTimeAiming = 0.3f;
        [Header("Animation Procedural")][SerializeField] private Animator characterAnimator;

        // Private fields
        private bool aiming, running, holstered;
        private float lastShotTime;
        private int layerOverlay, layerHolster, layerActions;
        private CharacterKinematics characterKinematics;
        private PlayerInput playerInput;
        private WeaponBehaviour equippedWeapon;
        private WeaponAttachmentManagerBehaviour weaponAttachmentManager;
        private ScopeBehaviour equippedWeaponScope;
        private MagazineBehaviour equippedWeaponMagazine;
        private bool reloading, inspecting, holstering;
        private Vector2 axisLook, axisMovement;
        private bool holdingButtonAim, holdingButtonRun, holdingButtonFire;
        private bool tutorialTextVisible;
        private bool cursorLocked;

        // Unity Methods
        protected override void Awake()
        {
            cursorLocked = true;
            UpdateCursorState();
            characterKinematics = GetComponent<CharacterKinematics>();
            playerInput = GetComponent<PlayerInput>();
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
            aiming = holdingButtonAim && CanAim();
            running = holdingButtonRun && CanRun();

            if (holdingButtonFire)
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
            if (equippedWeapon == null || equippedWeaponScope == null)
                return;
            if (characterKinematics != null)
                characterKinematics.Compute();
        }

        #region GETTERS

        public override Camera GetCameraWorld() => cameraWorld;
        public override InventoryBehaviour GetInventory() => inventory;
        public override bool IsCrosshairVisible() => !aiming && !holstered;
        public override bool IsRunning() => running;
        public override bool IsAiming() => aiming;
        public override bool IsCursorLocked() => cursorLocked;
        public override bool IsTutorialTextVisible() => tutorialTextVisible;
        public override Vector2 GetInputMovement() => axisMovement;
        public override Vector2 GetInputLook() => axisLook;

        // Implement jump input using the new Input System:
        public override bool GetInputJump()
        {
            bool jumpTriggered = playerInput !=  null && playerInput.actions["Jump"].triggered;
            //Debug.Log("Jump triggered: " + jumpTriggered);
            return jumpTriggered;
        }

        

        #endregion

        #region METHODS

        private void UpdateAnimator()
        {
            characterAnimator.SetFloat(Animator.StringToHash("Movement"),
                Mathf.Clamp01(Mathf.Abs(axisMovement.x) + Mathf.Abs(axisMovement.y)), dampTimeLocomotion, Time.deltaTime);
            characterAnimator.SetFloat(Animator.StringToHash("Aiming"), Convert.ToSingle(aiming), dampTimeAiming * 0.25f, Time.deltaTime);
            characterAnimator.SetBool("Aim", aiming);
            characterAnimator.SetBool("Running", running);
        }

        private void RefreshWeaponSetup()
        {
            if ((equippedWeapon = inventory.GetEquipped()) == null) return;
            characterAnimator.runtimeAnimatorController = equippedWeapon.GetAnimatorController();
            weaponAttachmentManager = equippedWeapon.GetAttachmentManager();
            if (weaponAttachmentManager == null) return;
            equippedWeaponScope = weaponAttachmentManager.GetEquippedScope();
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
            Cursor.visible = !cursorLocked;
            Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        }

        private void SetHolstered(bool value = true)
        {
            holstered = value;
            characterAnimator.SetBool("Holstered", holstered);
        }

        // Action checks (condensed)
        private bool CanPlayAnimationFire() => !(holstered || holstering || reloading || inspecting);
        private bool CanPlayAnimationReload() => !reloading && !inspecting;
        private bool CanPlayAnimationHolster() => !reloading && !inspecting;
        private bool CanChangeWeapon() => !(holstering || reloading || inspecting);
        private bool CanPlayAnimationInspect() => !(holstered || holstering || reloading || inspecting);
        private bool CanAim() => !(holstered || inspecting || reloading || holstering);
        // private bool CanRun() => !(inspecting || reloading || aiming || (holdingButtonFire && equippedWeapon.HasAmmunition()))
        //     && (axisMovement.y > 0 && Math.Abs(Mathf.Abs(axisMovement.x) - 1) >= 0.01f); //only forward sprinting
        private bool CanRun() => 
            !(inspecting || reloading || aiming || (holdingButtonFire && equippedWeapon.HasAmmunition()))
            && axisMovement.sqrMagnitude > 0.01f;

        #endregion

        #region INPUT METHODS

        public void OnTryFire(InputAction.CallbackContext context)
        {
            if (!cursorLocked) return;
            switch (context.phase)
            {
                case InputActionPhase.Started:
                    holdingButtonFire = true;
                    break;
                case InputActionPhase.Performed:
                    if (!CanPlayAnimationFire()) break;
                    if (equippedWeapon.HasAmmunition())
                    {
                        if (!equippedWeapon.IsAutomatic() && Time.time - lastShotTime > 60.0f / equippedWeapon.GetRateOfFire())
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
            if (context.phase == InputActionPhase.Started)
                holdingButtonAim = true;
            else if (context.phase == InputActionPhase.Canceled)
                holdingButtonAim = false;
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
            if (context.phase == InputActionPhase.Started)
                holdingButtonRun = true;
            else if (context.phase == InputActionPhase.Canceled)
                holdingButtonRun = false;
        }

        public void OnTryInventoryNext(InputAction.CallbackContext context)
        {
            if (!cursorLocked || inventory == null) return;
            if (context.phase == InputActionPhase.Performed)
            {
                float scrollValue = context.valueType.IsEquivalentTo(typeof(Vector2))
                    ? Mathf.Sign(context.ReadValue<Vector2>().y)
                    : 1.0f;
                int indexNext = scrollValue > 0 ? inventory.GetNextIndex() : inventory.GetLastIndex();
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

        public void OnMove(InputAction.CallbackContext context) => axisMovement = cursorLocked ? context.ReadValue<Vector2>() : default;
        public void OnLook(InputAction.CallbackContext context) => axisLook = cursorLocked ? context.ReadValue<Vector2>() : default;
        public void OnUpdateTutorial(InputAction.CallbackContext context)
        {
            tutorialTextVisible = context switch
            {
                { phase: InputActionPhase.Started } => true,
                { phase: InputActionPhase.Canceled } => false,
                _ => tutorialTextVisible
            };
        }

        #endregion

        #region ANIMATION EVENTS

        public override void EjectCasing() => equippedWeapon?.EjectCasing();
        public override void FillAmmunition(int amount) => equippedWeapon?.FillAmmunition(amount);
        public override void SetActiveMagazine(int active) => equippedWeaponMagazine.gameObject.SetActive(active != 0);
        public override void AnimationEndedReload() => reloading = false;
        public override void AnimationEndedInspect() => inspecting = false;
        public override void AnimationEndedHolster() => holstering = false;

        #endregion

        private IEnumerator Equip(int index = 0)
        {
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

        private void PlayReloadAnimation()
        {
            string stateName = equippedWeapon.HasAmmunition() ? "Reload" : "Reload Empty";
            characterAnimator.Play(stateName, layerActions, 0.0f);
            reloading = true;
            equippedWeapon.Reload();
        }

        private void Inspect()
        {
            inspecting = true;
            characterAnimator.CrossFade("Inspect", 0.0f, layerActions, 0);
        }
    }
}
