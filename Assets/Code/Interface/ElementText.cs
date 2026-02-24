// Copyright 2021, Infima Games. All Rights Reserved.

using TMPro;
using UnityEngine;

namespace MyGame.Interface
{
    
    /// Text Interface Element.
    
    [RequireComponent(typeof(TextMeshProUGUI))]
    public abstract class ElementText : Element
    {
        #region FIELDS   
        /// Text Mesh. 
        protected TextMeshProUGUI textMesh;

        #endregion

        #region UNITY

        protected override void Awake()
        {
            //Base.
            base.Awake();

            //Get Text Mesh.
            textMesh = GetComponent<TextMeshProUGUI>();
        }

        #endregion
    }
}