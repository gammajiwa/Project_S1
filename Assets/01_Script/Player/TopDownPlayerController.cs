using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectS1.Gameplay
{
    /// <summary>
    /// Camera-relative top-down locomotion driven by the project's InputSystem_Actions asset.
    /// Expects a PlayerInput component using the "Player" action map with
    /// Behavior = Send Messages (OnMove / OnSprint are invoked by reflection).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TopDownPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _sprintMultiplier = 1.6f;
        [SerializeField] private float _acceleration = 40f;
        [SerializeField] private float _rotationSpeed = 900f;

        [Header("Gravity")]
        [SerializeField] private float _gravity = -25f;
        [SerializeField] private float _groundedStick = -2f;

        [Header("Camera")]
        [Tooltip("Movement is relative to this transform's yaw. Falls back to Camera.main.")]
        [SerializeField] private Transform _cameraTransform;

        private CharacterController _controller;
        private Vector2 _moveInput;
        private bool _sprinting;
        private Vector3 _planarVelocity;
        private float _verticalVelocity;

        /// <summary>Current horizontal velocity — read by animation and VFX systems.</summary>
        public Vector3 PlanarVelocity => _planarVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        // --- PlayerInput: Send Messages ---

        private void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
        }

        private void OnSprint(InputValue value)
        {
            _sprinting = value.isPressed;
        }

        private void Update()
        {
            float targetSpeed = _moveSpeed * (_sprinting ? _sprintMultiplier : 1f);
            Vector3 desired = ResolveMoveDirection() * targetSpeed;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, desired, _acceleration * Time.deltaTime);

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = _groundedStick;
            }
            else
            {
                _verticalVelocity += _gravity * Time.deltaTime;
            }

            Vector3 motion = _planarVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            FaceMoveDirection();
        }

        /// <summary>Projects raw stick/WASD input onto the camera's yaw so "up" is always screen-up.</summary>
        private Vector3 ResolveMoveDirection()
        {
            Vector3 input = new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            if (_cameraTransform == null)
            {
                return input;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return forward * input.z + right * input.x;
        }

        private void FaceMoveDirection()
        {
            Vector3 flat = new Vector3(_planarVelocity.x, 0f, _planarVelocity.z);
            if (flat.sqrMagnitude < 0.01f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _rotationSpeed * Time.deltaTime);
        }
    }
}
