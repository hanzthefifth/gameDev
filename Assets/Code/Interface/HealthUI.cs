using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image healthFillImage;       // radial/linear bar (set Fill Type to Filled)
    [SerializeField] private TMP_Text healthText;         // optional "100 / 100" text
    [SerializeField] private Slider healthSlider;

    private void Reset()
    {
        // Auto-find PlayerHealth when component is added
        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    private void Start()
    {
        // Fallback if not set in inspector
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            
            if (playerHealth == null)
            {
                Debug.LogError("[HealthUI] No PlayerHealth found! Make sure PlayerHealth exists in the scene.");
                enabled = false;
                return;
            }
        }

        // Subscribe to events
        playerHealth.OnHealthNormalizedChanged += HandleHealthNormalizedChanged;
        //playerHealth.OnHealthChanged += HandleHealthChanged; //for debugging

        // Force initial sync
        //HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth); //for debugging
        HandleHealthNormalizedChanged(playerHealth.CurrentHealth / playerHealth.MaxHealth);
        
        Debug.Log($"[HealthUI] Initialized. Current health: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}");
    }

    private void OnDestroy()
    {
        // IMPORTANT: Unsubscribe to prevent memory leaks
        if (playerHealth != null)
        {
            playerHealth.OnHealthNormalizedChanged -= HandleHealthNormalizedChanged;
           playerHealth.OnHealthChanged -= HandleHealthChanged; //for debugging
        }
    }

    private void HandleHealthNormalizedChanged(float normalized)
    {
        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = normalized;
            //Debug.Log($"[HealthUI] Fill amount updated: {normalized:F2}");
        }
         if (healthSlider != null)
        {
            healthSlider.value = normalized;
            //Debug.Log($"[HealthUI] Slider value updated: {normalized:F2}");
        }
    }

        //health debugging
    private void HandleHealthChanged(float current, float max)
    {
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            //Debug.Log($"[HealthUI] Text updated: {current:F1}/{max:F1}");
        }
    }
}

