// UnityEngine.CoreModule stub
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
