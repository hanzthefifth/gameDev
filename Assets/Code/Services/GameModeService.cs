// Copyright 2021, Infima Games. All Rights Reserved.

namespace MyGame
{
    
    /// MyGame Mode Service.
    
    public class GameModeService : IGameModeService
    {
        #region FIELDS
        
        
        /// The Player Character.
        
        private CharacterBehaviour playerCharacter;
        
        #endregion
        
        #region FUNCTIONS
        
        public CharacterBehaviour GetPlayerCharacter()
        {
            //Make sure we have a player character that is good to go!
            if (playerCharacter == null)
                playerCharacter = UnityEngine.Object.FindFirstObjectByType<CharacterBehaviour>();
            
            //Return.
            return playerCharacter;
        }
        
        #endregion
    }
}