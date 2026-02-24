// Copyright 2021, Infima Games. All Rights Reserved.

using UnityEngine;

namespace MyGame
{
    
    /// Array Utilities.
    
    public static class UtilitiesArrays
    {
        
        /// Returns true if the array contains this index.
        
        public static bool IsValidIndex<T>(this T[] array, int index) => array.Length > index && index >= 0;
        
        /// Returns true if the array is valid.
        
        public static bool IsValid<T>(this T[] array) => !array.Equals(null) && array.Length > 0;
        
        /// Returns a random audio clip from an array of clips.
        
        public static T GetRandom<T>(this T[] array) => array[Random.Range(0, array.Length)];
    }
}