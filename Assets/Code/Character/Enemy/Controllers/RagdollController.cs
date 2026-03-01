using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class RagdollController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navMeshAgent;

    [Header("Physics Setup")]
    [Tooltip("The Hips/Pelvis Rigidbody. Root for velocity inheritance and fallback impulse target.")]
    [SerializeField] private Rigidbody hipsBody;
    [Tooltip("All Rigidbodies in the bone hierarchy. Auto-populated by Setup Ragdoll.")]
    [SerializeField] private Rigidbody[] ragdollBodies;

    [Header("Mass Configuration")]
    [Tooltip("Target total mass for the entire ragdoll in kg. ~70 for a human. " +
             "All bone masses are rescaled proportionally to hit this total. " +
             "Low total mass = tiny forces cause huge velocity = juggling.")]
    [SerializeField] private float targetTotalMass = 70f;
    [Tooltip("Disable only if you've manually set all bone masses to realistic values already.")]
    [SerializeField] private bool autoNormalizeMass = true;

    [Header("Velocity Inheritance")]
    [Tooltip("How much of the NavAgent's movement velocity carries into the hips on death. " +
             "0.5–0.8 feels natural. 0 = body drops straight down.")]
    [SerializeField] private float inheritVelocityMultiplier = 0.6f;

    [Header("Bullet Hit")]
    [Tooltip("Hard cap on any bone's speed after an impulse. Prevents juggling. 2–4 m/s is realistic.")]
    [SerializeField] private float maxRagdollSpeed = 3f;
    [Tooltip("Minimum seconds between impulses on a dead body. " +
             "Stops rapid fire stacking velocity before the speed clamp can clean it up.")]
    [SerializeField] private float impulseRechargeCooldown = 0.35f;

    [Header("Transition")]
    [Tooltip("Keeps the Animator running for a few physics ticks so the skeleton blends " +
             "smoothly into ragdoll rather than snapping instantly.")]
    [SerializeField] private bool useBlendedTransition = true;
    [Tooltip("Physics ticks to run the Animator before handing off to ragdoll. 2 is a good default.")]
    [SerializeField] private int blendFrames = 2;

    [Header("External Colliders")]
    [Tooltip("Gameplay capsule collider(s) to disable on death so the body can fall freely.")]
    [SerializeField] private Collider[] gameplayColliders;

    [Header("Weapon Drop (Optional)")]
    [SerializeField] private GameObject currentWeapon;

    // -------------------------------------------------------------------------
    private struct RigidbodyState
    {
        public bool isKinematic;
        public bool useGravity;
        public float mass;
        public RigidbodyInterpolation interpolation;
    }
    private RigidbodyState[] savedStates;

    private bool      hasPendingHit;
    private Vector3   pendingImpulse;
    private Vector3   pendingHitPoint;
    private Rigidbody pendingHitBody;

    private bool  isRagdollActive;
    private float lastImpulseTime = -999f;

    // -------------------------------------------------------------------------
    private void Awake()
    {
        if (ragdollBodies == null || ragdollBodies.Length == 0)
            ragdollBodies = GetComponentsInChildren<Rigidbody>();

        savedStates = new RigidbodyState[ragdollBodies.Length];
        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            var rb = ragdollBodies[i];
            savedStates[i] = new RigidbodyState
            {
                isKinematic   = rb.isKinematic,
                useGravity    = rb.useGravity,
                mass          = rb.mass,
                interpolation = rb.interpolation
            };
        }

        if (autoNormalizeMass)
            NormalizeMasses();

        SetRagdollPhysicsActive(false);
    }

    private void NormalizeMasses()
    {
        float currentTotal = 0f;
        foreach (var rb in ragdollBodies)
            if (rb) currentTotal += rb.mass;

        if (currentTotal <= 0f) return;

        float scale = targetTotalMass / currentTotal;
        foreach (var rb in ragdollBodies)
            if (rb) rb.mass *= scale;

        Debug.Log($"[RagdollController] {name}: masses rescaled {currentTotal:F1}kg → {targetTotalMass:F1}kg (x{scale:F2})");
    }

    // -------------------------------------------------------------------------
    public void EnableRagdoll()
    {
        if (isRagdollActive) return;
        isRagdollActive = true;

        Vector3 inheritedVelocity = Vector3.zero;
        if (navMeshAgent != null && navMeshAgent.enabled)
            inheritedVelocity = navMeshAgent.velocity;

        if (navMeshAgent != null) navMeshAgent.enabled = false;

        if (gameplayColliders != null)
            foreach (var col in gameplayColliders)
                if (col) col.enabled = false;

        HandleWeaponDrop();

        if (useBlendedTransition)
            StartCoroutine(BlendedActivation(inheritedVelocity));
        else
            ActivatePhysicsNow(inheritedVelocity);
    }

    private IEnumerator BlendedActivation(Vector3 inheritedVelocity)
    {
        SetRagdollPhysicsActive(true);

        for (int i = 0; i < blendFrames; i++)
            yield return new WaitForFixedUpdate();

        if (animator) animator.enabled = false;

        InheritVelocity(inheritedVelocity);
        FlushPendingHit();
    }

    private void ActivatePhysicsNow(Vector3 inheritedVelocity)
    {
        if (animator) animator.enabled = false;
        SetRagdollPhysicsActive(true);
        InheritVelocity(inheritedVelocity);
        FlushPendingHit();
    }

    private void InheritVelocity(Vector3 velocity)
    {
        if (inheritVelocityMultiplier <= 0f || velocity.sqrMagnitude < 0.0001f || hipsBody == null)
            return;

        hipsBody.linearVelocity = velocity * inheritVelocityMultiplier;
    }

    private void FlushPendingHit()
    {
        if (!hasPendingHit) return;
        hasPendingHit = false;
        ApplyImpulseInternal(pendingImpulse, pendingHitPoint, pendingHitBody);
    }

    // -------------------------------------------------------------------------
    // impulse = weapon's (playerCamera.forward * impactForce), passed raw.
    // Magnitude comes directly from the weapon's impactForce — tune that per
    // weapon and the ragdoll reacts accordingly. maxRagdollSpeed prevents juggling.
    // -------------------------------------------------------------------------
    public void ApplyImpact(Vector3 impulse, Vector3 worldPoint, Rigidbody hitBody = null)
    {
        if (impulse.sqrMagnitude < 0.0001f) return;
        if (Time.time - lastImpulseTime < impulseRechargeCooldown) return;

        if (!isRagdollActive)
        {
            hasPendingHit    = true;
            pendingImpulse   = impulse;
            pendingHitPoint  = worldPoint;
            pendingHitBody   = hitBody;
            return;
        }

        ApplyImpulseInternal(impulse, worldPoint, hitBody);
    }

    private void ApplyImpulseInternal(Vector3 impulse, Vector3 worldPoint, Rigidbody hitBody)
    {
        lastImpulseTime = Time.time;

        Rigidbody rb = hitBody != null ? hitBody : hipsBody;
        if (rb == null) return;

        Physics.SyncTransforms();
        rb.AddForceAtPosition(impulse, worldPoint, ForceMode.Impulse);

        ClampRagdollVelocity();
    }

    private void ClampRagdollVelocity()
    {
        if (maxRagdollSpeed <= 0f) return;

        foreach (var rb in ragdollBodies)
        {
            if (rb == null || rb.isKinematic) continue;

            float speed = rb.linearVelocity.magnitude;
            if (speed > maxRagdollSpeed)
                rb.linearVelocity = rb.linearVelocity * (maxRagdollSpeed / speed);
        }
    }

    // -------------------------------------------------------------------------
    public void DisableRagdoll()
    {
        isRagdollActive = false;
        SetRagdollPhysicsActive(false);

        if (animator)     animator.enabled     = true;
        if (navMeshAgent) navMeshAgent.enabled = true;

        if (gameplayColliders != null)
            foreach (var col in gameplayColliders)
                if (col) col.enabled = true;
    }

    private void SetRagdollPhysicsActive(bool active)
    {
        for (int i = 0; i < ragdollBodies.Length; i++)
        {
            Rigidbody rb = ragdollBodies[i];
            if (!rb) continue;

            rb.isKinematic   = !active;
            rb.useGravity    = active;
            rb.interpolation = active
                ? RigidbodyInterpolation.Interpolate
                : RigidbodyInterpolation.None;

            if (!active)
            {
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void HandleWeaponDrop()
    {
        if (currentWeapon == null) return;

        currentWeapon.transform.SetParent(null);

        Rigidbody weaponRb = currentWeapon.GetComponent<Rigidbody>()
                          ?? currentWeapon.AddComponent<Rigidbody>();
        Collider weaponCol = currentWeapon.GetComponent<Collider>()
                          ?? currentWeapon.AddComponent<BoxCollider>();

        weaponRb.isKinematic = false;
        weaponRb.useGravity  = true;
        weaponCol.enabled    = true;

        weaponRb.AddForce(Random.insideUnitSphere * 2f, ForceMode.Impulse);
        weaponRb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);

        Destroy(currentWeapon, 15f);
    }

    // -------------------------------------------------------------------------
    [ContextMenu("Setup Ragdoll Now")]
    public void SetupRagdoll()
    {
        ragdollBodies = GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in ragdollBodies)
        {
            string n = rb.name.ToLower();
            if (n.Contains("hip") || n.Contains("pelvis"))
            {
                hipsBody = rb;
                break;
            }
        }

        CapsuleCollider mainCap = GetComponent<CapsuleCollider>();
        if (mainCap) gameplayColliders = new Collider[] { mainCap };

        Debug.Log($"<color=green>Ragdoll Setup Complete!</color> " +
                  $"Found {ragdollBodies.Length} bones. " +
                  $"Hips: {(hipsBody != null ? hipsBody.name : "(none)")}");
    }
}