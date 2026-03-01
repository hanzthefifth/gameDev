using UnityEngine;
using UnityEngine.AI;

namespace EnemyAI.Complete
{
    public class WeaponSystem : MonoBehaviour
    {
        [Header("Weapon Stats")]
        [SerializeField] private float fireRate = 0.25f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float range = 25f;
        [SerializeField] private float baseAccuracy = 2f;
        [SerializeField] private bool usePredictiveAim = true;
        [SerializeField, Range(0f, 1f)] private float predictionConfidenceThreshold = 0.65f;
        [SerializeField, Min(0f)] private float lateralVelocityThreshold = 0.75f;
        [SerializeField, Min(0f)] private float maxLeadDistance = 4f;
        [SerializeField, Min(0f)] private float closeRangeLeadClampDistance = 8f;
        [SerializeField, Min(0f)] private float closeRangeMaxLead = 1.25f;
        [SerializeField, Min(0f)] private float enemyGunshotIntensity = 1f;
        [SerializeField, Min(0f)] private float enemyGunshotRange = 40f;

        [Header("Melee Attack")]
        [SerializeField] private bool hasMeleeAttack = true;
        [SerializeField] public float meleeRange = 2.2f;
        [SerializeField] private float meleeCooldown = 1.25f;
        [SerializeField] private float meleeDamage = 15f;
        [Tooltip("Force applied to the ragdoll on a melee hit. Projectile weapons don't need this " +
                 "since the projectile physics handles it.")]
        [SerializeField] private float meleeImpulseForce = 300f;

        [Header("Hit Settings")]
        [SerializeField] private LayerMask hitMask;

        [Header("References")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private SoundEmitter soundEmitter;

        private Transform ownerTransform;
        private NavMeshAgent agent;
        private float nextFireTime;
        private float nextMeleeTime;

        public void Initialize(Transform owner, NavMeshAgent agent)
        {
            this.ownerTransform = owner;
            this.agent = agent;

            if (firePoint == null)
            {
                GameObject fp = new GameObject("FirePoint");
                fp.transform.SetParent(owner, false);
                fp.transform.localPosition = new Vector3(0f, 1.6f, 0.5f);
                fp.transform.localRotation = Quaternion.identity;
                firePoint = fp.transform;

                Debug.LogWarning($"[WeaponSystem] No FirePoint assigned on {name}. " +
                                 "Created a default FirePoint at local (0,1.6,0.5).");
            }

            if (soundEmitter == null)
            {
                soundEmitter = GetComponent<SoundEmitter>();
                if (soundEmitter == null)
                {
                    soundEmitter = gameObject.AddComponent<SoundEmitter>();
                    Debug.Log($"[WeaponSystem] Added SoundEmitter component to {name}");
                }
            }
        }

        public void EngageMelee(Transform target)
        {
            if (!hasMeleeAttack || Time.time < nextMeleeTime) return;

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > meleeRange) return;

            IDamageable damageable = target.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(meleeDamage);
            }

            // Melee has no projectile, so we drive the ragdoll impulse manually.
            RagdollController ragdoll = target.GetComponentInParent<RagdollController>();
            if (ragdoll != null)
            {
                Vector3 direction = (target.position - transform.position).normalized;
                // Slight upward angle so the body lifts rather than sliding along the floor.
                direction = (direction + Vector3.up * 0.2f).normalized;
                ragdoll.ApplyImpact(direction * meleeImpulseForce, target.position, null);
            }

            nextMeleeTime = Time.time + meleeCooldown;
        }

        public void EngageTarget(PerceptionSystem.ThreatInfo threat)
        {
            if (Time.time < nextFireTime) return;
            if (threat == null) return;

            Transform target = threat.target;
            if (target == null) return;

            Vector3 targetPos = GetAimPoint(threat);
            if (!HasClearShot(target, targetPos)) return;

            Fire(targetPos);
            nextFireTime = Time.time + fireRate;
        }

        private Vector3 GetAimPoint(PerceptionSystem.ThreatInfo threat)
        {
            Vector3 defaultAimPoint = threat.lastSeenPosition + Vector3.up * 1.0f;

            if (!usePredictiveAim || threat.ConfidenceNow < predictionConfidenceThreshold)
                return defaultAimPoint;

            Vector3 toTarget = threat.lastSeenPosition - transform.position;
            float distanceToTarget = toTarget.magnitude;

            if (distanceToTarget > range)
                return defaultAimPoint;

            Vector3 lateralVelocity = Vector3.ProjectOnPlane(threat.estimatedVelocity, toTarget.normalized);
            if (lateralVelocity.magnitude < lateralVelocityThreshold)
                return defaultAimPoint;

            Vector3 leadOffset = threat.predictedPosition - threat.lastSeenPosition;
            float allowedLead = maxLeadDistance;

            if (distanceToTarget < closeRangeLeadClampDistance)
                allowedLead = Mathf.Min(allowedLead, closeRangeMaxLead);

            leadOffset = Vector3.ClampMagnitude(leadOffset, allowedLead);
            return threat.lastSeenPosition + leadOffset + Vector3.up * 1.0f;
        }

        private void Fire(Vector3 targetPosition)
        {
            Vector3 origin = firePoint != null
                ? firePoint.position
                : transform.position + Vector3.up * 1.5f;

            if (soundEmitter != null)
                soundEmitter.EmitGunshot(enemyGunshotIntensity, enemyGunshotRange);

            Vector3 direction = (targetPosition - origin).normalized;
            float currentSpread = baseAccuracy;

            if (agent != null && agent.enabled && agent.velocity.magnitude > 0.5f)
                currentSpread *= 2.5f;

            direction = ApplySpread(direction, currentSpread);

            if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask))
            {
                Transform hitRoot = hit.collider.transform.root;
                int layer = hit.collider.gameObject.layer;

                Debug.Log($"[WeaponSystem] Raycast HIT: {hit.collider.name} " +
                          $"(layer: {LayerMask.LayerToName(layer)}), root: {hitRoot.name}");

                if (ownerTransform != null && hitRoot == ownerTransform.root)
                    return;

                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    // Don't re-damage dead enemies.
                    if (damageable.IsDead) return;

                    damageable.TakeDamage(damage);

                    CombatAI targetAI = hit.collider.GetComponentInParent<CombatAI>();
                    if (targetAI != null)
                        targetAI.OnTakeDamage(damage, origin, ownerTransform);
                }
                else
                {
                    Debug.Log("[WeaponSystem] No IDamageable found for " + hit.collider.name);
                }
            }
            else
            {
                Debug.Log("[WeaponSystem] Raycast did NOT hit anything.");
            }
        }

        private Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
        {
            float yaw   = Random.Range(-spreadDegrees, spreadDegrees);
            float pitch = Random.Range(-spreadDegrees, spreadDegrees);
            return Quaternion.Euler(pitch, yaw, 0f) * direction;
        }

        private bool HasClearShot(Transform target, Vector3 targetPosition)
        {
            Vector3 origin = firePoint != null
                ? firePoint.position
                : transform.position + Vector3.up * 1.6f;

            Vector3 direction = (targetPosition - origin).normalized;
            float distance = Vector3.Distance(origin, targetPosition);

            if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, hitMask))
            {
                bool hitTarget = hit.collider.transform.root == target.root;
                return hitTarget;
            }

            return true;
        }
    }
}