// Stub assemblies for Lethal Company V81 mod compilation
// These provide the minimal type definitions needed to compile the mod

using System;
using UnityEngine;

// ============================================================
// BepInEx Stubs
// ============================================================
namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class BepInPlugin : Attribute
    {
        public string GUID { get; }
        public string Name { get; }
        public string Version { get; }
        public BepInPlugin(string guid, string name, string version)
        {
            GUID = guid;
            Name = name;
            Version = version;
        }
    }

    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        private Logging.ManualLogSource _logger;
        public Logging.ManualLogSource Logger
        {
            get
            {
                if (_logger == null)
                    _logger = Logging.Logger.CreateLogSource("Stub");
                return _logger;
            }
        }
    }
}

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public void LogInfo(object data) { }
        public void LogError(object data) { }
        public void LogWarning(object data) { }
        public void LogDebug(object data) { }
    }

    public static class Logger
    {
        public static ManualLogSource CreateLogSource(string source) => new ManualLogSource();
    }
}

// ============================================================
// HarmonyLib Stubs
// ============================================================
namespace HarmonyLib
{
    public class Harmony
    {
        public Harmony(string id) { }
        public void PatchAll() { }
        public void PatchAll(System.Reflection.Assembly assembly) { }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatch : Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(Type type) { }
        public HarmonyPatch(Type type, string methodName) { }
        public HarmonyPatch(string typeName) { }
        public HarmonyPatch(string typeName, string methodName) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HarmonyPrefix : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class HarmonyPostfix : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class HarmonyTranspiler : Attribute { }
}

// ============================================================
// UnityEngine Stubs
// ============================================================
namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static bool operator ==(Object a, Object b)
        {
            if (ReferenceEquals(a, null)) return ReferenceEquals(b, null);
            if (ReferenceEquals(b, null)) return false;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object obj) => base.Equals(obj);
        public override int GetHashCode() => base.GetHashCode();
        public static void Destroy(Object obj) { }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; set; }
        public Transform transform { get; set; }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
    }

    public class MonoBehaviour : Behaviour { }

    public class ScriptableObject : Object { }

    public class GameObject : Object
    {
        public T GetComponent<T>() where T : class => default(T);
    }

    public class Transform : Component { }

    public static class Time
    {
        public static float realtimeSinceStartup => 0f;
        public static float time => 0f;
        public static float deltaTime => 0f;
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogError(object message) { }
        public static void LogWarning(object message) { }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
    }
}

// ============================================================
// Unity.InputSystem Stubs
// ============================================================
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

// ============================================================
// GameNetcodeStuff Stubs (Lethal Company)
// ============================================================
namespace GameNetcodeStuff
{
    public class PlayerControllerB : UnityEngine.MonoBehaviour
    {
        public GrabbableObject currentlyHeldObjectServer;
        public UnityEngine.InputSystem.PlayerInput playerInput;

        public void UseItemOnClient() { }
        public void UseItemOnClient(int slot) { }
    }
}

// ============================================================
// Assembly-CSharp Stubs (Lethal Company)
// ============================================================
public class GrabbableObject : UnityEngine.MonoBehaviour
{
    public bool isHeld;
    public GameNetcodeStuff.PlayerControllerB playerHeldBy;
}

public class KnifeItem : GrabbableObject
{
    private float timeAtLastDamageDealt;
    private float timeAtLastHit;

    public void HitKnife() { }
    public void HitKnife(UnityEngine.Vector3 hitPoint) { }
}

public class IngamePlayerSettings : UnityEngine.MonoBehaviour
{
    public static IngamePlayerSettings Instance { get; set; } = new IngamePlayerSettings();
    public UnityEngine.InputSystem.PlayerInput playerInput { get; set; } = new UnityEngine.InputSystem.PlayerInput();
}

public class StartOfRound : UnityEngine.MonoBehaviour
{
    public static StartOfRound Instance { get; set; }
}

public class RoundManager : UnityEngine.MonoBehaviour
{
    public static RoundManager Instance { get; set; }
}
