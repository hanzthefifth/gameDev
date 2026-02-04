using UnityEngine;

namespace Game.LowPolyShooterPack
{
    /// <summary>
    /// Handles locomotion when the character is in the air (jumping/falling).
    /// Manages air control, speed clamping, and reduced acceleration.
    /// </summary>
    internal sealed class AirborneState : IMotionState
    {
        private readonly Movement _movement;

        public AirborneState(Movement movement)
        {
            _movement = movement;
        }

        public void Enter()
        {
            // Called when leaving the ground (jump or fall)
            // Could add: play jump sound, start air timer, etc.
        }

        public void Exit()
        {
            // Called when landing
            // Landing effects are typically handled in GroundedState.Enter()
        }

        /// <summary>
        /// Called once per FixedUpdate while airborne.
        /// Handles air movement physics with reduced control and speed capping.
        /// </summary>
        public void Tick(Vector3 wishDir, float inputMagnitude, float forwardAmount, bool canSprint)
        {
            // Calculate directional speed multiplier with smooth blending
            float dirSpeedMul = _movement.CalcDirSpeedMultiplier(forwardAmount, canSprint);
            
            // Calculate final wish speed
            float baseSpeed = canSprint ? _movement.currentStats.speedSprinting : _movement.currentStats.speedWalking;
            float wishSpeed = baseSpeed * inputMagnitude * dirSpeedMul;

            // Get current velocity
            Vector3 velocity = _movement.Velocity;
            float dt = Time.fixedDeltaTime;

            // Air movement has capped speed to prevent excessive bunnyhopping
            float airWishSpeed = Mathf.Min(wishSpeed, _movement.maxAirSpeed);
            
            // Apply air acceleration (typically weaker than ground)
            _movement.Accelerate(ref velocity, wishDir, airWishSpeed, _movement.currentStats.airAcceleration, dt);

            // Enforce maximum horizontal speed cap
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float hSpeed = horizontal.magnitude;
            
            if (hSpeed > _movement.maxAirSpeed)
            {
                // Clamp horizontal speed to maxAirSpeed
                horizontal = horizontal.normalized * _movement.maxAirSpeed;
                velocity.x = horizontal.x;
                velocity.z = horizontal.z;
            }

            // Apply the modified velocity
            _movement.Velocity = velocity;
        }
    }
}