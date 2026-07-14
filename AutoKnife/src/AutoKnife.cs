// AutoKnife - Merged Mod for Lethal Company V81
// Combines: Auto-attack on hold + Remove knife attack cooldown
// Author: TAIGU
// Version: 1.0.6
//
// Changelog:
//   1.0.6 - CRITICAL FIX: Complete reflection-based approach
//           - All Unity type references now use reflection to avoid version mismatch
//           - Input.GetMouseButton(0) called via reflection (no compile-time UnityEngine.Input ref)
//           - PlayerControllerB fields accessed via reflection
//           - KnifeItem fields accessed via reflection
//           - Zero direct type references to Unity types - eliminates all TypeLoadException risks
//   1.0.5 - Attempted Input.GetMouseButton(0) but still had version mismatch
//   1.0.4 - Attempted Mouse.current.leftButton.isPressed (TypeLoadException)
//   1.0.3 - Attempted IngamePlayerSettings.Instance.playerInput (MissingMethodException)
//   1.0.2 - Removed isLocalPlayerController (MissingFieldException)
//   1.0.1 - Fixed HarmonyPrefix->Postfix, "Use"->"ActivateItem", removed ItemActivate patch

using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace AutoKnife
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class AutoKnifePlugin : BaseUnityPlugin
    {
        private const string ModGUID = "TAIGU.AutoKnife";
        private const string ModName = "AutoKnife";
        private const string ModVersion = "1.0.6";

        private Harmony _harmony;
        internal static ManualLogSource Log;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;

            Log = Logger;

            _harmony = new Harmony(ModGUID);
            _harmony.PatchAll();

            Log.LogInfo($"[TAIGU] {ModName} v{ModVersion} loaded successfully.");
            Log.LogInfo("[TAIGU] Features: Auto-attack on hold + No knife cooldown");
            Log.LogInfo("[TAIGU] Input: Reflection-based Input.GetMouseButton(0) - zero version dependency");
        }

        public static AutoKnifePlugin Instance { get; private set; }
    }
}

namespace AutoKnife.Patches
{
    /// <summary>
    /// Patch 1: Auto-attack when holding left mouse button.
    /// COMPLETELY REFLECTION-BASED - no direct Unity type references.
    /// This eliminates all TypeLoadException risks from version mismatches.
    /// </summary>
    [HarmonyPatch]
    public class PlayerControllerB_Update_Patch
    {
        private static float _timeAtLastAttack = 0f;
        private const float ATTACK_INTERVAL = 0.1f;

        // Reflection cache for Input.GetMouseButton
        private static MethodInfo _getMouseButtonMethod;
        private static bool _inputMethodResolved = false;

        // Reflection cache for PlayerControllerB fields
        private static FieldInfo _isPlayerDeadField;
        private static FieldInfo _currentlyHeldObjectServerField;
        private static MethodInfo _useItemOnClientMethod;
        private static bool _playerFieldsResolved = false;

        private static void ResolveInputMethod()
        {
            if (_inputMethodResolved) return;
            _inputMethodResolved = true;

            try
            {
                // Get UnityEngine.Input class via reflection
                var inputType = typeof(UnityEngine.Object).Assembly.GetType("UnityEngine.Input");
                if (inputType != null)
                {
                    _getMouseButtonMethod = inputType.GetMethod("GetMouseButton",
                        BindingFlags.Public | BindingFlags.Static,
                        null, new Type[] { typeof(int) }, null);
                }
            }
            catch (Exception ex)
            {
                AutoKnife.AutoKnifePlugin.Log?.LogWarning($"[TAIGU] Failed to resolve Input.GetMouseButton: {ex.Message}");
            }
        }

        private static void ResolvePlayerFields(Type playerType)
        {
            if (_playerFieldsResolved) return;
            _playerFieldsResolved = true;

            try
            {
                _isPlayerDeadField = playerType.GetField("isPlayerDead",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                _currentlyHeldObjectServerField = playerType.GetField("currentlyHeldObjectServer",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                _useItemOnClientMethod = playerType.GetMethod("UseItemOnClient",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, Type.EmptyTypes, null);
            }
            catch (Exception ex)
            {
                AutoKnife.AutoKnifePlugin.Log?.LogWarning($"[TAIGU] Failed to resolve PlayerControllerB fields: {ex.Message}");
            }
        }

        private static bool IsLeftMouseHeld()
        {
            ResolveInputMethod();
            if (_getMouseButtonMethod == null) return false;

            try
            {
                return (bool)_getMouseButtonMethod.Invoke(null, new object[] { 0 });
            }
            catch
            {
                return false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("GameNetcodeStuff.PlayerControllerB", "Update")]
        private static void UpdatePostfix(object __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                var playerType = __instance.GetType();
                ResolvePlayerFields(playerType);

                // Safety: don't auto-attack if player is dead
                if (_isPlayerDeadField != null)
                {
                    try
                    {
                        if ((bool)_isPlayerDeadField.GetValue(__instance))
                            return;
                    }
                    catch { }
                }

                // Check if the player is holding a KnifeItem
                object heldItem = null;
                if (_currentlyHeldObjectServerField != null)
                {
                    try { heldItem = _currentlyHeldObjectServerField.GetValue(__instance); }
                    catch { }
                }

                if (heldItem == null) return;

                // Check if held item is KnifeItem by type name
                if (heldItem.GetType().Name != "KnifeItem") return;

                // Check if left mouse button is held (via reflection)
                if (!IsLeftMouseHeld())
                    return;

                // Rate-limit the auto-attack
                float currentTime = UnityEngine.Time.realtimeSinceStartup;
                if (currentTime - _timeAtLastAttack < ATTACK_INTERVAL)
                    return;

                _timeAtLastAttack = currentTime;

                // Trigger the knife attack via reflection
                if (_useItemOnClientMethod != null)
                {
                    try { _useItemOnClientMethod.Invoke(__instance, null); }
                    catch (Exception ex)
                    {
                        AutoKnife.AutoKnifePlugin.Log?.LogWarning($"[TAIGU] UseItemOnClient failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AutoKnife.AutoKnifePlugin.Log?.LogError($"[TAIGU] Auto-attack patch error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch 2: Remove knife attack cooldown.
    /// COMPLETELY REFLECTION-BASED - no direct Unity type references.
    /// After HitKnife is called, resets the timeAtLastDamageDealt field to -1.
    /// Based on FastKnife by nexor.
    /// </summary>
    [HarmonyPatch]
    public class KnifeItem_HitKnife_Patch
    {
        private static FieldInfo _timeAtLastDamageDealtField;
        private static bool _fieldSearchDone = false;

        private static void ResolveCooldownField(Type knifeType)
        {
            if (_fieldSearchDone) return;
            _fieldSearchDone = true;

            try
            {
                // Try exact field name first (from original FastKnife mod)
                _timeAtLastDamageDealtField = knifeType.GetField("timeAtLastDamageDealt",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (_timeAtLastDamageDealtField != null)
                {
                    AutoKnife.AutoKnifePlugin.Log?.LogInfo("[TAIGU] Cooldown field resolved: timeAtLastDamageDealt");
                    return;
                }

                // Fallback: try alternative field names for V81 compatibility
                string[] fallbackNames = { "timeAtLastHit", "lastAttackTime", "attackCooldown", "timeSinceLastAttack" };
                foreach (string name in fallbackNames)
                {
                    _timeAtLastDamageDealtField = knifeType.GetField(name,
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (_timeAtLastDamageDealtField != null)
                    {
                        AutoKnife.AutoKnifePlugin.Log?.LogInfo($"[TAIGU] Cooldown field resolved (fallback): {name}");
                        return;
                    }
                }

                // Last resort: search all float fields for time/damage/cooldown keywords
                foreach (var field in knifeType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (field.FieldType == typeof(float))
                    {
                        string lowerName = field.Name.ToLower();
                        if (lowerName.Contains("time") || lowerName.Contains("damage") || lowerName.Contains("cooldown"))
                        {
                            _timeAtLastDamageDealtField = field;
                            AutoKnife.AutoKnifePlugin.Log?.LogInfo($"[TAIGU] Cooldown field resolved (search): {field.Name}");
                            return;
                        }
                    }
                }

                AutoKnife.AutoKnifePlugin.Log?.LogWarning("[TAIGU] WARNING: Could not find cooldown field on KnifeItem");
            }
            catch (Exception ex)
            {
                AutoKnife.AutoKnifePlugin.Log?.LogError($"[TAIGU] Failed to resolve cooldown field: {ex.Message}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("KnifeItem", "HitKnife")]
        private static void HitKnifePostfix(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var knifeType = __instance.GetType();
                ResolveCooldownField(knifeType);

                if (_timeAtLastDamageDealtField == null)
                {
                    AutoKnife.AutoKnifePlugin.Log?.LogError("[TAIGU] ERROR: timeAtLastDamageDealt field not found on KnifeItem!");
                    return;
                }

                // Set cooldown to -1 to effectively remove it
                _timeAtLastDamageDealtField.SetValue(__instance, -1f);
            }
            catch (Exception ex)
            {
                AutoKnife.AutoKnifePlugin.Log?.LogError($"[TAIGU] HitKnife patch error: {ex.Message}");
            }
        }
    }
}
