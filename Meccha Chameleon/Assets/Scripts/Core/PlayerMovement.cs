using UnityEngine;

namespace Core
{
    /// <summary>
    /// A clean, robust 3D character movement system using Unity's CharacterController.
    /// Supports camera-relative movement, walking/running, smooth rotation, jumping, and gravity.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The camera transform used to determine movement direction. If null, will fallback to Camera.main.")]
        [SerializeField] private Transform _cameraTransform;
        
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _sprintSpeed = 7f;
        [SerializeField] private float _rotationSmoothTime = 0.1f;
        [SerializeField] private float _speedChangeRate = 10f; // Controls how fast the character accelerates/decelerates
        
        [Header("Air & Jump Settings")]
        [SerializeField] private float _jumpHeight = 1.5f;
        [SerializeField] private float _gravityMultiplier = 2f;
        [SerializeField] private float _airControlMultiplier = 0.5f; // Reduces horizontal movement speed while in the air
        [SerializeField] private float _terminalVelocity = 53.0f; // Max falling speed

        // Internal State
        private CharacterController _controller;
        private IPlayerInput _input;
        
        private float _verticalVelocity;
        private float _rotationVelocity;
        private float _currentSpeed;
        private const float GRAVITY = -9.81f;
        
        private bool _isGrounded;
        
        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<IPlayerInput>();
            
            // Auto-assign camera if not provided
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }
        
        private void Update()
        {
            if (_input == null)
            {
                Debug.LogWarning("No IPlayerInput component found on the player. Please add one (e.g., PlayerInputHandler).", this);
                return;
            }

            HandleGravityAndJump();
            HandleMovementAndRotation();
        }

        /// <summary>
        /// Handles grounded checks, gravity application, and jumping logic.
        /// </summary>
        private void HandleGravityAndJump()
        {
            // CharacterController.isGrounded can be jittery on uneven terrain.
            // We ensure stability by forcing a small downward velocity when grounded.
            _isGrounded = _controller.isGrounded;
            
            if (_isGrounded)
            {
                // Reset vertical velocity when grounded; keep a small negative value to stick to the ground smoothly
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Handle jump request
                if (_input.Jump)
                {
                    // Calculate required velocity to reach the desired jump height (v = sqrt(h * -2 * g))
                    _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * GRAVITY * _gravityMultiplier);
                }
            }
            else
            {
                // Apply gravity linearly when not grounded, capping at terminal velocity
                if (_verticalVelocity > -_terminalVelocity)
                {
                    _verticalVelocity += GRAVITY * _gravityMultiplier * Time.deltaTime;
                }
            }
        }

        /// <summary>
        /// Calculates camera-relative movement and smoothly rotates the character.
        /// </summary>
        private void HandleMovementAndRotation()
        {
            Vector2 moveInput = _input.Move;
            Vector3 inputDirection = new Vector3(moveInput.x, 0.0f, moveInput.y).normalized;

            // Determine target speed based on input magnitude and sprint state
            float targetSpeed = _input.Sprint ? _sprintSpeed : _walkSpeed;
            if (inputDirection == Vector3.zero) 
            {
                targetSpeed = 0f;
            }

            // Smoothly interpolate current horizontal speed for natural acceleration
            float speedOffset = 0.1f;
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _currentSpeed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * _speedChangeRate);
            }
            else
            {
                _currentSpeed = targetSpeed;
            }

            Vector3 moveDirection = Vector3.zero;

            // Only calculate rotation if we have actual movement input
            if (inputDirection.magnitude >= 0.1f)
            {
                // Calculate target angle based on input and camera's forward direction
                float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;
                
                if (_cameraTransform != null)
                {
                    targetAngle += _cameraTransform.eulerAngles.y;
                }
                
                // Smoothly rotate the character model
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _rotationVelocity, _rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, angle, 0.0f);

                // Convert angle back to a forward movement vector
                moveDirection = Quaternion.Euler(0.0f, targetAngle, 0.0f) * Vector3.forward;
            }

            // Apply air control modifier if not grounded
            float speedMultiplier = _isGrounded ? 1f : _airControlMultiplier;

            // Combine horizontal movement and vertical velocity
            Vector3 finalMovement = moveDirection.normalized * (_currentSpeed * speedMultiplier) + Vector3.up * _verticalVelocity;
            
            // Move character
            _controller.Move(finalMovement * Time.deltaTime);
        }
    }
}
