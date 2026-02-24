// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame
{
    /// Handles all the animation events that come from the weapon in the asset.
    public class WeaponAnimationEventHandler : MonoBehaviour
    {

        /// Equipped Weapon.
        private WeaponBehaviour weapon;


        private void Awake()
        {
            //Cache. We use this one to call things on the weapon later.
            weapon = GetComponent<WeaponBehaviour>();
        }


        /// Ejects a casing from this weapon. This function is called from an Animation Event.
        private void OnEjectCasing()
        {
            //Notify.
            if(weapon != null)
                weapon.EjectCasing();
        }
    }
}