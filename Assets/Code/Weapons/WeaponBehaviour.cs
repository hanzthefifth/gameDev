// // Copyright 2021, Infima Games. All Rights Reserved.

// using UnityEngine;

// namespace MyGame
// {
//     public abstract class WeaponBehaviour : MonoBehaviour
//     {
//         #region UNITY

//         protected virtual void Awake(){}
//         protected virtual void Start(){}
//         protected virtual void Update(){}
//         protected virtual void LateUpdate(){}

//         #endregion
//         #region GETTERS
//         /// Returns the sprite to use when displaying the weapon's body.
//         public abstract Sprite GetSpriteBody();

//         public abstract AudioClip GetAudioClipHolster();
//         public abstract AudioClip GetAudioClipUnholster();
//         public abstract AudioClip GetAudioClipReload();
//         public abstract AudioClip GetAudioClipReloadEmpty();
//         public abstract AudioClip GetAudioClipFireEmpty();
//         public abstract AudioClip GetAudioClipFire(); 
//         public abstract int GetAmmunitionCurrent();
//         public abstract int GetAmmunitionTotal();
//         public abstract Animator GetAnimator();
//         public abstract bool IsAutomatic();
//         public abstract bool HasAmmunition();
//         public abstract bool IsFull();
//         public abstract float GetRateOfFire();
//         /// Returns the RuntimeAnimationController the Character needs to use when this Weapon is equipped!
//         public abstract RuntimeAnimatorController GetAnimatorController();
//         /// Returns the weapon's attachment manager component.
//         public abstract WeaponAttachmentManagerBehaviour GetAttachmentManager();
        
//         #endregion

//         #region METHODS

//         /// <param name="spreadMultiplier">Value to multiply the weapon's spread by. Very helpful to account for aimed spread multipliers.</param>
//         public abstract void Fire(float spreadMultiplier = 1.0f);
//         public abstract void Reload();
//         /// Fills the character's equipped weapon's ammunition by a certain amount, or fully if set to -1.
//         public abstract void FillAmmunition(int amount);
//         /// Ejects a casing from the weapon. This is commonly called from animation events, but can be called from anywhere.
//         public abstract void EjectCasing();

//         #endregion
//     }
// }
using UnityEngine;

namespace MyGame
{
    public abstract class WeaponBehaviour : MonoBehaviour
    {
        #region UNITY
        protected virtual void Awake() { }
        protected virtual void Start() { }
        protected virtual void Update() { }
        protected virtual void LateUpdate() { }
        #endregion

        #region GETTERS
        public abstract Sprite GetSpriteBody();
        public abstract AudioClip GetAudioClipHolster();
        public abstract AudioClip GetAudioClipUnholster();
        public abstract AudioClip GetAudioClipReload();
        public abstract AudioClip GetAudioClipReloadEmpty();
        public abstract AudioClip GetAudioClipFireEmpty();
        public abstract AudioClip GetAudioClipFire();
        public abstract int GetAmmunitionCurrent();
        public abstract int GetAmmunitionTotal();
        public abstract Animator GetAnimator();
        public abstract bool IsAutomatic();
        public abstract bool HasAmmunition();
        public abstract bool IsFull();
        public abstract float GetRateOfFire();
        public abstract RuntimeAnimatorController GetAnimatorController();
        public abstract WeaponAttachmentManagerBehaviour GetAttachmentManager();
        #endregion

        #region METHODS
        /// <param name="spreadMultiplier">Value to multiply the weapon's spread by.</param>
        public abstract void Fire(float spreadMultiplier = 1.0f);

        /// Begin the reload sequence on the weapon animator.
        public abstract void Reload();

        /// Snap the weapon animator back to idle immediately. Called by Character.CancelReload().
        public abstract void CancelReload();

        /// Fills ammunition by amount, or fully if amount is 0.
        public abstract void FillAmmunition(int amount);

        /// Ejects a casing. Typically called from an animation event.
        public abstract void EjectCasing();
        #endregion
    }
}