// Copyright 2021, Infima Games. All Rights Reserved.

namespace Game.LowPolyShooterPack
{
    /// <summary>
    /// Game Mode Service.
    /// </summary>
    public interface IGameModeService : IGameService
    {
        /// <summary>
        /// Returns the Player Character.
        /// </summary>
        CharacterBehaviour GetPlayerCharacter();
    }
}