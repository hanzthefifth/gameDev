// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;
using System.Globalization;

namespace MyGame.Interface
{
    /// <summary>
    /// Current Ammunition Text.
    /// </summary>
    public class TextAmmunitionCurrent : ElementText
    {
        #region FIELDS SERIALIZED
        
        [Header("Colors")]
        
        [Tooltip("Determines if the color of the text should changes as ammunition is fired.")]
        [SerializeField]
        private bool updateColor = true;
        
        [Tooltip("Determines how fast the color changes as the ammunition is fired.")]
        [SerializeField]
        private float emptySpeed = 1.5f;
        
        [Tooltip("Color used on this text when the player character has no ammunition.")]
        [SerializeField]
        private Color emptyColor = Color.red;
        
        #endregion
        
        #region METHODS
        
        /// <summary>
        /// Tick.
        /// </summary>
        protected override void Tick()
        {
            float current = equippedWeapon.GetAmmunitionCurrent();
            float total   = equippedWeapon.GetAmmunitionTotal();

            textMesh.text = current.ToString(CultureInfo.InvariantCulture);

            if (updateColor)
            {
                // Avoid divide-by-zero / NaN if total is 0 or “infinite”.
                if (total <= 0.0f || float.IsInfinity(total))
                {
                    textMesh.color = Color.white; // or emptyColor, up to you
                    return;
                }

                float colorAlpha = (current / total) * emptySpeed;
                textMesh.color = Color.Lerp(emptyColor, Color.white, colorAlpha);
            }
        }
        
        #endregion
    }
}