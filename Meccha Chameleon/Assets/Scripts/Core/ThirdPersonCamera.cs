using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Core.CameraSystem
{
    /// <summary>
    /// A robust Third-Person Orbit Follow Camera.
    /// Features smooth following, wall collision detection, zooming, and optional right-click orbit requirement.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The object the camera will follow (e.g., Player's Head or Center).")]
        [SerializeField] private Transform _target;
        
        [Header("Orbit Settings")]
        [Tooltip("If true, you must hold Right-Click to rotate the camera. Essential if you need a free mouse cursor for UI or color sampling!")]
        [SerializeField] private bool _requireRightClickToOrbit = true;
        [SerializeField] private Vector2 _mouseSensitivity = new Vector2(0.3f, 0.3f);
        
        [Header("Distance & Zoom")]
        [SerializeField] private float _distance = 5f;
        [SerializeField] private float _minDistance = 1.5f;
        [SerializeField] private float _maxDistance = 10f;
        [SerializeField] private float _zoomSpeed = 2f;
        
        [Header("Angle Limits")]
        [SerializeField] private float _minYAngle = -20f;
        [SerializeField] private float _maxYAngle = 80f;
        
        [Header("Smoothing")]
        [SerializeField] private float _rotationSmoothTime = 0.12f;
        [SerializeField] private float _positionSmoothTime = 0.1f;
        
        [Header("Collision Avoidance")]
        [Tooltip("Layers the camera will collide with to prevent clipping through walls.")]
        [SerializeField] private LayerMask _collisionMask = ~0; // ~0 means Everything
        [Tooltip("How far away from the wall the camera should stop.")]
        [SerializeField] private float _collisionOffset = 0.2f;

        // Internal State
        private float _currentX;
        private float _currentY;
        
        private Vector3 _currentRotation;
        private Vector3 _rotationSmoothVelocity;
        private Vector3 _positionSmoothVelocity;

        private void Start()
        {
            if (_target != null)
            {
                _currentX = transform.eulerAngles.y;
                _currentY = transform.eulerAngles.x;
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            HandleInput();
            
            // Clamp vertical angle to prevent flipping upside down
            _currentY = Mathf.Clamp(_currentY, _minYAngle, _maxYAngle);

            // 1. Calculate Smooth Rotation
            Vector3 targetRotation = new Vector3(_currentY, _currentX, 0f);
            _currentRotation = Vector3.SmoothDamp(_currentRotation, targetRotation, ref _rotationSmoothVelocity, _rotationSmoothTime);
            transform.eulerAngles = _currentRotation;

            // 2. Calculate Desired Position (behind the target)
            Vector3 offset = transform.forward * -_distance;
            Vector3 desiredPosition = _target.position + offset;

            // 3. Handle Wall Collisions
            desiredPosition = HandleCollision(desiredPosition);

            // 4. Apply Smooth Movement
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _positionSmoothVelocity, _positionSmoothTime);
        }

        private void HandleInput()
        {
            Vector2 lookInput = Vector2.zero;
            float scrollInput = 0f;
            bool isOrbiting = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                lookInput = Mouse.current.delta.ReadValue();
                scrollInput = Mouse.current.scroll.ReadValue().y;
                isOrbiting = Mouse.current.rightButton.isPressed;
            }
#else
            lookInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            scrollInput = Input.GetAxis("Mouse ScrollWheel");
            isOrbiting = Input.GetMouseButton(1);
#endif

            // Only apply rotation if requirement is met
            if (!_requireRightClickToOrbit || isOrbiting)
            {
                _currentX += lookInput.x * _mouseSensitivity.x;
                _currentY -= lookInput.y * _mouseSensitivity.y; // Invert Y axis
            }

            // Handle Scroll Zooming
            if (Mathf.Abs(scrollInput) > 0.01f)
            {
                // Scroll input can be large (+120 or -120), so we normalize it slightly depending on input system
                float normalizedScroll = Mathf.Clamp(scrollInput, -1f, 1f);
                _distance -= normalizedScroll * _zoomSpeed; 
                _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
            }
        }

        private Vector3 HandleCollision(Vector3 desiredPosition)
        {
            Vector3 direction = desiredPosition - _target.position;
            float distanceToDesired = direction.magnitude;

            // SphereCast from the target towards the camera to see if a wall blocks it
            if (Physics.SphereCast(_target.position, 0.2f, direction.normalized, out RaycastHit hit, distanceToDesired, _collisionMask))
            {
                // If we hit a wall, move the camera in front of the wall
                return _target.position + direction.normalized * (hit.distance - _collisionOffset);
            }

            return desiredPosition;
        }
    }
}
