using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace AutoKnife
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class AutoKnifePlugin : BaseUnityPlugin
    {
        public const string ModGuid = "TAIGU.AutoKnife";
        public const string ModName = "AutoKnife";
        public const string ModVersion = "1.0.7";

        private Harmony _harmony;
        private static float _timeAtLastAttack = 0f;
        private const float AttackInterval = 0.1f;

        // Cached types
        private static Type _playerControllerBType;
        private static Type _knifeItemType;
        private static MethodInfo _useItemOnClientMethod;
        private static FieldInfo _currentlyHeldObjectServerField;
        private static FieldInfo _timeAtLastDamageDealtField;

        private void Awake()
        {
            Logger.LogInfo($"[TAIGU] {ModName} v{ModVersion} loading...");
            Logger.LogInfo($"[TAIGU] Features: Auto-attack on hold + No knife cooldown");
            Logger.LogInfo($"[TAIGU] Input: Input.GetMouseButton(0) (direct mouse detection)");

            // Initialize types via reflection
            if (!InitializeTypes())
            {
                Logger.LogError("[TAIGU] Failed to initialize types, mod will not function");
                return;
            }

            _harmony = new Harmony(ModGuid);
            _harmony.PatchAll(typeof(AutoKnifePlugin).Assembly);
            Logger.LogInfo($"[TAIGU] {ModName} v{ModVersion} loaded successfully.");
        }

        private bool InitializeTypes()
        {
            try
            {
                // Get Assembly-CSharp assembly
                Assembly assemblyCSharp = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (assemblyCSharp == null)
                {
                    Logger.LogError("[TAIGU] Assembly-CSharp not found");
                    return false;
                }

                // Get PlayerControllerB type
                _playerControllerBType = assemblyCSharp.GetType("GameNetcodeStuff.PlayerControllerB");
                if (_playerControllerBType == null)
                {
                    Logger.LogError("[TAIGU] PlayerControllerB type not found");
                    return false;
                }

                // Get KnifeItem type
                _knifeItemType = assemblyCSharp.GetType("KnifeItem");
                if (_knifeItemType == null)
                {
                    Logger.LogError("[TAIGU] KnifeItem type not found");
                    return false;
                }

                // Get UseItemOnClient method
                _useItemOnClientMethod = _playerControllerBType.GetMethod("UseItemOnClient",
                    BindingFlags.Public | BindingFlags.Instance);
                if (_useItemOnClientMethod == null)
                {
                    Logger.LogError("[TAIGU] UseItemOnClient method not found");
                    return false;
                }

                // Get currentlyHeldObjectServer field
                _currentlyHeldObjectServerField = _playerControllerBType.GetField("currentlyHeldObjectServer",
                    BindingFlags.Public | BindingFlags.Instance);
                if (_currentlyHeldObjectServerField == null)
                {
                    Logger.LogError("[TAIGU] currentlyHeldObjectServer field not found");
                    return false;
                }

                // Get timeAtLastDamageDealt field from KnifeItem
                _timeAtLastDamageDealtField = _knifeItemType.GetField("timeAtLastDamageDealt",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (_timeAtLastDamageDealtField == null)
                {
                    // Try alternative field names
                    Logger.LogWarning("[TAIGU] timeAtLastDamageDealt field not found, trying alternatives...");
                    foreach (var field in _knifeItemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        string fieldName = field.Name.ToLower();
                        if (fieldName.Contains("time") && (fieldName.Contains("damage") || fieldName.Contains("attack") || fieldName.Contains("hit")))
                        {
                            _timeAtLastDamageDealtField = field;
                            Logger.LogInfo($"[TAIGU] Found alternative field: {field.Name}");
                            break;
                        }
                    }
                }

                if (_timeAtLastDamageDealtField != null)
                {
                    Logger.LogInfo($"[TAIGU] Cooldown field resolved: {_timeAtLastDamageDealtField.Name}");
                }
                else
                {
                    Logger.LogWarning("[TAIGU] No cooldown field found, cooldown removal disabled");
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TAIGU] Type initialization failed: {ex.Message}");
                return false;
            }
        }

        // Patch PlayerControllerB.Update
        [HarmonyPatch]
        public static class PlayerControllerB_Update_Patch
        {
            public static bool Prefix(object __instance)
            {
                try
                {
                    // Check if player is dead
                    var isPlayerDeadField = __instance.GetType().GetField("isPlayerDead",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (isPlayerDeadField != null)
                    {
                        bool isDead = (bool)isPlayerDeadField.GetValue(__instance);
                        if (isDead) return true;
                    }

                    // Check if left mouse button is held down
                    if (!Input.GetMouseButton(0))
                    {
                        return true;
                    }

                    // Check if holding a knife
                    object heldItem = _currentlyHeldObjectServerField.GetValue(__instance);
                    if (heldItem == null)
                    {
                        return true;
                    }

                    if (!_knifeItemType.IsInstanceOfType(heldItem))
                    {
                        return true;
                    }

                    // Check attack interval
                    float currentTime = Time.realtimeSinceStartup;
                    if (currentTime - _timeAtLastAttack < AttackInterval)
                    {
                        return true;
                    }

                    // Call UseItemOnClient
                    _useItemOnClientMethod.Invoke(__instance, null);
                    _timeAtLastAttack = currentTime;

                    return false; // Skip original Update to prevent double execution
                }
                catch (Exception ex)
                {
                    // Silently fail to avoid breaking the game
                    return true;
                }
            }
        }

        // Patch KnifeItem.HitKnife to remove cooldown
        [HarmonyPatch]
        public static class KnifeItem_HitKnife_Patch
        {
            public static void Postfix(object __instance)
            {
                try
                {
                    if (_timeAtLastDamageDealtField != null)
                    {
                        _timeAtLastDamageDealtField.SetValue(__instance, -1f);
                    }
                }
                catch (Exception ex)
                {
                    // Silently fail
                }
            }
        }
    }
}
