// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;
using System.Collections;

namespace MyGame
{
    
    /// Manages the spawning and playing of sounds.
    
    public class AudioManagerService : MonoBehaviour, IAudioManagerService
    {
        
        /// Contains data related to playing a OneShot audio.
        
        private readonly struct OneShotCoroutine
        {
            
            /// Audio Clip.
            
            public AudioClip Clip { get; }
            
            /// Audio Settings.
            
            public AudioSettings Settings { get; }
            
            /// Delay.
            
            public float Delay { get; }
            
            
            /// Constructor.
            
            public OneShotCoroutine(AudioClip clip, AudioSettings settings, float delay)
            {
                //Clip.
                Clip = clip;
                //Settings
                Settings = settings;
                //Delay.
                Delay = delay;
            }
        }

        
        /// Destroys the audio source once it has finished playing.
        
        private IEnumerator DestroySourceWhenFinished(AudioSource source)
        {
            //Wait for the audio source to complete playing the clip.
            yield return new WaitWhile(() => source.isPlaying);
            
            //Destroy the audio MyGame object, since we're not using it anymore.
            //This isn't really too great for performance, but it works, for now.
            DestroyImmediate(source.gameObject);
        }

        
        /// Waits for a certain amount of time before starting to play a one shot sound.
        
        private IEnumerator PlayOneShotAfterDelay(OneShotCoroutine value)
        {
            //Wait for the delay.
            yield return new WaitForSeconds(value.Delay);
            //Play.
            PlayOneShot_Internal(value.Clip, value.Settings);
        }
        
        
        /// Internal PlayOneShot. Basically does the whole function's name!
        
        private void PlayOneShot_Internal(AudioClip clip, AudioSettings settings)
        {
            //No need to do absolutely anything if the clip is null.
            if (clip == null)
                return;
            
            //Spawn a MyGame object for the audio source.
            var newSourceObject = new GameObject($"Audio Source -> {clip.name}");
            //Add an audio source component to that object.
            var newAudioSource = newSourceObject.AddComponent<AudioSource>();

            //Set volume.
            newAudioSource.volume = settings.Volume;
            //Set spatial blend.
            newAudioSource.spatialBlend = settings.SpatialBlend;
            
            //Play the clip!
            newAudioSource.PlayOneShot(clip);
            
            //Start a coroutine that will destroy the whole object once it is done!
            if(settings.AutomaticCleanup)
                StartCoroutine(nameof(DestroySourceWhenFinished), newAudioSource);
        }

        #region Audio Manager Service Interface

        public void PlayOneShot(AudioClip clip, AudioSettings settings = default)
        {
            //Play.
            PlayOneShot_Internal(clip, settings);
        }

        public void PlayOneShotDelayed(AudioClip clip, AudioSettings settings = default, float delay = 1.0f)
        {
            //Play.
            StartCoroutine(nameof(PlayOneShotAfterDelay), new OneShotCoroutine(clip, settings, delay));
        }

        #endregion
    }
}