using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour, IDamageable
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
    private HumanoidEnemyAI ai;

    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        colliders = GetComponentsInChildren<Collider>();
        ai = GetComponent<HumanoidEnemyAI>();
    }

    public void TakeDamage(float amount)
    {
        if (isDead)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        if (ai != null)
        {
            ai.enabled = false;
        }

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (animator != null)
        {
            animator.SetTrigger("Die");
            animator.SetBool("IsAttacking", false);
            animator.SetFloat("Speed", 0f);
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }

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
