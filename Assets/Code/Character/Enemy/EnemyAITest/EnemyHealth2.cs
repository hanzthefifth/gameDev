using UnityEngine;
using EnemyAI.Complete;

public class EnemyHealth2 : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float destroyDelay = 10f;
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("Lifetime")]
    [Tooltip("If true, the enemy GameObject will not be destroyed on death.")]
    [SerializeField] private bool neverDestroyOnDeath = true;

    [Header("Death Physics")]
    [SerializeField] private RagdollController ragdollController;

    private float currentHealth;
    private bool  isDead;

    // AI subsystems
    private CombatAI           combatAI;
    private PerceptionSystem   perception;
    private CombatStateMachine stateMachine;
    private TacticalMovement   movement;
    private WeaponSystem       weapon;
    private RoleProfile        role;

    public bool IsDead => isDead;

    // -------------------------------------------------------------------------
    private void Awake()
    {
        currentHealth = maxHealth;

        if (ragdollController == null)
            ragdollController = GetComponent<RagdollController>();

        combatAI     = GetComponent<CombatAI>();
        perception   = GetComponent<PerceptionSystem>();
        stateMachine = GetComponent<CombatStateMachine>();
        movement     = GetComponent<TacticalMovement>();
        weapon       = GetComponent<WeaponSystem>();
        role         = GetComponent<RoleProfile>();
    }

    // -------------------------------------------------------------------------
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f) Die();
    }

    // -------------------------------------------------------------------------
    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (combatAI     != null) combatAI.enabled     = false;
        if (stateMachine != null) stateMachine.enabled = false;
        if (movement     != null) movement.enabled     = false;
        if (weapon       != null) weapon.enabled       = false;
        if (perception   != null) perception.enabled   = false;
        if (role         != null) role.enabled         = false;

        // Projectile physics handles ragdoll movement on ranged hits.
        // EnableRagdoll() just releases the bones — no impulse needed here.
        if (ragdollController != null)
            ragdollController.EnableRagdoll();

        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

        if (!neverDestroyOnDeath && destroyDelay > 0f)
            Destroy(gameObject, destroyDelay);
    }
}