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
}
