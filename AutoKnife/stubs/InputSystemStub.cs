// Unity.InputSystem stub
namespace UnityEngine.InputSystem
{
    public class InputAction
    {
        public bool IsPressed() => false;
        public bool WasPressedThisFrame() => false;
        public bool WasReleasedThisFrame() => false;
    }

    public class InputActionAsset
    {
        public InputAction FindAction(string actionName, bool throwIfNotFound = false) => new InputAction();
    }

    public class PlayerInput : MonoBehaviour
    {
        public InputActionAsset actions { get; set; } = new InputActionAsset();
    }

    public class InputControl
    {
    }

    public class ButtonControl : InputControl
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
    }

    public class InputDevice : InputControl
    {
    }

    public class Mouse : InputDevice
    {
        public static Mouse current { get; } = new Mouse();
        public ButtonControl leftButton { get; } = new ButtonControl();
        public ButtonControl rightButton { get; } = new ButtonControl();
    }

    public class Keyboard : InputDevice
    {
        public static Keyboard current { get; } = new Keyboard();
    }
}
