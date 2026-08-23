using Menu;
using Player.Input;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.LowLevelPhysics2D;
using UnityEngine.SceneManagement;

namespace Player
{
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Camera")]
        [Range(0.1f, 9f)][SerializeField] float sensitivity = .5f;
        [Tooltip("Limits vertical camera rotation.")]
        [Range(0f, 90f)][SerializeField] float yRotationLimit = 88f;
        
        [Header("Movement")]
        [SerializeField] public float moveSpeed = 5f;
        [SerializeField] private float jumpHeight = 1f;
        [SerializeField] private float airControl = 1f;
        
        [Header("Interact")]
        [SerializeField] public float interactDistance = 2f;

        private PlayerInput _playerInput;
        private CharacterController _cc;
        private Transform _cameraTransform;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _interactAction;

        private Vector2 rotation = Vector2.zero;
        private float verticalVelocity;
        private Vector3 horizontalVelocity;
        
        private Vector3 _carryOffset = new Vector3(.5f,0,.25f);
        private GameObject _leftHand;
        private GameObject _rightHand;
        
        private bool canInteract;
        private GameObject currentInteractable;

        private const float Gravity = -9.81f;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _cc = GetComponent<CharacterController>();
            _cameraTransform = transform.GetChild(0);

            _moveAction = _playerInput.actions["Player/Move"];
            _lookAction = _playerInput.actions["Player/Look"];
            _jumpAction = _playerInput.actions["Player/Jump"];
            _interactAction = _playerInput.actions["Player/Interact"];
            
            if (SceneManager.GetActiveScene().name != "Menu")
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                MenuManager.Instance.ShowHUD();
            }
        }

        private void OnEnable()
        {
            _jumpAction.performed += OnJump;
            _interactAction.performed += OnInteract; 
        }

        private void OnDisable()
        {
            _jumpAction.performed -= OnJump;
            _interactAction.performed -= OnInteract;
        }
        
        private void Update()
        {
            ProcessLook();
            ProcessMove();
            CheckCursor();
        }
        
        private void ProcessLook()
        {
            Vector2 lookInput = _lookAction.ReadValue<Vector2>();
            
            rotation.x += lookInput.x * sensitivity;
            rotation.y = Mathf.Clamp(rotation.y + lookInput.y * sensitivity, -yRotationLimit, yRotationLimit);
            var xQuat = Quaternion.AngleAxis(rotation.x, Vector3.up);
            var yQuat = Quaternion.AngleAxis(rotation.y, Vector3.left);

            _cameraTransform.localRotation = yQuat;
            transform.localRotation = xQuat;
        }

        private void ProcessMove()
        {
            Vector2 moveInput = _moveAction.ReadValue<Vector2>();
            Vector3 moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;
            
            if (_cc.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
                horizontalVelocity = moveDirection * moveSpeed;
            }
            else
            {
                verticalVelocity += Gravity * Time.deltaTime;
                var targetVelocity = moveDirection * moveSpeed;
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, airControl * Time.deltaTime);
            } 
            
            _cc.Move(new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z) * Time.deltaTime);
        }
        
        private void CheckCursor()
        {
            var isHit = Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out var hit, interactDistance);
            
            canInteract = isHit && hit.collider.CompareTag("Interactable") && hit.collider.transform.parent==null;
            currentInteractable = canInteract ? hit.collider.gameObject : null;
            
            MenuManager.Instance.SetCursorText(canInteract ? "E - Interact" : "");
        }

        /// <summary>Function for use when pressing a rebind button.</summary>
        public void StartRebind(string actionName, int bindingIndex)
        {
            InputManager.Instance.StartRebind(_playerInput.playerIndex, actionName, bindingIndex);
        }

        private void OnJump(InputAction.CallbackContext ctx)
        {
            if (!_cc.isGrounded) return;
            
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * Gravity);
        }

        private void OnInteract(InputAction.CallbackContext ctx)
        { 
            if (TryPickUp()) return;
            if (PutDown()) return;
            PutDown(false);
        }

        private bool TryPickUp()
        {
            if (!canInteract) return false;
            
            if (_leftHand==null)
            {
                _leftHand = currentInteractable;
            }
            else if (_rightHand == null)
            {
                _rightHand = currentInteractable;
            }
            else
            {
                return false;
            }
            
            currentInteractable.transform.parent = transform;
            currentInteractable.transform.localPosition = new Vector3(_leftHand==currentInteractable ? -_carryOffset.x : _carryOffset.x, _carryOffset.y, _carryOffset.z);
            var rb = currentInteractable.GetComponent<Rigidbody>().useGravity = false;
            currentInteractable.GetComponent<Collider>().enabled = false;
            return true;
        }

        private bool PutDown(bool left = true)
        {
            var target = left ? _leftHand : _rightHand;
            if (target==null) return false;
            
            target.transform.SetParent(null, true);
            var rb = target.GetComponent<Rigidbody>().useGravity = true;
            target.GetComponent<Collider>().enabled = true;

            if (left)
            {
                _leftHand = null;
            }
            else
            {
                _rightHand = null;
            }
            
            return true;
        }
    }
}