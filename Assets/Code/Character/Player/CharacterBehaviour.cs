// using UnityEngine;

// namespace MyGame
// {
//     public abstract class CharacterBehaviour : MonoBehaviour
//     {
//         #region UNITY
//         protected virtual void Awake() { }
//         protected virtual void Start() { }
//         protected virtual void Update() { }
//         protected virtual void LateUpdate() { }
//         #endregion

//         #region GETTERS
//         public abstract Camera GetCameraWorld();
//         public abstract InventoryBehaviour GetInventory();
//         public abstract bool IsCrosshairVisible();
//         public abstract bool IsRunning();
//         public abstract bool IsAiming();
//         public abstract bool IsCursorLocked();
//         public abstract bool IsTutorialTextVisible();
//         public abstract Vector2 GetInputMovement();
//         public abstract Vector2 GetInputLook();
//         // Added for jump input.
//         public abstract bool GetInputJump();
//         #endregion

//         #region ANIMATION
//         public abstract void EjectCasing();
//         public abstract void FillAmmunition(int amount);
//         public abstract void SetActiveMagazine(int active);
//         public abstract void AnimationEndedReload();
//         public abstract void AnimationEndedInspect();
//         public abstract void AnimationEndedHolster();
//         #endregion
//     }
// }

using UnityEngine;

namespace MyGame
{
    public abstract class CharacterBehaviour : MonoBehaviour
    {
        #region UNITY
        protected virtual void Awake() { }
        protected virtual void Start() { }
        protected virtual void Update() { }
        protected virtual void LateUpdate() { }
        #endregion

        #region GETTERS
        public abstract Camera GetCameraWorld();
        public abstract InventoryBehaviour GetInventory();
        public abstract bool IsCrosshairVisible();
        public abstract bool IsRunning();
        public abstract bool IsAiming();
        public abstract bool IsCursorLocked();
        public abstract bool IsTutorialTextVisible();
        public abstract Vector2 GetInputMovement();
        public abstract Vector2 GetInputLook();
        public abstract bool GetInputJump();

        /// Returns true if a reload is currently in progress.
        public abstract bool IsReloading();

        /// Returns true if ammo has already been committed during the current reload.
        /// Useful for abilities/melee to decide whether to roll back ammo on cancel.
        public abstract bool IsReloadAmmoFilled();
        #endregion

        #region ACTIONS
        /// Cancel an in-progress reload. Safe to call at any time — no-ops if not reloading.
        /// Callers: melee system, dodge/ability system, weapon swap.
        public abstract void CancelReload();
        #endregion

        #region ANIMATION EVENTS
        public abstract void EjectCasing();
        public abstract void FillAmmunition(int amount);
        public abstract void SetActiveMagazine(int active);
        public abstract void AnimationEndedReload();
        public abstract void AnimationEndedInspect();
        public abstract void AnimationEndedHolster();
        #endregion
    }
}