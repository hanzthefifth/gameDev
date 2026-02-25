using UnityEngine;
using EnemyAI.Complete; // <-- to see IDamageable and CombatAI

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
    [SerializeField] private float deathImpulse = 2.5f;
    [SerializeField] private Vector3 deathImpulseDirection = new Vector3(0f, 0.2f, 1f);

    // Optional: if your weapon code knows hit point/body, call the overload below.
    private bool hasCachedImpact;
    private Vector3 cachedImpactPoint;
    private Vector3 cachedImpactForce;
    private Rigidbody cachedImpactBody;

    private float currentHealth;
    private bool isDead;

    // New AI
    private CombatAI combatAI;
    private PerceptionSystem perception;
    private CombatStateMachine stateMachine;
    private TacticalMovement movement;
    private WeaponSystem weapon;
    private RoleProfile role;

    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;

        if (ragdollController == null)
        {
            ragdollController = GetComponent<RagdollController>();
        }


        // New modular AI
        combatAI = GetComponent<CombatAI>();
        perception = GetComponent<PerceptionSystem>();
        stateMachine = GetComponent<CombatStateMachine>();
        movement = GetComponent<TacticalMovement>();
        weapon = GetComponent<WeaponSystem>();
        role = GetComponent<RoleProfile>();
    }

    public void TakeDamage(float amount)
    {
        if (isDead)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // Optional overload (does NOT replace IDamageable interface method).
    public void TakeDamage(float amount, Vector3 force, Vector3 hitPoint, Rigidbody hitBody = null)
    {
        // Cache the last hit so death can apply force where the bullet landed.
        hasCachedImpact = true;
        cachedImpactPoint = hitPoint;
        cachedImpactBody = hitBody;
        cachedImpactForce = force;

        // Keep existing damage flow.
        TakeDamage(amount);
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        // Disable new AI brain and subsystems, so nothing keeps calling movement/nav APIs
        if (combatAI != null) combatAI.enabled = false;
        if (stateMachine != null) stateMachine.enabled = false;
        if (movement != null) movement.enabled = false;
        if (weapon != null) weapon.enabled = false;
        if (perception != null) perception.enabled = false;
        if (role != null) role.enabled = false;

        if (ragdollController != null)
        {
            // Fallback impulse if we don't have hit context (simple forward + slight up).
            Vector3 fallbackImpulse =
                transform.TransformDirection(deathImpulseDirection.normalized) * deathImpulse;

            // If we have hit info, enable ragdoll first (no default impulse), then apply at the hit.
            // Otherwise, fall back to a simple forward/upper push.
            ragdollController.EnableRagdoll(hasCachedImpact ? Vector3.zero : fallbackImpulse);

            if (hasCachedImpact)
            {
                ragdollController.ApplyImpact(cachedImpactForce, cachedImpactPoint, cachedImpactBody);
            }
        }

        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }

        // For now: keep bodies around for gameplay/testing. You can disable this per-enemy.
        if (!neverDestroyOnDeath && destroyDelay > 0f)
        {
            Destroy(gameObject, destroyDelay);
        }
    }
}