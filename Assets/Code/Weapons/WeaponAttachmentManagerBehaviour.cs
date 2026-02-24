// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame
{
    
    /// Weapon Attachment Manager Behaviour.
    
    public abstract class WeaponAttachmentManagerBehaviour : MonoBehaviour
    {
        #region UNITY FUNCTIONS

        
        /// Awake.
        
        protected virtual void Awake(){}

        
        /// Start.
        
        protected virtual void Start(){}

        
        /// Update.
        
        protected virtual void Update(){}

        
        /// Late Update.
        
        protected virtual void LateUpdate(){}

        #endregion
        
        #region GETTERS

        
        /// Returns the equipped scope.
        
        public abstract ScopeBehaviour GetEquippedScope();
        
        /// Returns the equipped scope default.
        
        public abstract ScopeBehaviour GetEquippedScopeDefault();
        
        
        /// Returns the equipped magazine.
        
        public abstract MagazineBehaviour GetEquippedMagazine();
        
        /// Returns the equipped muzzle.
        
        public abstract MuzzleBehaviour GetEquippedMuzzle();
        
        #endregion
    }
}