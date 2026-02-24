// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame
{
    
    /// Scope Behaviour.
    
    public abstract class ScopeBehaviour : MonoBehaviour
    {
        #region GETTERS

        
        /// Returns the Sprite used on the Character's Interface.
        
        public abstract Sprite GetSprite();

        #endregion
    }
}