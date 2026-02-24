// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame
{
    
    /// Audio Settings used to interact with the AudioManagerService.
    
    [System.Serializable]
    public struct AudioSettings
    {
        
        /// Automatic Cleanup Getter.
        
        public bool AutomaticCleanup => automaticCleanup;
        
        /// Volume Getter.
        
        public float Volume => volume;
        
        /// Spatial Blend Getter.
        
        public float SpatialBlend => spatialBlend;

        [Header("Settings")]
        
        [Tooltip("If true, any AudioSource created will be removed after it has finished playing its clip.")]
        [SerializeField]
        private bool automaticCleanup;

        [Tooltip("Volume.")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float volume;

        [Tooltip("Spatial Blend.")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float spatialBlend;

        
        /// Constructor.
        
        public AudioSettings(float volume = 1.0f, float spatialBlend = 0.0f, bool automaticCleanup = true)
        {
            //Volume.
            this.volume = volume;
            //Spatial Blend.
            this.spatialBlend = spatialBlend;
            //Automatic Cleanup.
            this.automaticCleanup = automaticCleanup;
        }
    }
}