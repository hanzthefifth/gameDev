// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame
{
    
    /// Sound Manager Service Interface.
    
    public interface IAudioManagerService : IGameService
    {
        
        /// Plays a one shot of the AudioClip.
        
        /// <param name="clip">Clip to play.</param>
        /// <param name="settings">Audio Settings.</param>
        void PlayOneShot(AudioClip clip, AudioSettings settings = default);

        
        /// Plays a one shot of the AudioClip, but waits for <paramref name="delay"/> before doing so.
        
        /// <param name="clip">Clip to play.</param>
        /// <param name="settings">Audio settings to use for this sound.</param>
        /// <param name="delay">Time to wait before we start playing this AudioClip.</param>
        void PlayOneShotDelayed(AudioClip clip, AudioSettings settings = default, float delay = 1.0f);
    }
}