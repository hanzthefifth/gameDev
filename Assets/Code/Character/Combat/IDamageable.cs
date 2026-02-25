using UnityEngine;

public interface IDamageable
{
    /// Basic damage with no physics context. Force defaults to zero.
    void TakeDamage(float amount);

    /// Damage with hit context so ragdolls receive correct impulse direction and position.
    void TakeDamage(float amount, Vector3 force, Vector3 hitPoint, Rigidbody hitBody = null);

    bool IsDead { get; }
}