using UnityEngine;

namespace Game.LowPolyShooterPack
{
    /// Handles locomotion when the character is on the ground.
    /// Manages ground friction, acceleration, and landing penalties.
    internal sealed class GroundedState : IMotionState
    {
        private readonly Movement _movement;

        public GroundedState(Movement movement)
        {
            _movement = movement;
        }

        public void Enter()
        {
            // Called when landing from airborne state
            // Landing time tracking is already handled in Movement.UpdateGroundedStatus()
        }

        public void Exit()
        {
            // Called when leaving the ground (jumping or falling)
        }
        
        /// Called once per FixedUpdate while grounded.
        /// Handles ground movement physics.
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
            
            // Check if we're in landing penalty period (brief reduced acceleration after landing)
            bool inLandingAccelPenalty = (Time.time - _movement.LastLandTime) < _movement.landingAccelPenaltyTime;

            // Apply ground friction
            _movement.ApplyGroundFriction(ref velocity, _movement.groundFriction, dt);

            // Apply acceleration if there's input
            if (inputMagnitude > 0.001f)
            {
                float accel = _movement.currentStats.groundAcceleration;
                
                // Reduce acceleration briefly after landing for heavier feel
                if (inLandingAccelPenalty)
                    accel *= 0.85f;

                _movement.Accelerate(ref velocity, wishDir, wishSpeed, accel, dt);
            }

            // Apply the modified velocity
            _movement.Velocity = velocity;
        }
    }
}