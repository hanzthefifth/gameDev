using UnityEngine;
using UnityEngine.AI;
using EnemyAI.Complete; // <-- to see IDamageable and CombatAI


public class EnemyHealth2 : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float destroyDelay = 3f;
    [SerializeField] private GameObject deathEffectPrefab;

    private float currentHealth;
    private bool isDead;

    private Animator animator;
    private NavMeshAgent agent;
    private Collider[] colliders;

    // Old AI
    private HumanoidEnemyAI oldAi;

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

        animator  = GetComponentInChildren<Animator>();
        agent     = GetComponent<NavMeshAgent>();
        colliders = GetComponentsInChildren<Collider>();

        // Old AI (optional, for backward compatibility)
        oldAi = GetComponent<HumanoidEnemyAI>();

        // New modular AI
        combatAI      = GetComponent<CombatAI>();
        perception    = GetComponent<PerceptionSystem>();
        stateMachine  = GetComponent<CombatStateMachine>();
        movement      = GetComponent<TacticalMovement>();
        weapon        = GetComponent<WeaponSystem>();
        role          = GetComponent<RoleProfile>();
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


    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        // Disable old AI if present
        if (oldAi != null)
        {
            oldAi.enabled = false;
        }

        // Disable new AI brain and subsystems, so nothing keeps calling SetDestination etc.
        if (combatAI != null)      combatAI.enabled = false;
        if (stateMachine != null)  stateMachine.enabled = false;
        if (movement != null)      movement.enabled = false;
        if (weapon != null)        weapon.enabled = false;
        if (perception != null)    perception.enabled = false;
        if (role != null)          role.enabled = false;

        // Stop navmesh movement safely
        if (agent != null)
        {
            // Just stopping is enough; disabling is optional once AI is disabled
            agent.isStopped = true;

            // If you really want to fully disable:
            // agent.enabled = false;
        }

        // Play death animation if you have one
        if (animator != null)
        {
            // These will only actually fire if you add the parameters later
            animator.SetTrigger("Die");
            animator.SetBool("IsAttacking", false);
            animator.SetFloat("Speed", 0f);
        }

        // Disable colliders so the corpse no longer blocks navmesh or bullets
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        // Optional death VFX
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }

        // Destroy after a delay
        if (destroyDelay > 0f)
        {
            Destroy(gameObject, destroyDelay);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
