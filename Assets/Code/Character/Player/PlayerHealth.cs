using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool regenEnabled = false;
    [SerializeField] private float regenPerSecond = 5f;
    [SerializeField] private float regenDelay = 3f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    // Events
    // normalized = 0..1
    public event Action<float> OnHealthNormalizedChanged;
    public event Action<float, float> OnHealthChanged; // current, max
    public event Action<float> OnDamaged;              // damage amount
    public event Action OnDeath;

    private float _lastDamageTime;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        Debug.Log($"[PlayerHealth] Initialized with {CurrentHealth}/{maxHealth} HP");
        NotifyHealthChanged();
    }

    private void Update()
    {
        if (!regenEnabled || IsDead) return; //dont regen if dead

        // Simple delayed regen
        if (Time.time - _lastDamageTime >= regenDelay && CurrentHealth < maxHealth)
        {
            float old = CurrentHealth;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + regenPerSecond * Time.deltaTime);

            if (!Mathf.Approximately(CurrentHealth, old))
            {
                //Debug.Log($"[PlayerHealth] Regenerating: {CurrentHealth:F1}/{maxHealth}");
                NotifyHealthChanged();
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            if (IsDead)
                Debug.LogWarning("[PlayerHealth] Cannot take damage - already dead!");
            return;
        }

        _lastDamageTime = Time.time;

        float old = CurrentHealth;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

        Debug.Log($"[PlayerHealth] Took {amount} damage. {old:F1} → {CurrentHealth:F1}");
        
        OnDamaged?.Invoke(amount);

        if (!Mathf.Approximately(CurrentHealth, old))
            NotifyHealthChanged();

        if (CurrentHealth <= 0f && !IsDead)
            Die();
    }

    //i guess we just implement the interface like this for now
    public void TakeDamage(float amount, Vector3 force, Vector3 hitPoint, Rigidbody hitBody = null)
    {
        // Player currently ignores hit context.
        TakeDamage(amount);
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            if (IsDead)
                Debug.LogWarning("[PlayerHealth] Cannot heal - player is dead!");
            return;
        }

        float old = CurrentHealth;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);

        if (!Mathf.Approximately(CurrentHealth, old))
        {
            Debug.Log($"[PlayerHealth] Healed {amount}. {old:F1} → {CurrentHealth:F1}");
            NotifyHealthChanged();
        }
    }

    public void Kill()
    {
        if (IsDead) return;
        
        Debug.Log("[PlayerHealth] Player killed!");
        CurrentHealth = 0f;
        NotifyHealthChanged();
        Die();
    }

    public void ResetToFull()
    {
        IsDead = false;
        CurrentHealth = maxHealth;
        Debug.Log($"[PlayerHealth] Reset to full health: {CurrentHealth}/{maxHealth}");
        NotifyHealthChanged();
    }

    private void Die()
    {
        if (IsDead) return;

        IsDead = true;
        Debug.Log("[PlayerHealth] Player died!");
        OnDeath?.Invoke();
        // You can hook your death logic elsewhere (disable input, play anim, reload level, etc.)
    }

    private void NotifyHealthChanged()
    {
        float normalized = maxHealth > 0f ? (CurrentHealth / maxHealth) : 0f; //divide by zero edge case
        
       // Debug.Log($"[PlayerHealth] Notifying listeners - Health: {CurrentHealth:F1}/{maxHealth} (normalized: {normalized:F2})");
        
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnHealthNormalizedChanged?.Invoke(normalized);
    }
}