using UnityEngine;

namespace MyGame.Interface
{
    /// Player UI.
    public class CanvasSpawner : MonoBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Settings")]
        
        [Tooltip("Canvas prefab spawned at start. Displays the player's user interface.")]
        [SerializeField]
        private GameObject canvasPrefab;

        #endregion

        #region UNITY FUNCTIONS

        /// Awake.
        private void Awake()
        {
            //Spawn Interface.
            Instantiate(canvasPrefab);
        }

        #endregion
    }
}