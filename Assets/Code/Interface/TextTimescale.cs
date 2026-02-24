// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame.Interface
{
    
    /// Component that changes a text to match the current time scale.
    
    public class TextTimescale : ElementText
    {
        #region METHODS

        protected override void Tick()
        {
            //Change text to match the time scale!
            textMesh.text = "Timescale : " + Time.timeScale;
        }        

        #endregion
    }
}