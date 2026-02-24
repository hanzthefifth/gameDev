// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGame
{
    
    /// Time Manager.
    
    public class TimeHandler : MonoBehaviour
    {
        [Header("Settings")]
        
        [Tooltip("Value the time scale gets updated by every time.")]
        [SerializeField]
        private float increment = 0.1f;
        
        
        /// Determines if the time is stopped.
        
        private bool paused;
        
        
        /// Current Time Scale.
        
        private float current = 1.0f;

        
        /// Updates The Time Scale.
        
        private void Scale()
        {
            //Update Time Scale.
            Time.timeScale = current;
        }
        
        
        /// Change Time Scale.
        
        private void Change(float value = 1.0f)
        {
            //Save Value.
            current = value;
            
            //Update.
            Scale();
        }

        
        /// Increase Time Scale Value.
        
        private void Increase(float value = 1.0f)
        {
            //Change.
            Change(Mathf.Clamp01(current + value));
        }

        
        /// Pause.
        
        private void Pause()
        {
            //Pause.
            paused = true;
            
            //Pause.
            Time.timeScale = 0.0f;
        }
        
        
        /// Toggle Pause.
        
        private void Toggle()
        {
            //Toggle Pause.
            if (paused)
                Unpause();
            else
                Pause();
        }

        
        /// Unpause.
        
        private void Unpause()
        {
            //Unpause.
            paused = false;
            
            //Unpause.
            Change(current);
        }

        
        /// Increase Time Scale Event.
        
        public virtual void OnIncrease(InputAction.CallbackContext context)
        {
            //Switch.
            switch (context.phase)
            {
                //Performed.
                case InputActionPhase.Performed:
                    //Increase.
                    Increase(increment);
                    break;
            }
        }
        
        
        /// Increase Time Scale Event.
        
        public virtual void OnDecrease(InputAction.CallbackContext context)
        {
            //Switch.
            switch (context.phase)
            {
                //Performed.
                case InputActionPhase.Performed:
                    //Increase.
                    Increase(-increment);
                    break;
            }
        }

        
        /// Toggle Time Scale Stop.
        
        public virtual void OnToggle(InputAction.CallbackContext context)
        {
            //Switch.
            switch (context.phase)
            {
                //Performed.
                case InputActionPhase.Performed:
                    //Toggle.
                    Toggle();
                    break;
            }      
        }
    }
}