// Copyright 2021, Infima Games. All Rights Reserved.

namespace MyGame
{
    /// <summary>
    /// MyGame Mode Service.
    /// </summary>
    public interface IGameModeService : IGameService
    {
        /// <summary>
        /// Returns the Player Character.
        /// </summary>
        CharacterBehaviour GetPlayerCharacter();
    }
}