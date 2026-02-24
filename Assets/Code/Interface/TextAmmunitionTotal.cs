// Copyright 2021, Infima Games. All Rights Reserved.

using System.Globalization;

namespace MyGame.Interface
{
    
    /// Total Ammunition Text.
    
    public class TextAmmunitionTotal : ElementText
    {
        #region METHODS
        
        
        /// Tick.
        
        protected override void Tick()
        {
            //Total Ammunition.
            float ammunitionTotal = equippedWeapon.GetAmmunitionTotal();
            
            //Update Text.
            textMesh.text = ammunitionTotal.ToString(CultureInfo.InvariantCulture);
        }
        
        #endregion
    }
}