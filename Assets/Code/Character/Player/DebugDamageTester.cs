using UnityEngine;

public class DebugDamageTester : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private float damageAmount = 10f;

    private void Reset()
    {
        // Auto-find PlayerHealth in the scene when component is added
        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    private void Start()
    {
        // Validate that we have a reference
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            
            if (playerHealth == null)
            {
                Debug.LogError("[DebugDamageTester] No PlayerHealth found! Assign it in the inspector or add PlayerHealth to the scene.");
                enabled = false;
                return;
            }
        }
        
        Debug.Log($"[DebugDamageTester] Ready! Press N to damage ({damageAmount}), M to heal ({damageAmount})");
        Debug.Log($"[DebugDamageTester] Current Health: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}");
    }

    private void Update()
    {
        if (playerHealth == null) return;

        if (Input.GetKeyDown(KeyCode.N))
        {
            Debug.Log($"[DebugDamageTester] Dealing {damageAmount} damage. Health before: {playerHealth.CurrentHealth}");
            playerHealth.TakeDamage(damageAmount);
            Debug.Log($"[DebugDamageTester] Health after: {playerHealth.CurrentHealth}");
        }
        
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log($"[DebugDamageTester] Healing {damageAmount}. Health before: {playerHealth.CurrentHealth}");
            playerHealth.Heal(damageAmount);
            Debug.Log($"[DebugDamageTester] Health after: {playerHealth.CurrentHealth}");
        }
        
        // Bonus: K to kill, R to reset
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("[DebugDamageTester] Killing player...");
            playerHealth.Kill();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("[DebugDamageTester] Resetting health to full");
            playerHealth.ResetToFull();
        }
    }
}