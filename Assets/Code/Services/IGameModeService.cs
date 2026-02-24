// Copyright 2021, Infima Games. All Rights Reserved.

namespace MyGame
{
    
    /// MyGame Mode Service.
    
    public interface IGameModeService : IGameService
    {
        
        /// Returns the Player Character.
        
        CharacterBehaviour GetPlayerCharacter();
    }
}