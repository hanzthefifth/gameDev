using UnityEngine;

namespace Game
{

internal interface IMotionState
    {
        void Enter();
        void Exit();
        void Tick(Vector3 inputDir, float inputMagnitude, float forwardAmount, bool canSprint);
    }

    [System.Serializable]
    public struct MovementStats
    {
        [Header("Speeds")]
        public float speedWalking;
        public float speedSprinting;
        [Range(0f, 1.5f)] public float forwardSpeedMultiplier, backwardSpeedMultiplier, strafeSpeedMultiplier, sprintStrafeSpeedMultiplier;

        [Header("Acceleration")]
        public float groundAcceleration;  // how quickly player hits target speed on ground
        public float airAcceleration; //acceleration in air scale
    }

    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(AudioSource))]
    public class Movement : MovementBehaviour
    {
        [Header("Movement Stats")]
        [SerializeField] private MovementStats baseStats;

        [Header("Speeds")]
        [SerializeField] public float maxAirSpeed = 10.0f;

        [Header("Jumping")]
        [SerializeField] private float jumpHeight = 3.0f;            // target jump height in meters
        [SerializeField] private float baseGravityMultiplier = 1.6f; // gravity scale
        [SerializeField] private float fallGravityMultiplier = 5.0f; // extra gravity when falling
        [SerializeField] private float sprintJumpBoost = 1.1f; // extra boost when jumping out of a sprint
        [SerializeField] private float sprintJumpMinSpeed = 5.0f; // minimum ground speed to allow full sprint long jump

        [Header("Jump Rhythm")]
        [SerializeField] private float walkJumpLockoutTime = 0.35f;    // time after landing during which jumping is blocked when walking/idle
        [SerializeField] private float sprintJumpLockoutTime = 0.15f;  // shorter lockout when landing from a sprint jump
        [SerializeField] public float landingAccelPenaltyTime = 0.15f;  // time after landing during which ground accel is reduced

        [Header("Movement Physics")]
        [SerializeField] public float groundFriction = 8.0f;
        [SerializeField, Range(0f, 1f)] private float directionChangePenalty = 0.5f;
        //[SerializeField, Range(-1f, 1f)] private float sprintForwardCone = 0.5f;      // old cone for sprint directional multi
        [SerializeField, Range(-1f, 0.5f)] private float sprintBackwardCone = -0.1f;  // backward threshold to disable sprint entirely
       //[SerializeField] private float softBrakeDeceleration = 7.5f; // no input: settle

        [Header("Stamina (Movement)")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRegenPerSecond = 20f;
        [SerializeField] private float sprintStaminaDrainPerSecond = 10f;
        [SerializeField] private float jumpStaminaCost = 20f;
        [SerializeField, Range(0f, 1f)] private float lowStaminaJumpMultiplier = 0.6f; // how weak jumps are at 0 stamina
        [SerializeField] private float sprintMinStaminaToStart = 20f;     // min stam required to begin sprinting
        [SerializeField] private float sprintMinStaminaToContinue = 5f;   // min stam required to keep sprinting


        [Header("Slope Handling")]
        [SerializeField] private float maxStandableSlope = 45f;   // degrees
        //[SerializeField] private float steepSlopeExtraGravity = 0f; //extra force on steeper slopes

        [Header("Ground Check")]
        [SerializeField] private float groundCheckDistance = 0.2f;   // extra distance below capsule for ground skin
        [SerializeField] private LayerMask groundLayerMask = ~0;     // set to your "Ground" layer(s) in inspector

        [Header("Audio Clips")]
        [SerializeField] private AudioClip audioClipWalking;
        [SerializeField] private AudioClip audioClipRunning;

        [Header("Debug")]
        [SerializeField] private bool debugJumpInfo = false; // toggle detailed jump logs

        private Rigidbody rigidBody;
        private CapsuleCollider capsule;
        private AudioSource audioSource;
        public MovementStats currentStats;
        private IMotionState _currentState;

        // State instances (created once, reused)
        private GroundedState groundedState;
        private AirborneState airborneState;

        private bool jumpRequested, isGrounded, isSprinting;
        private bool wasGroundedLastFrame = false;
        private float groundSlopeAngle = 0f;
        private float lastLandTime = -999f;
        private float currentStamina;
        private CharacterBehaviour playerCharacter;
        private WeaponBehaviour equippedWeapon;
        private Vector3 groundNormal = Vector3.up;
        public event System.Action<float, float> OnStaminaChanged; // Event fired whenever stamina changes

        public float MaxStamina => maxStamina;
        public float CurrentStamina => currentStamina;
        public float NormalizedStamina => maxStamina > 0f ? currentStamina / maxStamina : 0f;
        public float LastLandTime => lastLandTime;

        public Vector3 Velocity
        {
            get => rigidBody.linearVelocity;
            set => rigidBody.linearVelocity = value;
        }

        protected override void Awake()
        {
            base.Awake();
            playerCharacter = ServiceLocator.Current.Get<IGameModeService>().GetPlayerCharacter();
            currentStamina = maxStamina;
            currentStats = baseStats;

            // Initialize state instances
            groundedState = new GroundedState(this);
            airborneState = new AirborneState(this);
            
            // Start in grounded state
            _currentState = groundedState;
            _currentState.Enter();
        }


        protected override void Start()
        {
            rigidBody = GetComponent<Rigidbody>();
            rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
            rigidBody.linearDamping = 0f; // allows handling of friction
            rigidBody.useGravity = false; // for custom gravity
            capsule = GetComponent<CapsuleCollider>();
            audioSource = GetComponent<AudioSource>();
            audioSource.clip = audioClipWalking;
            audioSource.loop = true;
            currentStamina = maxStamina; // start full stam
            currentStats = baseStats;
        }


        protected override void Update()
        {
            equippedWeapon = playerCharacter.GetInventory().GetEquipped();

            if (playerCharacter.GetInputJump())
                jumpRequested = true;

            PlayFootstepSounds();
            
        }

        // Helper method to change stamina and fire the event
        private void SetStamina(float newValue)
        {
            float oldValue = currentStamina;
            currentStamina = Mathf.Clamp(newValue, 0f, maxStamina);
            
            // Only fire event if stamina actually changed (avoid spam)
            if (Mathf.Abs(oldValue - currentStamina) > 0.01f)
            {
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }

        protected override void FixedUpdate()
        {
            UpdateGroundedStatus();

            float dt = Time.fixedDeltaTime;
            Vector2 moveInput = playerCharacter.GetInputMovement();

            // 1. Input
            Vector3 wishDir; 
            float inputMagnitude, forwardAmount;
            bool hasMoveInput;
            ComputeMovementInput(moveInput, out wishDir, out inputMagnitude, out forwardAmount, out hasMoveInput);

            // 2. Sprint + stamina management
            bool canSprint = HandleSprint(dt, hasMoveInput, forwardAmount);

            // 3. Jump handling (uses jumpRequested, stamina, lockout, etc.)
            Vector3 velocity = rigidBody.linearVelocity;
            HandleJump(canSprint, ref velocity);

            // 4. Gravity
            ApplyCustomGravity(ref velocity);
            rigidBody.linearVelocity = velocity;

            // 5. STATE TRANSITIONS - grounded vs airborne
            if (isGrounded && _currentState != groundedState)
            {
                SetState(groundedState);
            }
            else if (!isGrounded && _currentState != airborneState)
            {
                SetState(airborneState);
            }

            // 6. Execute current state movement logic
            _currentState.Tick(wishDir, inputMagnitude, forwardAmount, canSprint);
        }


         private void SetState(IMotionState next)
        {
            if (_currentState == next || next == null)
                return;

            _currentState.Exit();
            _currentState = next;
            _currentState.Enter();
        }


        private void ComputeMovementInput(Vector2 moveInput, out Vector3 wishDir, out float inputMagnitude, out float forwardAmount, out bool hasMoveInput)
        {
            hasMoveInput = moveInput.sqrMagnitude > 0.0001f;

            // Local-space wish direction from input
            wishDir = new Vector3(moveInput.x, 0f, moveInput.y);
            inputMagnitude = wishDir.magnitude;

            if (inputMagnitude > 0f)
            {
                inputMagnitude = Mathf.Clamp01(inputMagnitude);
                wishDir /= inputMagnitude; // normalize
            }

            // Transform from local (input) to world space
            wishDir = transform.TransformDirection(wishDir);

            // Forward amount (dot with character forward projected to XZ)
            Vector3 forward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            forwardAmount = (wishDir.sqrMagnitude > 0f) ? Vector3.Dot(wishDir, forward) : 0f;
        }


        private bool HandleSprint(float dt, bool hasMoveInput, float forwardAmount)
        {
            bool sprintInput = playerCharacter.IsRunning();

            // Directional requirement for sprint (no strong backward sprinting)
            bool wantsSprintDirection = sprintInput &&
                                        hasMoveInput &&
                                        isGrounded &&
                                        forwardAmount > sprintBackwardCone;

            // Update sprint state with stamina gating
            if (wantsSprintDirection)
            {
                if (isSprinting)
                {
                    // Already sprinting: can we keep going?
                    isSprinting = currentStamina > sprintMinStaminaToContinue;
                }
                else
                {
                    // Not sprinting yet: do we have enough stamina to start?
                    isSprinting = currentStamina > sprintMinStaminaToStart;
                }
            }
            else
            {
                isSprinting = false;
            }

            // Stamina section (drain when sprinting, regen when grounded and not trying to jump)
            float staminaDelta = 0f;

            if (isSprinting)
            {
                staminaDelta -= sprintStaminaDrainPerSecond * dt;
            }
            else if (isGrounded && !jumpRequested)
            {
                staminaDelta += staminaRegenPerSecond * dt;
            }

            SetStamina(currentStamina + staminaDelta);

            // This is what we pass to movement states so they can pick sprint vs walk speeds
            return isSprinting;
        }

        private void HandleJump(bool canSprint, ref Vector3 velocity)
        {
            // Different jump lockouts for walk vs sprint
            float effectiveUpwardGravity = Physics.gravity.y * baseGravityMultiplier;
            float effectiveJumpLockoutTime = canSprint ? sprintJumpLockoutTime : walkJumpLockoutTime;
            bool  inJumpLockout = isGrounded && (Time.time - lastLandTime) < effectiveJumpLockoutTime;
            
            if (!jumpRequested || !isGrounded || inJumpLockout) // Basic jump conditions
                return;

            if (currentStamina < 0.25f * jumpStaminaCost) // Exhaustion gate
            {
                jumpRequested = false; // too tired for a proper jump
                return;
            }

            // Vertical jump velocity to reach desired height
            float jumpVelocity = Mathf.Sqrt(2f * -effectiveUpwardGravity * jumpHeight);

            // Clear any downward velocity before jumping again
            if (velocity.y < 0f)
                velocity.y = 0f;

            velocity.y = jumpVelocity;

            // Horizontal long-jump / stamina-scaling logic
            Vector3 horiz = new Vector3(velocity.x, 0f, velocity.z);
            float horizSpeedBefore = horiz.magnitude;
            bool boostApplied = false;
            float now = Time.time;

            if (horizSpeedBefore > 0.01f)
            {
                Vector3 dir = horiz.normalized;
                float horizMultiplier = 1f;

                // Sprint long jump: requires sprint state AND enough horizontal speed
                if (canSprint && horizSpeedBefore >= sprintJumpMinSpeed)
                {
                    horizMultiplier *= sprintJumpBoost;
                    boostApplied = true;
                }

                // Stamina factor: at 0 stamina we scale by lowStaminaJumpMultiplier, at full stamina by 1.0
                float stamina01     = currentStamina / maxStamina;
                float staminaFactor = Mathf.Lerp(lowStaminaJumpMultiplier, 1f, stamina01);
                horizMultiplier    *= staminaFactor;

                float horizSpeedAfter = horizSpeedBefore * horizMultiplier;
                horiz = dir * horizSpeedAfter;
                velocity.x = horiz.x;
                velocity.z = horiz.z;

                if (debugJumpInfo)
                {
                    Debug.Log(
                        $"[Movement Jump] t={now:F3} " +
                        $"groundAccel={currentStats.groundAcceleration:F2}, canSprint={canSprint}, " +
                        $"horizBefore={horizSpeedBefore:F3}, horizAfter={horizSpeedAfter:F3}, " +
                        $"longJumpMin={sprintJumpMinSpeed:F3}, " +
                        $"boostApplied={boostApplied}, stamina={currentStamina:F1}, " +
                        $"lockoutTime={effectiveJumpLockoutTime:F3}"
                    );
                }
            }
            
            SetStamina(currentStamina - jumpStaminaCost);
            jumpRequested = false; // Clear jump request and mark as airborne
            isGrounded    = false;
        }




        /// Cal directional speed multiplier based on movement direction and sprint state.smooth blending between forward/back/strafe multipliers.
        public float CalcDirSpeedMultiplier(float forwardAmount, bool canSprint)
        { 
            float forwardBlend = (forwardAmount + 1f) / 2f; // Remap forwardAmount from [-1, 1] to [0, 1] for smooth blending
            
            // Blend between backward and forward multipliers based on how forward/backward you're moving
            float backwardToForwardMul = Mathf.Lerp(
                currentStats.backwardSpeedMultiplier,
                currentStats.forwardSpeedMultiplier,
                forwardBlend
            );
            
            // Now blend with strafe multiplier based on how much you're moving sideways
            // forwardAmount is near 0 use strafe multiplier,forwardAmount is near ±1 (pure forward/back), forward/back multiplier
            float strafeInfluence = 1f - Mathf.Abs(forwardAmount);  // 0 at pure forward/back, 1 at pure strafe
            
            float strafeMul = canSprint 
                ? currentStats.sprintStrafeSpeedMultiplier 
                : currentStats.strafeSpeedMultiplier;
            
            float dirSpeedMul = Mathf.Lerp(
                backwardToForwardMul,
                strafeMul,
                strafeInfluence
            );
            
            return dirSpeedMul;
        }
    
        /// Apply acceleration in a direction up to a target speed. direction change penalty for realistic turning.
        public void Accelerate(ref Vector3 velocity, Vector3 wishDir, float wishSpeed, float acceleration, float deltaTime)
        {
            if (wishSpeed <= 0f || wishDir.sqrMagnitude < 0.0001f)
                return;

            Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
            float currentSpeed = Vector3.Dot(horizontalVel, wishDir); // Speed in the direction we want to move
            float addSpeed = wishSpeed - currentSpeed;
            if (addSpeed <= 0f)
                return;

            float accelSpeed = acceleration * deltaTime * wishSpeed; //base acceleration amount
            float turnFactor = 1f;
             if (directionChangePenalty > 0f && horizontalVel.sqrMagnitude > 0.0001f)
            {
                Vector3 currentDir = horizontalVel.normalized;
                float alignment = Vector3.Dot(currentDir, wishDir); // -1 (opposite) to 1 (same)

                if (alignment < 0f)
                {
                    // Minimum factor when doing a full 180° turn. penalty=0 minfactor 1 (no effect), penalty=1 minfactor 0 (max effect)
                    float minFactor = Mathf.Clamp01(1f - directionChangePenalty);

                    //alignment
                    float t = Mathf.Clamp01(-alignment);

                    // Lerp from 1 (no turn) to minFactor (full opposite)
                    turnFactor = Mathf.Lerp(1f, minFactor, t);
                }
            }

            accelSpeed *= turnFactor;

            if (accelSpeed > addSpeed)
                accelSpeed = addSpeed;

            Vector3 delta = wishDir * accelSpeed;
            velocity.x += delta.x;
            velocity.z += delta.z;
        }

        /// Apply friction to ground movement, slowing down horizontal velocity.
        public void ApplyGroundFriction(ref Vector3 velocity, float friction, float deltaTime)
        {
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontal.magnitude;
            if (speed < 0.001f)
                return;

            float drop = speed * friction * deltaTime;
            float newSpeed = Mathf.Max(speed - drop, 0f);
            float ratio = newSpeed / speed;

            horizontal *= ratio;
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;
        }

        private void ApplyCustomGravity(ref Vector3 velocity)
        {
            if (isGrounded && groundSlopeAngle <= maxStandableSlope) //if on a standable slope, keep glued by removing velocity into the ground
            {
                if (velocity.y <= 0f) //only project if not moving upwards in Y, dont kill jump velocity if ground check says grounded briefly after jump
                {
                    //Anti-slope ram/jitter, remove velocty along the ground normal
                    Vector3 projected = Vector3.ProjectOnPlane(velocity, groundNormal);
                    velocity.x = projected.x;
                    velocity.y = projected.y;
                    velocity.z = projected.z;
                }
                return;
            }
            float gravityMult = baseGravityMultiplier; // Airborne or on a too-steep slope: apply custom gravity
            
            if (velocity.y < 0f) // Extra gravity when falling
            {
                gravityMult *= fallGravityMultiplier;
            }
            velocity += Physics.gravity * gravityMult * Time.fixedDeltaTime;
        }

        private void PlayFootstepSounds()
        {
            if (isGrounded && rigidBody.linearVelocity.sqrMagnitude > 0.1f)
            {
                audioSource.clip = isSprinting ? audioClipRunning : audioClipWalking;
                if (!audioSource.isPlaying)
                    audioSource.Play();
            }
            else if (audioSource.isPlaying)
            {
                audioSource.Pause();
            }
        }
        private void UpdateGroundedStatus()
        {
            Vector3 origin = transform.position + capsule.center;
            float castDistance = (capsule.height * 0.5f) - capsule.radius + groundCheckDistance;

            RaycastHit hit;
            bool hitGround = Physics.SphereCast(
                origin,
                capsule.radius * 0.95f,
                Vector3.down,
                out hit,
                castDistance,
                groundLayerMask,
                QueryTriggerInteraction.Ignore
            );
            bool previouslyGrounded = wasGroundedLastFrame;
            isGrounded = hitGround;

            if (hitGround)
            {
                groundNormal     = hit.normal;
                groundSlopeAngle = Vector3.Angle(groundNormal, Vector3.up);

                if (!previouslyGrounded)
                {
                    lastLandTime = Time.time;
                }
            }
            else
            {
                groundNormal     = Vector3.up;
                groundSlopeAngle = 90f;
            }

            wasGroundedLastFrame = isGrounded;
        }
    }
}