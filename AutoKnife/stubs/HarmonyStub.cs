// HarmonyLib stub assembly (0Harmony)
namespace HarmonyLib
{
    public class HarmonyMethod
    {
        public System.Reflection.MethodInfo method;
        public HarmonyMethod() { }
        public HarmonyMethod(System.Reflection.MethodInfo method) { this.method = method; }
        public HarmonyMethod(System.Type type, string methodName) { }
    }

    public class Harmony
    {
        public Harmony(string id) { }
        public void PatchAll() { }
        public void PatchAll(System.Reflection.Assembly assembly) { }
        public void Patch(System.Reflection.MethodInfo original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatch : System.Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(System.Type type) { }
        public HarmonyPatch(System.Type type, string methodName) { }
        public HarmonyPatch(string typeName) { }
        public HarmonyPatch(string typeName, string methodName) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class HarmonyPrefix : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class HarmonyPostfix : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class HarmonyTranspiler : System.Attribute { }
}
