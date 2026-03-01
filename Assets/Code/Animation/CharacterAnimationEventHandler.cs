// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame
{
	
	/// Handles all the animation events that come from the character in the asset.
	
	public class CharacterAnimationEventHandler : MonoBehaviour
	{
		#region FIELDS

		
        /// Character Component Reference.
        
        private CharacterBehaviour playerCharacter;

		#endregion

		#region UNITY

		private void Awake()
		{
			//Grab a reference to the character component.
			playerCharacter = ServiceLocator.Current.Get<IGameModeService>().GetPlayerCharacter();
		}

		#endregion

		#region ANIMATION

		
		/// Ejects a casing from the character's equipped weapon. This function is called from an Animation Event.
		private void OnEjectCasing()
		{
			//Notify the character.
			if(playerCharacter != null)
				playerCharacter.EjectCasing();
		}

		
		/// Fills character's equipped weapon's ammunition by a certain amount, or fully if set to 0. This function is called
		/// from a Animation Event.
		private void OnAmmunitionFill(int amount = 0)
		{
			//Notify the character.
			if(playerCharacter != null)
				playerCharacter.FillAmmunition(amount);
		}
		
		/// Sets the character's knife active value. This function is called from an Animation Event.
		private void OnSetActiveKnife(int active)
		{
		}
		
		/// Spawns a grenade at the correct location. This function is called from an Animation Event.
		private void OnGrenade()
		{
		}

		/// Sets the equipped weapon's magazine to be active or inactive! This function is called from an Animation Event.
		private void OnSetActiveMagazine(int active)
		{
			//Notify the character.
			if(playerCharacter != null)
				playerCharacter.SetActiveMagazine(active);
		}

		
		/// Bolt Animation Ended. This function is called from an Animation Event.
		private void OnAnimationEndedBolt()
		{
		}
		
		/// Reload Animation Ended. This function is called from an Animation Event.
		
		private void OnAnimationEndedReload()
		{
			//Notify the character.
			if(playerCharacter != null)
				playerCharacter.AnimationEndedReload();
		}

		
		/// Grenade Throw Animation Ended. This function is called from an Animation Event.
		private void OnAnimationEndedGrenadeThrow()
		{
		}
		
		/// Melee Animation Ended. This function is called from an Animation Event.
		private void OnAnimationEndedMelee()
		{
			if(playerCharacter != null)
				playerCharacter.AnimationEndedMelee();
		}

		/// Melee hit frame reached. This function is called from an Animation Event.
		/// Place this event at the impact frame of your swing clip.
		private void OnMeleeHit()
		{
			if(playerCharacter != null)
				playerCharacter.OnMeleeHit();
		}

		
		/// Inspect Animation Ended. This function is called from an Animation Event.
		private void OnAnimationEndedInspect()
		{
			//Notify the character.
			if(playerCharacter != null)
				playerCharacter.AnimationEndedInspect();
		}
		
		/// Holster Animation Ended. This function is called from an Animation Event.
		private void OnAnimationEndedHolster()
		{
			//Notify the character.
			if(playerCharacter != null)
				playerCharacter.AnimationEndedHolster();
		}

		
		/// Sets the character's equipped weapon's slide back pose. This function is called from an Animation Event.
		private void OnSlideBack(int back)
		{
		}

		#endregion
	}   
}