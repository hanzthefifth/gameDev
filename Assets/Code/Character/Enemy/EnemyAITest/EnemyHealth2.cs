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

    // Cached from the most recent bullet hit.
    private bool      hasCachedImpact;
    private Vector3   cachedForce;       // raw playerCamera.forward * impactForce from weapon
    private Vector3   cachedImpactPoint;
    private Rigidbody cachedImpactBody;  // the specific bone rigidbody that was struck

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

    public void TakeDamage(float amount, Vector3 force, Vector3 hitPoint, Rigidbody hitBody = null)
    {
        // Always update so the killing shot drives the ragdoll, not an earlier hit.
        hasCachedImpact  = true;
        cachedForce      = force;      // direction * weapon impactForce, passed straight through
        cachedImpactPoint = hitPoint;
        cachedImpactBody  = hitBody;   // bone rigidbody the raycast landed on

        TakeDamage(amount);
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

        if (ragdollController != null)
        {
            ragdollController.EnableRagdoll();

            if (hasCachedImpact)
                ragdollController.ApplyImpact(cachedForce, cachedImpactPoint, cachedImpactBody);
        }

        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

        if (!neverDestroyOnDeath && destroyDelay > 0f)
            Destroy(gameObject, destroyDelay);
    }
}