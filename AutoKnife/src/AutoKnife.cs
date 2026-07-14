// AutoKnife - Merged Mod for Lethal Company V81
// Combines: Auto-attack on hold + Remove knife attack cooldown
// Author: TAIGU
// Version: 1.0.4
//
// Changelog:
//   1.0.4 - Critical fix:
//           - Replaced game input system API with direct Mouse.current.leftButton.isPressed
//           - V81 removed both PlayerControllerB.playerInput and IngamePlayerSettings.playerInput
//           - Direct mouse detection is version-independent and more robust
//   1.0.3 - Fixed: IngamePlayerSettings.Instance.playerInput doesn't exist in V81
//   1.0.2 - Fixed: Removed isLocalPlayerController (V81 doesn't have this field)
//   1.0.1 - Fixed: HarmonyPrefix->Postfix, "Use"->"ActivateItem", removed ItemActivate patch

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
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class AutoKnifePlugin : BaseUnityPlugin
    {
        private const string ModGUID = "TAIGU.AutoKnife";
        private const string ModName = "AutoKnife";
        private const string ModVersion = "1.0.4";

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
            Log.LogInfo("[TAIGU] Input: Mouse.current.leftButton.isPressed (direct detection)");
        }

        public static AutoKnifePlugin Instance { get; private set; }
    }
}

namespace AutoKnife.Patches
{
    /// <summary>
    /// Patch 1: Auto-attack when holding left mouse button.
    /// Uses Mouse.current.leftButton.isPressed for direct mouse detection.
    /// This bypasses the game's input system API which changed in V81.
    /// </summary>
    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    public class PlayerControllerB_Update_Patch
    {
        private static float _timeAtLastAttack = 0f;
        private const float ATTACK_INTERVAL = 0.1f;

        [HarmonyPostfix]
        private static void UpdatePostfix(PlayerControllerB __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                // Safety: don't auto-attack if player is dead
                if (__instance.isPlayerDead)
                    return;

                // Check if the player is holding a KnifeItem
                GrabbableObject heldItem = __instance.currentlyHeldObjectServer;
                if (heldItem == null || !(heldItem is KnifeItem))
                    return;

                // FIX v1.0.4: Direct mouse detection via Unity Input System
                // Mouse.current is a static property that works regardless of game version
                Mouse mouse = Mouse.current;
                if (mouse == null || mouse.leftButton == null)
                    return;

                if (!mouse.leftButton.isPressed)
                    return;

                // Rate-limit the auto-attack
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
    /// After HitKnife is called, resets the timeAtLastDamageDealt field to -1.
    /// Uses reflection with fallback for V81 field name changes.
    /// Based on FastKnife by nexor.
    /// </summary>
    [HarmonyPatch(typeof(KnifeItem), "HitKnife")]
    public class KnifeItem_HitKnife_Patch
    {
        private static FieldInfo _timeAtLastDamageDealtField;
        private static bool _fieldSearchDone = false;

        [HarmonyPostfix]
        private static void HitKnifePostfix(KnifeItem __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                if (!_fieldSearchDone)
                {
                    _fieldSearchDone = true;

                    Type knifeType = typeof(KnifeItem);
                    BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

                    // Primary: timeAtLastDamageDealt (original field name from FastKnife)
                    _timeAtLastDamageDealtField = knifeType.GetField("timeAtLastDamageDealt", flags);

                    // Fallback: timeAtLastHit
                    if (_timeAtLastDamageDealtField == null)
                        _timeAtLastDamageDealtField = knifeType.GetField("timeAtLastHit", flags);

                    // Last resort: search all float fields
                    if (_timeAtLastDamageDealtField == null)
                    {
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

                if (_timeAtLastDamageDealtField != null)
                {
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
