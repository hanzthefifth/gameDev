// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame
{
    
    /// Helper StateMachineBehaviour that allows us to more easily play a specific weapon sound
    public class PlaySoundCharacterBehaviour : StateMachineBehaviour
    {
        
        /// Type of weapon sound. 
        private enum SoundType
        {
            //Holsters.
            Holster, Unholster,
            //Normal Reloads.
            Reload, ReloadEmpty,
            //Firing.
            Fire, FireEmpty,
        }

        #region FIELDS SERIALIZED

        [Header("Setup")]
        [Tooltip("Delay at which the audio is played.")]
        [SerializeField] private float delay;
        [Tooltip("Type of weapon sound to play.")]
        [SerializeField] private SoundType soundType;
        
        [Header("Audio Settings")]
        [Tooltip("Audio Settings.")]
        [SerializeField] private AudioSettings audioSettings = new AudioSettings(1.0f, 0.0f, true);

        #endregion
        #region FIELDS

        
        private CharacterBehaviour playerCharacter;
        private InventoryBehaviour playerInventory;  
        /// The service that handles sounds.
        private IAudioManagerService audioManagerService;
        #endregion 
        #region UNITY

        
        /// On State Enter.
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            //We need to get the character component.
            playerCharacter ??= ServiceLocator.Current.Get<IGameModeService>().GetPlayerCharacter();

            //Get Inventory.
            playerInventory ??= playerCharacter.GetInventory();

            //Try to get the equipped weapon's Weapon component.
            if (!(playerInventory.GetEquipped() is { } weaponBehaviour))
                return;
            
            //Try grab a reference to the sound managing service.
            audioManagerService ??= ServiceLocator.Current.Get<IAudioManagerService>();


            #region Select Clip To Play

            AudioClip clip = soundType switch
            {
                SoundType.Holster => weaponBehaviour.GetAudioClipHolster(),
                SoundType.Unholster => weaponBehaviour.GetAudioClipUnholster(),
                SoundType.Reload => weaponBehaviour.GetAudioClipReload(),
                SoundType.ReloadEmpty => weaponBehaviour.GetAudioClipReloadEmpty(),
                SoundType.Fire => weaponBehaviour.GetAudioClipFire(),
                SoundType.FireEmpty => weaponBehaviour.GetAudioClipFireEmpty(),
                
                //Default.
                _ => default
            };
            #endregion
            //Play with some delay. Granted, if the delay is set to zero, this will just straight-up play!
            audioManagerService.PlayOneShotDelayed(clip, audioSettings, delay);
        }   
        #endregion
    }
}