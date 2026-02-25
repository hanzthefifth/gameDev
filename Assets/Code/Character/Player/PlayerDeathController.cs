using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MyGame
{
    public sealed class PlayerDeathController : MonoBehaviour
    {
        [Header("Action Maps")]
        [SerializeField] private string aliveMapName = "Player";
        [SerializeField] private string deadMapName  = "Dead";

        [Header("Optional")]
        [SerializeField] private bool disableEquippedWeaponComponent = true;

        private PlayerHealth playerHealth;
        private PlayerInput playerInput;
        private Movement movement;
        private Character character;
        private CameraLook cameraLook;

        private bool isDead;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            playerInput  = GetComponent<PlayerInput>();
            movement     = GetComponent<Movement>();
            character    = GetComponent<Character>();
            cameraLook = GetComponentInChildren<CameraLook>();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
                playerHealth.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.OnDeath -= HandleDeath;
        }

        private void HandleDeath()
        {
            if (isDead) return;
            isDead = true;

            // Stop gameplay scripts (optional but still good)
            if (movement != null) movement.enabled = false;

            if (character != null)
            {
                character.CancelReload();
                character.enabled = false;
            }

            if (disableEquippedWeaponComponent && character != null)
            {
                var inv = character.GetInventory();
                var equipped = inv != null ? inv.GetEquipped() : null;
                if (equipped != null) equipped.enabled = false;
            }

            // IMPORTANT: stop gameplay inputs by switching action map
            if (playerInput != null) playerInput.SwitchCurrentActionMap(deadMapName);
            if (cameraLook != null) cameraLook.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }


        // Wire this to Dead/Restart in PlayerInput UnityEvents
        public void OnRestart(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            if (!isDead) return;

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // Optional: if you ever respawn without reloading the scene
        public void SetAlive()
        {
            isDead = false;

            if (playerInput != null)
                playerInput.SwitchCurrentActionMap(aliveMapName);

            if (movement != null) movement.enabled = true;
            if (character != null) character.enabled = true;
            if (cameraLook != null) cameraLook.enabled = true;
        }
    }
}