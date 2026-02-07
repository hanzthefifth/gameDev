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
        // Added for jump input.
        public abstract bool GetInputJump();
        #endregion

        #region ANIMATION
        public abstract void EjectCasing();
        public abstract void FillAmmunition(int amount);
        public abstract void SetActiveMagazine(int active);
        public abstract void AnimationEndedReload();
        public abstract void AnimationEndedInspect();
        public abstract void AnimationEndedHolster();
        #endregion
    }
}
