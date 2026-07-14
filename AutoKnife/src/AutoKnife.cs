// AutoKnife - Merged Mod for Lethal Company V81
// Combines: Auto-attack on hold + Remove knife attack cooldown
// Author: TAIGU
// Version: 1.0.0

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
    /// 1. Hold left mouse button to auto-attack with knife
    /// 2. Remove knife attack cooldown (timeAtLastDamageDealt reset)
    /// </summary>
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class AutoKnifePlugin : BaseUnityPlugin
    {
        private const string ModGUID = "TAIGU.AutoKnife";
        private const string ModName = "AutoKnife";
        private const string ModVersion = "1.0.0";

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
    /// Targets PlayerControllerB.Update to continuously trigger knife use
    /// while the left mouse button is held down.
    /// </summary>
    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    public class PlayerControllerB_Update_Patch
    {
        private static float _timeAtLastAttack = 0f;

        // Minimum interval between auto-attacks (seconds)
        // Tuned for V81: fast but not server-crashing
        private const float ATTACK_INTERVAL = 0.1f;

        /// <summary>
        /// Prefix patch on PlayerControllerB.Update.
        /// Checks if left mouse button is held and the player is holding a knife,
        /// then triggers UseItemOnClient at a controlled interval.
        /// </summary>
        [HarmonyPrefix]
        private static void UpdatePrefix(PlayerControllerB __instance)
        {
            try
            {
                // Only process for the local player
                if (__instance == null)
                    return;

                // Check if the player is holding a KnifeItem
                GrabbableObject heldItem = __instance.currentlyHeldObjectServer;
                if (heldItem == null || !(heldItem is KnifeItem))
                    return;

                // Get the input action for the primary use/attack button
                // In Lethal Company, this is typically the "Use" action bound to left mouse
                InputAction useAction = null;
                if (__instance.playerInput != null && __instance.playerInput.actions != null)
                {
                    useAction = __instance.playerInput.actions.FindAction("Use");
                }

                if (useAction == null)
                    return;

                // Check if the left mouse button (Use action) is currently pressed
                if (!useAction.IsPressed())
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
    /// Adapted for V81: uses reflection to handle potential field name changes.
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

                    // Primary target: timeAtLastDamageDealt (original field name)
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

    /// <summary>
    /// Patch 3 (V81 Safety): ItemActivate postfix to prevent
    /// the game from blocking rapid item usage.
    /// Ensures the auto-attack isn't throttled by item activation guards.
    /// </summary>
    [HarmonyPatch(typeof(PlayerControllerB), "ItemActivate")]
    public class PlayerControllerB_ItemActivate_Patch
    {
        [HarmonyPostfix]
        private static void ItemActivatePostfix(PlayerControllerB __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                // If holding a knife, ensure the item state allows continued use
                GrabbableObject heldItem = __instance.currentlyHeldObjectServer;
                if (heldItem != null && heldItem is KnifeItem)
                {
                    // Reset any item-level activation cooldown if present
                    Type itemType = heldItem.GetType();
                    FieldInfo isUsedField = itemType.GetField("isBeingUsed",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (isUsedField != null)
                    {
                        isUsedField.SetValue(heldItem, false);
                    }
                }
            }
            catch
            {
                // Silently fail - this is a supplementary patch
            }
        }
    }
}
