// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame.Interface
{
    
    /// Interface Element.
    
    public abstract class Element : MonoBehaviour
    {
        #region FIELDS
        
        
        /// MyGame Mode Service.
        
        protected IGameModeService gameModeService;
        
        
        /// Player Character.
        
        protected CharacterBehaviour playerCharacter;
        
        /// Player Character Inventory.
        
        protected InventoryBehaviour playerCharacterInventory;

        
        /// Equipped Weapon.
        
        protected WeaponBehaviour equippedWeapon;
        
        #endregion

        #region UNITY

        
        /// Awake.
        
        protected virtual void Awake()
        {
            //Get MyGame Mode Service. Very useful to get MyGame Mode references.
            gameModeService = ServiceLocator.Current.Get<IGameModeService>();
            
            //Get Player Character.
            playerCharacter = gameModeService.GetPlayerCharacter();
            //Get Player Character Inventory.
            playerCharacterInventory = playerCharacter.GetInventory();
        }
        
        
        /// Update.
        
        private void Update()
        {
            //Ignore if we don't have an Inventory.
            if (Equals(playerCharacterInventory, null))
                return;

            //Get Equipped Weapon.
            equippedWeapon = playerCharacterInventory.GetEquipped();
            
            //Tick.
            Tick();
        }

        #endregion

        #region METHODS

        
        /// Tick.
        
        protected virtual void Tick() {}

        #endregion
    }
}