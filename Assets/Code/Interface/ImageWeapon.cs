// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;
using UnityEngine.UI;

namespace MyGame.Interface
{
    
    /// Weapon Image. Handles assigning the proper sprites to the weapon images.
    
    public class ImageWeapon : Element
    {
        #region FIELDS SERIALIZED

        [Header("Settings")]
        
        [Tooltip("Weapon Body Image.")]
        [SerializeField]
        private Image imageWeaponBody;
        
        [Tooltip("Weapon Magazine Image.")]
        [SerializeField]
        private Image imageWeaponMagazine;
        
        [Tooltip("Weapon Scope Default Image.")]
        [SerializeField]
        private Image imageWeaponScopeDefault;

        #endregion

        #region FIELDS

        
        /// Weapon Attachment Manager.
        
        private WeaponAttachmentManagerBehaviour attachmentManagerBehaviour;

        #endregion

        #region METHODS

        protected override void Tick()
        {
            //Get Attachment Manager.
            attachmentManagerBehaviour = equippedWeapon.GetAttachmentManager();
            //Update the weapon's body sprite!
            imageWeaponBody.sprite = equippedWeapon.GetSpriteBody();

            //Sprite.
            Sprite sprite = default;

            //Scope Default.
            ScopeBehaviour scopeDefaultBehaviour = attachmentManagerBehaviour.GetEquippedScopeDefault();
            //Get Sprite.
            if (scopeDefaultBehaviour != null)
                sprite = scopeDefaultBehaviour.GetSprite();
            //Assign Sprite!
            AssignSprite(imageWeaponScopeDefault, sprite, scopeDefaultBehaviour == null);

            //Magazine.
            MagazineBehaviour magazineBehaviour = attachmentManagerBehaviour.GetEquippedMagazine();
            //Get Sprite.
            if (magazineBehaviour != null)
                sprite = magazineBehaviour.GetSprite();
            //Assign Sprite!
            AssignSprite(imageWeaponMagazine, sprite, magazineBehaviour == null);
        }

        
        /// Assigns a sprite to an image.
        
        private static void AssignSprite(Image image, Sprite sprite, bool forceHide = false)
        {
            //Update.
            image.sprite = sprite;
            //Disable image if needed.
            image.enabled = sprite != null && !forceHide;
        }

        #endregion
    }
}