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
        [SerializeField] private float baseAccuracy = 2f; // degrees of spread
        [SerializeField] private bool usePredictiveAim = true;
        [SerializeField, Range(0f, 1f)] private float predictionConfidenceThreshold = 0.65f; //predication gate for aiming lead
        [SerializeField, Min(0f)] private float lateralVelocityThreshold = 0.75f; //prevents leading when target is mostly moving towards/away or strafing too slow
        [SerializeField, Min(0f)] private float maxLeadDistance = 4f; //hard cap on lead offset to avoid wild shots
        [SerializeField, Min(0f)] private float closeRangeLeadClampDistance = 8f; //stops over leading with extra clamp when target is nearby
        [SerializeField, Min(0f)] private float closeRangeMaxLead = 1.25f;
        [SerializeField, Min(0f)] private float enemyGunshotIntensity = 1f;
        [SerializeField, Min(0f)] private float enemyGunshotRange= 40f; //stops over leading with extra clamp when target is nearby

        [Header("Melee Attack")]
        [SerializeField] private bool hasMeleeAttack = true;
        [SerializeField] public float meleeRange = 2.2f; //switched to public
        [SerializeField] private float meleeCooldown = 1.25f;
        [SerializeField] private float meleeDamage = 15f;

        [Header("Hit Settings")]
        [SerializeField] private LayerMask hitMask;


        [Header("References")]
        [SerializeField] private Transform firePoint;  // can be assigned in editor
        [SerializeField] private SoundEmitter soundEmitter;  // for gunshot sounds

        private Transform ownerTransform;
        private NavMeshAgent agent;
        private float nextFireTime;
        private float nextMeleeTime;

        public void Initialize(Transform owner, NavMeshAgent agent)
        {
            this.ownerTransform = owner;
            this.agent = agent;

            // If no firePoint is assigned in the inspector, create a default one
            if (firePoint == null)
            {
                GameObject fp = new GameObject("FirePoint");
                fp.transform.SetParent(owner, false);

                // Roughly chest height, slightly forward
                fp.transform.localPosition = new Vector3(0f, 1.6f, 0.5f);
                fp.transform.localRotation = Quaternion.identity;

                firePoint = fp.transform;

                Debug.LogWarning($"[WeaponSystem] No FirePoint assigned on {name}. " +
                                 "Created a default FirePoint at local (0,1.6,0.5).");
            }
            
            // Set up sound emitter if not assigned
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
            
            // Apply damage
            IDamageable damageable = target.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(meleeDamage);
            }
            
            nextMeleeTime = Time.time + meleeCooldown;
        }


        public void EngageTarget(PerceptionSystem.ThreatInfo threat)
        {
            if (Time.time < nextFireTime)
                return;

            if (threat == null)
                return;

            Transform target = threat.target;

            if (target == null)
                return;

            //Debug.Log($"[WeaponSystem] EngageTarget: target={target.name}, root={target.root.name}, pos={target.position}");
            Vector3 targetPos = GetAimPoint(threat);
            // NEW: Check if we have clear shot
            // ✅ Pass both target and targetPos
            if (!HasClearShot(target, targetPos))
            {
                //Debug.Log("[WeaponSystem] No clear shot - something is blocking LOS");
                return;
            }
            Fire(targetPos);
            nextFireTime = Time.time + fireRate;
        }

        private Vector3 GetAimPoint(PerceptionSystem.ThreatInfo threat)
        {
            Vector3 defaultAimPoint = threat.lastSeenPosition + Vector3.up * 1.0f;

            if (!usePredictiveAim || threat.ConfidenceNow < predictionConfidenceThreshold)
            {
                return defaultAimPoint;
            }

            Vector3 toTarget = threat.lastSeenPosition - transform.position;
            float distanceToTarget = toTarget.magnitude;

            if (distanceToTarget > range)
            {
                return defaultAimPoint;
            }

            Vector3 lateralVelocity = Vector3.ProjectOnPlane(threat.estimatedVelocity, toTarget.normalized);
            if (lateralVelocity.magnitude < lateralVelocityThreshold)
            {
                return defaultAimPoint;
            }

            Vector3 leadOffset = threat.predictedPosition - threat.lastSeenPosition;
            float allowedLead = maxLeadDistance;

            if (distanceToTarget < closeRangeLeadClampDistance)
            {
                allowedLead = Mathf.Min(allowedLead, closeRangeMaxLead);
            }

            leadOffset = Vector3.ClampMagnitude(leadOffset, allowedLead);
            return threat.lastSeenPosition + leadOffset + Vector3.up * 1.0f;
        }


        private void Fire(Vector3 targetPosition)
        {

            //Debug.Log("[WeaponSystem] Fire() called");
            // Fallback if firePoint is somehow still null
            Vector3 origin;
            if (firePoint != null)
            {
                origin = firePoint.position;
            }
            else
            {
                origin = transform.position + Vector3.up * 1.5f;
                //Debug.LogWarning($"[WeaponSystem] FirePoint is null on {name}, " + "using transform.position as origin.");
            }
            
            // Emit gunshot sound for nearby AI to hear
            if (soundEmitter != null)
            {
                soundEmitter.EmitGunshot(enemyGunshotIntensity, enemyGunshotRange);
            }
 
            float dist = Vector3.Distance(origin, targetPosition);
            //Debug.Log($"[WeaponSystem] origin={origin}, targetPos={targetPosition}, dist={dist}");
            Vector3 direction = (targetPosition - origin).normalized;
            // draw the ray so you can see it in Scene view
            //Debug.DrawRay(origin, direction * range, Color.red, 0.1f);
            float currentSpread = baseAccuracy;

            if (agent != null && agent.enabled && agent.velocity.magnitude > 0.5f) //extra spread while moving
            {
                currentSpread *= 2.5f; // Moving penalty
            }

            direction = ApplySpread(direction, currentSpread);

                //Check if we hit ourselves,  Use 'QueryTriggerInteraction.Ignore' if you have trigger colliders
                if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask))
                {
                    Transform hitRoot = hit.collider.transform.root;
                    int layer = hit.collider.gameObject.layer;

                    Debug.Log($"[WeaponSystem] Raycast HIT: {hit.collider.name} " +
                    $"(layer: {LayerMask.LayerToName(layer)}), root: {hitRoot.name}");
                    
                    // 3. Self-hit guard (enemy shooting itself)
                        if (ownerTransform != null && hitRoot == ownerTransform.root)
                        {
                            //Debug.Log("[WeaponSystem] Ignoring self-hit on " + hit.collider.name);
                            return;
                        }

                    IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        //Debug.Log("[WeaponSystem] Calling TakeDamage on " + hitRoot.name);
                        damageable.TakeDamage(damage);
                        
                        // If hit target has a CombatAI, inform them they were hit and who hit them
                        CombatAI targetAI = hit.collider.GetComponentInParent<CombatAI>();
                        if (targetAI != null)
                        {
                            targetAI.OnTakeDamage(damage, origin, ownerTransform);
                        }
                    }
                    else
                    {
                         Debug.Log("[WeaponSystem] No IDamageable found for " + hit.collider.name);
                    }
                    //need muzzle flash and fx here
                }
                else
                {
                    Debug.Log("[WeaponSystem] Raycast did NOT hit anything.");
                }
        }      

        private Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
        {
            float yaw = Random.Range(-spreadDegrees, spreadDegrees);
            float pitch = Random.Range(-spreadDegrees, spreadDegrees);
            Quaternion spread = Quaternion.Euler(pitch, yaw, 0f);
            return spread * direction;
        }



        private bool HasClearShot(Transform target, Vector3 targetPosition)
        {
            Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.6f;
            Vector3 direction = (targetPosition - origin).normalized;
            float distance = Vector3.Distance(origin, targetPosition);
            
            if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, hitMask))
            {
                // Check if we hit the target or something else
                Transform hitRoot = hit.collider.transform.root;
                bool hitTarget = hitRoot == target.root; // ✅ Now we have access to target
                
                if (!hitTarget)
                {
                    //Debug.Log($"[WeaponSystem] LOS blocked by {hit.collider.name}");
                }
                
                return hitTarget;
            }
            
            return true; // Nothing blocking, clear shot
        }
    }
}