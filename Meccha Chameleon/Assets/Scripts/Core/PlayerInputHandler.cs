using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Core
{
    /// <summary>
    /// A drop-in input provider that implements IPlayerInput.
    /// It automatically uses the New Input System if it's installed, otherwise falls back to the old Input System.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour, IPlayerInput
    {
        public Vector2 Move { get; private set; }
        public bool Jump { get; private set; }
        public bool Sprint { get; private set; }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                // Simple WASD polling using the new Input System (No InputActions Asset required)
                Vector2 moveInput = Vector2.zero;
                if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
                if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
                if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
                if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
                Move = moveInput.normalized;

                Jump = Keyboard.current.spaceKey.wasPressedThisFrame;
                Sprint = Keyboard.current.leftShiftKey.isPressed;
            }
#else
            // Fallback to legacy input system
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Move = new Vector2(h, v).normalized;
            Jump = Input.GetButtonDown("Jump");
            Sprint = Input.GetKey(KeyCode.LeftShift);
#endif
        }
    }
}
