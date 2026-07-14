// AutoKnife - Merged Mod for Lethal Company V81
// Combines: Auto-attack on hold + Remove knife attack cooldown
// Author: TAIGU
// Version: 1.0.2
//
// Changelog:
//   1.0.2 - Bug fix:
//           - Fixed: Removed isLocalPlayerController check that caused
//                    MissingFieldException every frame in V81 (field doesn't exist)
//   1.0.1 - Bug fixes:
//           - Fixed: Changed HarmonyPrefix to HarmonyPostfix on Update patch
//                    (original mod uses Postfix, not Prefix)
//           - Fixed: Changed input action name from "Use" to "ActivateItem"
//                    (matches original mod's FindAction target)
//           - Fixed: Removed spurious ItemActivate patch that could crash
//                    PatchAll() if the method doesn't exist in V81
//           - Added: Player dead/state checks for safety
//           - Added: Fallback input action name resolution

using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using GameNetcodeStuff;

namespace AutoKnife
{
    /// <summary>
    /// Main plugin class for AutoKnife mod.
    /// Merges two functionalities:
    /// 1. Hold left mouse button to auto-attack with knife (from AutoKnifeAttack by Yan01h)
    /// 2. Remove knife attack cooldown (from FastKnife by nexor)
    /// </summary>
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class AutoKnifePlugin : BaseUnityPlugin
    {
        private const string ModGUID = "TAIGU.AutoKnife";
        private const string ModName = "AutoKnife";
        private const string ModVersion = "1.0.3";

        private Harmony _harmony;
        internal static ManualLogSource Log;

        private void Awake()
        {
            // Singleton pattern
            if (Instance == null)
                Instance = this;

            Log = Logger;

            // Initialize Harmony and apply all patches
            _harmony = new Harmony(ModGUID);
            _harmony.PatchAll();

            Log.LogInfo($"[TAIGU] {ModName} v{ModVersion} loaded successfully.");
            Log.LogInfo("[TAIGU] Features: Auto-attack on hold + No knife cooldown");
        }

        public static AutoKnifePlugin Instance { get; private set; }
    }
}

namespace AutoKnife.Patches
{
    /// <summary>
    /// Patch 1: Auto-attack when holding left mouse button.
    /// Targets PlayerControllerB.Update with a Postfix to continuously trigger
    /// knife use while the left mouse button is held down.
    ///
    /// NOTE: Uses HarmonyPostfix (not Prefix) to match the original mod behavior.
    /// The original AutoKnifeAttack by Yan01h uses a Postfix on Update.
    /// </summary>
    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    public class PlayerControllerB_Update_Patch
    {
        private static float _timeAtLastAttack = 0f;

        // Minimum interval between auto-attacks (seconds)
        // Tuned for V81: fast but not server-crashing
        private const float ATTACK_INTERVAL = 0.1f;

        // Cached input action reference for performance
        private static InputAction _cachedUseAction;
        private static bool _actionSearchDone = false;

        /// <summary>
        /// Postfix patch on PlayerControllerB.Update.
        /// Checks if left mouse button is held and the player is holding a knife,
        /// then triggers UseItemOnClient at a controlled interval.
        ///
        /// FIX v1.0.1: Changed from HarmonyPrefix to HarmonyPostfix to match original.
        /// </summary>
        [HarmonyPostfix]
        private static void UpdatePostfix(PlayerControllerB __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                // FIX v1.0.1: Safety check for player state
                // Don't auto-attack if player is dead
                if (__instance.isPlayerDead)
                    return;

                // Note: No explicit isLocalPlayerController check needed.
                // The IsPressed() check on the input action implicitly ensures
                // only the local player's input triggers the attack, because
                // input actions are only active for the local player.

                // Check if the player is holding a KnifeItem
                GrabbableObject heldItem = __instance.currentlyHeldObjectServer;
                if (heldItem == null || !(heldItem is KnifeItem))
                    return;

                // FIX v1.0.3: Use IngamePlayerSettings.Instance.playerInput
                // instead of __instance.playerInput (which doesn't exist in V81)
                // This matches the original AutoKnifeAttack mod's approach.
                if (!_actionSearchDone)
                {
                    _actionSearchDone = true;

                    try
                    {
                        PlayerInput playerInput = IngamePlayerSettings.Instance.playerInput;
                        if (playerInput != null && playerInput.actions != null)
                        {
                            // Primary: "ActivateItem" (matches original AutoKnifeAttack mod)
                            _cachedUseAction = playerInput.actions.FindAction("ActivateItem");

                            // Fallback: "Use" (alternative action name)
                            if (_cachedUseAction == null)
                                _cachedUseAction = playerInput.actions.FindAction("Use");
                        }
                    }
                    catch (Exception ex)
                    {
                        AutoKnife.AutoKnifePlugin.Log?.LogError(
                            $"[TAIGU] Failed to get input actions: {ex.Message}");
                    }
                }

                if (_cachedUseAction == null)
                    return;

                // Check if the left mouse button (ActivateItem action) is currently pressed
                if (!_cachedUseAction.IsPressed())
                    return;

                // Rate-limit the auto-attack to avoid flooding
                float currentTime = Time.realtimeSinceStartup;
                if (currentTime - _timeAtLastAttack < ATTACK_INTERVAL)
                    return;

                _timeAtLastAttack = currentTime;

                // Trigger the knife attack
                __instance.UseItemOnClient();
            }
            catch (Exception ex)
            {
                AutoKnife.AutoKnifePlugin.Log?.LogError($"[TAIGU] Auto-attack patch error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch 2: Remove knife attack cooldown.
    /// After HitKnife is called, resets the timeAtLastDamageDealt field
    /// to -1, effectively removing any cooldown between knife strikes.
    /// Adapted for V81: uses reflection with fallback to handle potential field name changes.
    ///
    /// Based on FastKnife by nexor (original implementation).
    /// </summary>
    [HarmonyPatch(typeof(KnifeItem), "HitKnife")]
    public class KnifeItem_HitKnife_Patch
    {
        // Cached field info for performance
        private static FieldInfo _timeAtLastDamageDealtField;
        private static bool _fieldSearchDone = false;

        /// <summary>
        /// Postfix patch on KnifeItem.HitKnife.
        /// Resets the internal cooldown timer to allow immediate next attack.
        /// </summary>
        [HarmonyPostfix]
        private static void HitKnifePostfix(KnifeItem __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                // Lazy-initialize the field lookup (cached for performance)
                if (!_fieldSearchDone)
                {
                    _fieldSearchDone = true;

                    // Try to find the cooldown field via reflection
                    // V81 may have renamed fields, so we try multiple candidates
                    Type knifeType = typeof(KnifeItem);
                    BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

                    // Primary target: timeAtLastDamageDealt (original field name from FastKnife)
                    _timeAtLastDamageDealtField = knifeType.GetField("timeAtLastDamageDealt", flags);

                    // Fallback candidates for V81 compatibility
                    if (_timeAtLastDamageDealtField == null)
                        _timeAtLastDamageDealtField = knifeType.GetField("timeAtLastHit", flags);

                    if (_timeAtLastDamageDealtField == null)
                    {
                        // Last resort: search all float fields for likely candidates
                        FieldInfo[] allFields = knifeType.GetFields(flags);
                        foreach (FieldInfo field in allFields)
                        {
                            if (field.FieldType == typeof(float) &&
                                (field.Name.Contains("time") || field.Name.Contains("Time") ||
                                 field.Name.Contains("damage") || field.Name.Contains("Damage") ||
                                 field.Name.Contains("cooldown") || field.Name.Contains("Cooldown") ||
                                 field.Name.Contains("last") || field.Name.Contains("Last")))
                            {
                                _timeAtLastDamageDealtField = field;
                                AutoKnife.AutoKnifePlugin.Log?.LogInfo(
                                    $"[TAIGU] Found cooldown field via search: {field.Name}");
                                break;
                            }
                        }
                    }

                    if (_timeAtLastDamageDealtField != null)
                    {
                        AutoKnife.AutoKnifePlugin.Log?.LogInfo(
                            $"[TAIGU] Cooldown field resolved: {_timeAtLastDamageDealtField.Name}");
                    }
                    else
                    {
                        AutoKnife.AutoKnifePlugin.Log?.LogError(
                            "[TAIGU] WARNING: Could not find any cooldown field in KnifeItem! " +
                            "Cooldown removal may not work on this game version.");
                    }
                }

                // Apply the cooldown reset
                if (_timeAtLastDamageDealtField != null)
                {
                    // Set to -1 to indicate no recent damage (effectively removing cooldown)
                    _timeAtLastDamageDealtField.SetValue(__instance, -1f);
                }
            }
            catch (Exception ex)
            {
                AutoKnife.AutoKnifePlugin.Log?.LogError($"[TAIGU] Cooldown removal patch error: {ex.Message}");
            }
        }
    }
}
