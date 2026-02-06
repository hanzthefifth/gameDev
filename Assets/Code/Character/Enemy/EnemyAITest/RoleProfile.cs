using UnityEngine;
using System.Collections.Generic;

namespace EnemyAI.Complete
{
    
     // ========================================================================
    // ROLE PROFILE - Defines enemy personality/behavior
    // ========================================================================
    
    public class RoleProfile : MonoBehaviour
    {
        public enum Role { Aggressive, Balanced, Defensive, Sniper }
        
        [SerializeField] private Role role = Role.Balanced;
        [Header("Capabilities")]
        // [SerializeField] private bool hasMeleeAttack = true;
        // [SerializeField] private bool hasRangedAttack = true;
        // [SerializeField] private bool canReposition = true;

        
        public float PreferredRange { get; private set; }
        public float RepositionFrequency { get; private set; }
        // public bool CanMelee => hasMeleeAttack;
        // public bool CanShoot => hasRangedAttack;
        // public bool CanReposition => canReposition;
        
        private void Awake()
        {
            ApplyRole();
        }
        
        private void ApplyRole()
        {
            switch (role)
            {
                case Role.Aggressive:
                    PreferredRange = 8f;
                    RepositionFrequency = 2f;
                    break;
                
                case Role.Balanced:
                    PreferredRange = 12f;
                    RepositionFrequency = 3f;
                    break;
                
                case Role.Defensive:
                    PreferredRange = 15f;
                    RepositionFrequency = 5f;
                    break;
                
                case Role.Sniper:
                    PreferredRange = 20f;
                    RepositionFrequency = 8f;
                    break;
            }
        }
    }
}