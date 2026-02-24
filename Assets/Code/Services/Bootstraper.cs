// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame
{
    
    /// Bootstraper.
    
    public static class Bootstraper
    {
        
        /// Initialize.
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            //Initialize default service locator.
            ServiceLocator.Initialize();
            
            //MyGame Mode Service.
            ServiceLocator.Current.Register<IGameModeService>(new GameModeService());
            
            #region Sound Manager Service

            //Create an object for the sound manager, and add the component!
            var soundManagerObject = new GameObject("Sound Manager");
            var soundManagerService = soundManagerObject.AddComponent<AudioManagerService>();
            
            //Make sure that we never destroy our SoundManager. We need it in other scenes too!
            Object.DontDestroyOnLoad(soundManagerObject);
            
            //Register the sound manager service!
            ServiceLocator.Current.Register<IAudioManagerService>(soundManagerService);

            #endregion
        }
    }
}