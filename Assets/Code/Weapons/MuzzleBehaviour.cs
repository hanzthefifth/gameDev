// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame
{
    
    /// Muzzle Abstract Class.
    
    public abstract class MuzzleBehaviour : MonoBehaviour
    {
        #region GETTERS

        
        /// Returns the firing socket. This is the point that we use to fire the bullets.
        
        public abstract Transform GetSocket();

        
        /// Returns the Sprite used on the Character's Interface.
        
        public abstract Sprite GetSprite();
        
        /// Returns the AudioClip to play when firing.
        
        public abstract AudioClip GetAudioClipFire();
        
        
        /// Returns the particle system to use when firing.
        
        public abstract ParticleSystem GetParticlesFire();
        
        /// Returns the number of particles to emit when firing.
        
        public abstract int GetParticlesFireCount();

        
        /// Returns the light component used when firing..
        
        public abstract Light GetFlashLight();
        
        /// Returns the time it takes for the light flash to be hidden.
        
        public abstract float GetFlashLightDuration();

        #endregion

        #region METHODS

        
        /// Plays all of the muzzle effects.
        
        public abstract void Effect(); 

        #endregion
    }
}