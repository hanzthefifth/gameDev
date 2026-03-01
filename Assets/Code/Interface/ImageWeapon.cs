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
            // If no weapon is equipped, hide everything safely.
            if (equippedWeapon == null)
            {
                AssignSprite(imageWeaponBody, null, true);
                AssignSprite(imageWeaponScopeDefault, null, true);
                AssignSprite(imageWeaponMagazine, null, true);
                return;
            }

            // Update weapon body sprite safely.
            imageWeaponBody.sprite = equippedWeapon.GetSpriteBody();

            // Get attachment manager (may be null for melee weapons).
            attachmentManagerBehaviour = equippedWeapon.GetAttachmentManager();

            // If no attachment manager (melee weapon), hide scope and magazine UI.
            if (attachmentManagerBehaviour == null)
            {
                AssignSprite(imageWeaponScopeDefault, null, true);
                AssignSprite(imageWeaponMagazine, null, true);
                return;
            }

            // Sprite temp variable.
            Sprite sprite = null;

            // Scope Default.
            ScopeBehaviour scopeDefaultBehaviour = attachmentManagerBehaviour.GetEquippedScopeDefault();
            if (scopeDefaultBehaviour != null)
                sprite = scopeDefaultBehaviour.GetSprite();

            AssignSprite(imageWeaponScopeDefault, sprite, scopeDefaultBehaviour == null);

            // Magazine.
            MagazineBehaviour magazineBehaviour = attachmentManagerBehaviour.GetEquippedMagazine();
            sprite = null;

            if (magazineBehaviour != null)
                sprite = magazineBehaviour.GetSprite();

            AssignSprite(imageWeaponMagazine, sprite, magazineBehaviour == null);
        }

        /// Assigns a sprite to an image.
        private static void AssignSprite(Image image, Sprite sprite, bool forceHide = false)
        {
            if (image == null) return;

            image.sprite = sprite;
            image.enabled = sprite != null && !forceHide;
        }

        #endregion
    }
}