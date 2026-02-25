using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class RagdollController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navMeshAgent;

    [Header("Physics Setup")]
    [Tooltip("The Hips Rigidbody. Drag it here or use the 'Setup Ragdoll' context menu.")]
    [SerializeField] private Rigidbody impulseBody;
    
    [Tooltip("All Rigidbodies in the bone hierarchy.")]
    [SerializeField] private Rigidbody[] ragdollBodies;

    [Header("Impact Tuning")]
    [Tooltip("Clamp the applied impulse magnitude to avoid launching bodies into orbit.")]
    [SerializeField] private float maxImpulseMagnitude = 15f;
    [Tooltip("How much of the pre-death movement velocity should be transferred to the ragdoll.")]
    [SerializeField] private float inheritVelocityMultiplier = 1f;
    [Tooltip("Use AddForceAtPosition for slightly more natural twists on impact.")]
    [SerializeField] private bool useForceAtPosition = true;
    [Tooltip("If true, forces Unity to sync transforms before applying impulse (helps reduce 1-frame 'delay').")]
    [SerializeField] private bool syncTransformsBeforeImpulse = true;

    [Header("External Colliders")]
    [Tooltip("The main AI Capsule Collider that should be disabled on death.")]
    [SerializeField] private Collider[] gameplayColliders;

    [Header("Weapon Drop (Optional)")]
    [Tooltip("The weapon object the enemy is currently holding.")]
    [SerializeField] private GameObject currentWeapon;

    // To handle recovery (getting back up)
    private struct RigidbodyState
    {
        public bool isKinematic;
        public bool useGravity;
        public RigidbodyInterpolation interpolation;
    }
    private RigidbodyState[] savedStates;

    private void Awake()
    {
        if (ragdollBodies == null || ragdollBodies.Length == 0)
        {
            ragdollBodies = GetComponentsInChildren<Rigidbody>();
        }

        // Cache states so we can return to 'Alive' mode perfectly
        savedStates = new RigidbodyState[ragdollBodies.Length];
        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            savedStates[i] = new RigidbodyState
            {
                isKinematic = ragdollBodies[i].isKinematic,
                useGravity = ragdollBodies[i].useGravity,
                interpolation = ragdollBodies[i].interpolation
            };
        }

        SetRagdollPhysicsActive(false);
    }

    public void EnableRagdoll(Vector3 force)
    {
        // Capture movement velocity before shutting systems down.
        // This helps the ragdoll "continue" motion instantly instead of feeling delayed/stiff.
        Vector3 inheritedVelocity = Vector3.zero;
        if (navMeshAgent != null)
        {
            inheritedVelocity = navMeshAgent.velocity;
        }

        // 1. Shut down AI
        if (animator) animator.enabled = false;
        if (navMeshAgent) navMeshAgent.enabled = false;

        // 2. Disable main capsule so the body can fall
        if (gameplayColliders != null)
        {
            foreach (var col in gameplayColliders)
            {
                if (col) col.enabled = false;
            }
        }

        // 3. Drop weapon (Future proofing)
        HandleWeaponDrop();

        // 4. Enable physics on all bones
        SetRagdollPhysicsActive(true);

        // Give the ragdoll an initial velocity so it doesn't "pause" for a frame.
        if (inheritVelocityMultiplier > 0f && inheritedVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 v = inheritedVelocity * inheritVelocityMultiplier;
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                if (ragdollBodies[i] == null) continue;
                ragdollBodies[i].linearVelocity = v;
            }
        }

        // 5. Apply the death impact
        ApplyImpact(force, impulseBody != null ? impulseBody.worldCenterOfMass : transform.position, impulseBody);
    }

    /// <summary>
    /// Apply an impulse to the ragdoll. You can call this from your hit code after the enemy is dead,
    /// or later when you upgrade TakeDamage to pass hit-point info.
    /// </summary>
    public void ApplyImpact(Vector3 impulse, Vector3 worldPoint, Rigidbody targetBody = null)
    {
        if (impulse.sqrMagnitude < 0.0001f) return;

        // Clamp to keep tuning simple and prevent extreme launches.
        if (maxImpulseMagnitude > 0f)
        {
            float mag = impulse.magnitude;
            if (mag > maxImpulseMagnitude)
            {
                impulse = impulse * (maxImpulseMagnitude / mag);
            }
        }

        if (syncTransformsBeforeImpulse)
        {
            Physics.SyncTransforms();
        }

        Rigidbody rb = targetBody != null ? targetBody : impulseBody;
        if (rb == null) return;

        if (useForceAtPosition)
            rb.AddForceAtPosition(impulse, worldPoint, ForceMode.Impulse);
        else
            rb.AddForce(impulse, ForceMode.Impulse);
    }

    public void DisableRagdoll()
    {
        SetRagdollPhysicsActive(false);
        
        if (animator) animator.enabled = true;
        if (navMeshAgent) navMeshAgent.enabled = true;

        if (gameplayColliders != null)
        {
            foreach (var col in gameplayColliders)
            {
                if (col) col.enabled = true;
            }
        }
    }

    private void SetRagdollPhysicsActive(bool active)
    {
        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            Rigidbody rb = ragdollBodies[i];
            if (!rb) continue;

            rb.isKinematic = !active;
            rb.useGravity = active;
            
            // Performance: Only interpolate while the ragdoll is active
            rb.interpolation = active ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;

            if (!active)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void HandleWeaponDrop()
    {
        if (currentWeapon == null) return;

        // Unparent and add physics so the gun falls
        currentWeapon.transform.SetParent(null);
        
        Rigidbody weaponRb = currentWeapon.GetComponent<Rigidbody>();
        if (!weaponRb) weaponRb = currentWeapon.AddComponent<Rigidbody>();
        
        Collider weaponCol = currentWeapon.GetComponent<Collider>();
        if (!weaponCol) weaponCol = currentWeapon.AddComponent<BoxCollider>();

        weaponRb.isKinematic = false;
        weaponRb.useGravity = true;
        weaponCol.enabled = true;

        // Add random spin for a natural look
        weaponRb.AddForce(Random.insideUnitSphere * 2f, ForceMode.Impulse);
        weaponRb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);

        Destroy(currentWeapon, 15f);
    }

    // --- EDITOR TOOLS ---
    [ContextMenu("Setup Ragdoll Now")]
    public void SetupRagdoll()
    {
        // Automatically find bones
        ragdollBodies = GetComponentsInChildren<Rigidbody>();

        // Find hips by common names
        foreach (Rigidbody rb in ragdollBodies)
        {
            string n = rb.name.ToLower();
            if (n.Contains("hip") || n.Contains("pelvis"))
            {
                impulseBody = rb;
                break; 
            }
        }
        
        // Find the main capsule collider on the root object
        CapsuleCollider mainCap = GetComponent<CapsuleCollider>();
        if (mainCap) gameplayColliders = new Collider[] { mainCap };

        Debug.Log($"<color=green>Ragdoll Setup Complete!</color> Found {ragdollBodies.Length} bones. Hips assigned: {(impulseBody != null ? impulseBody.name : "(none)")}");
    }
}