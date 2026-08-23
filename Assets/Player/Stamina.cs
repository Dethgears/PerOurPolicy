using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Player
{
    public class Stamina : MonoBehaviour
    {
        private CharacterController characterController;
        private PlayerController playerController;
        private PlayerInput playerInput;

        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction sprintAction;

        private float normalSpeed;

        public float CurrentStamina { get; private set; }

        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float jumpDrain = 3f;
        [SerializeField] private float sprintDrain = 3.5f;
        [SerializeField] private float staminaRegen = 2.5f;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            playerController = GetComponent<PlayerController>();
            playerInput = GetComponent<PlayerInput>();

            moveAction = playerInput.actions["Player/Move"];
            jumpAction = playerInput.actions["Player/Jump"];
            sprintAction = playerInput.actions["Player/Sprint"];

            CurrentStamina = maxStamina;
            normalSpeed = playerController.moveSpeed;
        }

        private void Update()
        {
            Vector2 moveInput = moveAction.ReadValue<Vector2>();

            bool isMoving = moveInput.sqrMagnitude > 0.01f;
            bool sprintPressed = sprintAction.IsPressed();
            bool jumpPressed = jumpAction.WasPressedThisFrame();

            bool isSprinting =
                sprintPressed &&
                isMoving &&
                CurrentStamina > 0;

            bool actuallyJumped =
                jumpPressed &&
                characterController.isGrounded &&
                CurrentStamina >= jumpDrain;

            playerController.moveSpeed =
                isSprinting ? normalSpeed*sprintMultiplier : normalSpeed;

            if (actuallyJumped)
            {
                CurrentStamina -= jumpDrain;
            }

            if (isSprinting)
            {
                CurrentStamina -= sprintDrain * Time.deltaTime;
            }

            if (!isMoving)
            {
                CurrentStamina += staminaRegen * Time.deltaTime;
            }

            CurrentStamina =
                Mathf.Clamp(CurrentStamina, 0, maxStamina);
        }

        private void OnDisable()
        {
            playerController.moveSpeed = normalSpeed;
        }
    }
}