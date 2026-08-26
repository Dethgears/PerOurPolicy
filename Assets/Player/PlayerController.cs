using Game.Pickup;
using Game.Shop;
using Menu;
using Player.Input;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerController : NetworkBehaviour
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

        [Header("Local-Only References")]
        [Tooltip("Disabled automatically on remote (non-owner) player instances.")]
        [SerializeField] private Behaviour[] localComponents;
        
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

        private Vector3 _carryOffset = new Vector3(.5f, 0, .25f);
        private NetworkObject _leftHand;
        private NetworkObject _rightHand;

        private bool canInteract;
        private GameObject currentInteractable;

        private const float Gravity = -9.81f;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _cc = GetComponent<CharacterController>();
            _cameraTransform = transform.GetChild(0);
        }

        public override void OnNetworkSpawn()
        {
            _moveAction = _playerInput.actions["Player/Move"];
            _lookAction = _playerInput.actions["Player/Look"];
            _jumpAction = _playerInput.actions["Player/Jump"];
            _interactAction = _playerInput.actions["Player/Interact"];

            if (!IsOwner)
            {
                _playerInput.enabled = false;
                foreach (var item in localComponents)
                {
                    if (item != null) item.enabled = false;
                }
                return;
            }

            _jumpAction.performed += OnJump;
            _interactAction.performed += OnInteract;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;
            _jumpAction.performed -= OnJump;
            _interactAction.performed -= OnInteract;
        }

        private void Update()
        {
            if (!IsOwner) return;

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
            // Define the layer mask to exclude the "Ignore Raycast" layer (Layer 2)
            int layerMask = ~(1 << 2);
            var isHit = Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out var hit, interactDistance, layerMask);
            
            canInteract = isHit && hit.collider.CompareTag("Interactable") && hit.collider.transform.parent != transform;
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

        // --- Interact / pickup: server-authoritative so hand-state can't desync between clients ---

        private void OnInteract(InputAction.CallbackContext ctx)
        {
            ulong targetId = 0;
            bool hasTarget = false;

            if (canInteract && currentInteractable != null &&
                currentInteractable.TryGetComponent<NetworkObject>(out var netObj))
            {
                targetId = netObj.NetworkObjectId;
                hasTarget = true;
            }
            
            RequestInteractServerRpc(targetId, hasTarget);
        }

        [ServerRpc]
        private void RequestInteractServerRpc(ulong targetNetworkObjectId, bool hasTarget)
        {
            NetworkObject target = null;
            if (hasTarget && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var netObj))
            {
                target = netObj;
            }

            if (target != null && !target.TryGetComponent<Pickup>(out var _))
            {
                // It's a button
                var shop = target.GetComponentInParent<Shop>();
                shop.OnButtonPressed();
                return;
            }
            
            if (TryPickUp(target)) return;
            if (PutDown()) return;
            PutDown(false);
        }

        // Runs on the server only (called from RequestInteractServerRpc).
        private bool TryPickUp(NetworkObject target)
        { 
            if (target == null) return false;
            if (_leftHand != null && _rightHand != null) return false;

            bool goesInLeftHand = _leftHand == null;
            if (goesInLeftHand) _leftHand = target;
            else _rightHand = target;
            
            target.TrySetParent(NetworkObject, false);
            target.transform.localPosition = new Vector3(goesInLeftHand ? -_carryOffset.x : _carryOffset.x, _carryOffset.y, _carryOffset.z);
            
            if (target.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
            if (target.TryGetComponent<Collider>(out var col)) col.isTrigger = true;
            //if (target.TryGetComponent<NetworkTransform>(out var nt)) nt.enabled = false;

            return true;
        }
        
        // Runs on the server only (called from RequestInteractServerRpc).
        private bool PutDown(bool left = true)
        {
            var target = left ? _leftHand : _rightHand;
            if (target == null) return false;

            target.TrySetParent((NetworkObject)null, true);

            if (target.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = false;
            if (target.TryGetComponent<Collider>(out var col)) col.isTrigger = false;
            //if (target.TryGetComponent<NetworkTransform>(out var nt)) nt.enabled = true;

            if (left) _leftHand = null;
            else _rightHand = null;

            return true;
        }
    }
}