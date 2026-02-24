// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame
{
    
    /// Magazine.
    
    public class Magazine : MagazineBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Settings")]
        
        [Tooltip("Total Ammunition.")]
        [SerializeField]
        private int ammunitionTotal = 10;

        [Header("Interface")]

        [Tooltip("Interface Sprite.")]
        [SerializeField]
        private Sprite sprite;

        #endregion

        #region GETTERS

        
        /// Ammunition Total.
        
        public override int GetAmmunitionTotal() => ammunitionTotal;
        
        /// Sprite.
        
        public override Sprite GetSprite() => sprite;

        #endregion
    }
}