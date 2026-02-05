using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public class StaminaUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Slider slider;
        private Movement movement;

        private void Start()
        {
            // Find the Movement component
            movement = FindFirstObjectByType<Movement>();
            
            if (movement != null)
            {
                // Subscribe to the stamina changed event
                movement.OnStaminaChanged += UpdateStamina;
                
                // Initialize with current values
                UpdateStamina(movement.CurrentStamina, movement.MaxStamina);
            }
            else
            {
                Debug.LogWarning("StaminaUI: Could not find Movement component!");
            }
        }

        private void OnDestroy()
        {
            // IMPORTANT: Unsubscribe to prevent memory leaks
            if (movement != null)
            {
                movement.OnStaminaChanged -= UpdateStamina;
            }
        }

        private void UpdateStamina(float current, float max)
        {
            float value = max > 0.0f ? current / max : 0.0f;

            if (fillImage != null)
                fillImage.fillAmount = value;

            if (slider != null)
                slider.value = value;
        }
    }
}