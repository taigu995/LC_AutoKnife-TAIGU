// BepInEx stub assembly
namespace BepInEx
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false)]
    public class BepInPlugin : System.Attribute
    {
        public string GUID { get; private set; }
        public string Name { get; private set; }
        public string Version { get; private set; }
        public BepInPlugin(string guid, string name, string version)
        {
            GUID = guid; Name = name; Version = version;
        }
    }

    public abstract class BaseUnityPlugin : UnityEngine.MonoBehaviour
    {
        private Logging.ManualLogSource _logger;
        public Logging.ManualLogSource Logger
        {
            get
            {
                if (_logger == null) _logger = Logging.Logger.CreateLogSource("Stub");
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
