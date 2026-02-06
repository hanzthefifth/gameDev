using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Attach this to any HumanoidEnemyAI to get detailed debug logging.
/// This component monitors the AI's state and actions without modifying the AI itself.
/// </summary>
[RequireComponent(typeof(HumanoidEnemyAI))]
public class EnemyAIDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableStateLogs = true;
    [SerializeField] private bool enableCombatLogs = true;
   // [SerializeField] private bool enableRaycastVisualization = true;
    [SerializeField] private bool enablePerceptionLogs = false;
    [SerializeField] private float logInterval = 1f; // Log state every X seconds

    [Header("Visualization")]
    [SerializeField] private bool showRanges = true;
    [SerializeField] private bool showRaycastLines = true;
    [SerializeField] private float raycastLineDuration = 0.5f;

    private HumanoidEnemyAI enemyAI;
    private NavMeshAgent agent;
    private Transform playerTransform;
    private float lastLogTime;
    private string lastState = "";
    
    // Reflection cache for accessing private fields
    private System.Reflection.FieldInfo stateField;
    private System.Reflection.FieldInfo playerField;
    private System.Reflection.FieldInfo nextAttackTimeField;

    private void Awake()
    {
        enemyAI = GetComponent<HumanoidEnemyAI>();
        agent = GetComponent<NavMeshAgent>();

        // Cache reflection info for accessing private fields
        var type = typeof(HumanoidEnemyAI);
        stateField = type.GetField("state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        playerField = type.GetField("player", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        nextAttackTimeField = type.GetField("nextAttackTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Debug.Log($"[EnemyDebugger] Attached to {gameObject.name}");
    }

    private void Update()
    {
        if (!enableStateLogs && !enableCombatLogs)
            return;

        // Get current state via reflection
        var currentState = stateField?.GetValue(enemyAI)?.ToString() ?? "Unknown";
        playerTransform = playerField?.GetValue(enemyAI) as Transform;

        // Log state changes
        if (enableStateLogs && currentState != lastState)
        {
            float distance = playerTransform != null ? Vector3.Distance(transform.position, playerTransform.position) : -1f;
            Debug.Log($"[EnemyDebugger] {gameObject.name} state changed: {lastState} → {currentState} (Player distance: {distance:F2})");
            lastState = currentState;
        }

        // Periodic state logging
        if (enableStateLogs && Time.time - lastLogTime >= logInterval)
        {
            lastLogTime = Time.time;
            LogCurrentState(currentState);
        }
    }

    private void LogCurrentState(string state)
    {
        if (playerTransform == null)
        {
            Debug.Log($"[EnemyDebugger] {gameObject.name} - State: {state}, No player detected");
            return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        float nextAttackTime = (float)(nextAttackTimeField?.GetValue(enemyAI) ?? 0f);
        float timeUntilAttack = Mathf.Max(0f, nextAttackTime - Time.time);

        string message = $"[EnemyDebugger] {gameObject.name}\n" +
                        $"  State: {state}\n" +
                        $"  Distance to Player: {distance:F2}\n" +
                        $"  Agent Speed: {agent.velocity.magnitude:F2}\n" +
                        $"  Agent Stopped: {agent.isStopped}\n" +
                        $"  Time Until Next Attack: {timeUntilAttack:F2}s";

        Debug.Log(message);
    }

    // This method can be called by the AI when it fires (you'd need to add a UnityEvent or make this public and call it)
    public void OnRangedShotFired(Vector3 origin, Vector3 direction, float range)
    {
        if (!enableCombatLogs)
            return;

        Debug.Log($"[EnemyDebugger] {gameObject.name} fired ranged shot from {origin} in direction {direction}");

        if (showRaycastLines)
        {
            Debug.DrawRay(origin, direction * range, Color.red, raycastLineDuration);
        }
    }

    public void OnRaycastHit(RaycastHit hit, bool foundDamageable, float damage)
    {
        if (!enableCombatLogs)
            return;

        Debug.Log($"[EnemyDebugger] Raycast HIT: {hit.collider.name} at distance {hit.distance:F2}\n" +
                  $"  Tag: {hit.collider.tag}, Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}\n" +
                  $"  IDamageable Found: {foundDamageable}\n" +
                  $"  Damage Applied: {(foundDamageable ? damage.ToString() : "N/A")}");

        if (!foundDamageable)
        {
            Component[] components = hit.collider.GetComponentsInParent<Component>();
            Debug.Log($"[EnemyDebugger] Components on hit object: {string.Join(", ", System.Array.ConvertAll(components, c => c.GetType().Name))}");
        }
    }

    public void OnRaycastMiss(Vector3 origin, Vector3 direction, float range)
    {
        if (!enableCombatLogs)
            return;

        Debug.Log($"[EnemyDebugger] Raycast MISSED - No hit detected");
        
        if (showRaycastLines)
        {
            Debug.DrawRay(origin, direction * range, Color.yellow, raycastLineDuration);
        }
    }

    public void OnMeleeAttack(float damage)
    {
        if (!enableCombatLogs)
            return;

        Debug.Log($"[EnemyDebugger] {gameObject.name} executed melee attack ({damage} damage)");
    }

    public void OnPlayerDetected(Transform player, float distance)
    {
        if (!enablePerceptionLogs)
            return;

        Debug.Log($"[EnemyDebugger] Player detected at distance {distance:F2}");
    }

    public void OnPlayerLost()
    {
        if (!enablePerceptionLogs)
            return;

        Debug.Log($"[EnemyDebugger] Lost sight of player");
    }

    private void OnDrawGizmos()
    {
        if (!showRanges || !Application.isPlaying)
            return;

        // Get serialized field values via reflection to show ranges
        var type = typeof(HumanoidEnemyAI);
        var attackRangeField = type.GetField("attackRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var rangedAttackRangeField = type.GetField("rangedAttackRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var preferredRangedDistanceField = type.GetField("preferredRangedDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var preferredDistanceToleranceField = type.GetField("preferredDistanceTolerance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (enemyAI == null)
            enemyAI = GetComponent<HumanoidEnemyAI>();

        float attackRange = (float)(attackRangeField?.GetValue(enemyAI) ?? 0f);
        float rangedAttackRange = (float)(rangedAttackRangeField?.GetValue(enemyAI) ?? 0f);
        float preferredRangedDistance = (float)(preferredRangedDistanceField?.GetValue(enemyAI) ?? 0f);
        float preferredDistanceTolerance = (float)(preferredDistanceToleranceField?.GetValue(enemyAI) ?? 0f);

        // Melee range (red)
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Ranged attack range (blue)
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, rangedAttackRange);

        // Preferred ranged distance zone (cyan)
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        float lowerBound = preferredRangedDistance - preferredDistanceTolerance;
        float upperBound = preferredRangedDistance + preferredDistanceTolerance;
        Gizmos.DrawWireSphere(transform.position, lowerBound);
        Gizmos.DrawWireSphere(transform.position, upperBound);

        // Draw line to player if detected
        if (playerTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position + Vector3.up, playerTransform.position + Vector3.up);
        }
    }
}
