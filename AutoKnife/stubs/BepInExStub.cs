using System;

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)]
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

    public abstract class BaseUnityPlugin
    {
        public BepInEx.Logging.ManualLogSource Logger { get; set; }
    }
}

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public void LogInfo(object data) { }
        public void LogWarning(object data) { }
        public void LogError(object data) { }
        public void LogDebug(object data) { }
    }
}
