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
        public const string ModVersion = "1.0.17";

        private Harmony _harmony;
        private static float _timeAtLastAttack = 0f;
        private const float AttackInterval = 0.1f;
        private static BepInEx.Logging.ManualLogSource _staticLogger;

        // Cached types
        private static Type _playerControllerBType;
        private static Type _knifeItemType;
        private static MethodInfo _useItemOnClientMethod;
        private static MethodInfo _activateItemMethod;
        private static MethodInfo _updateMethod;
        private static MethodInfo _hitKnifeMethod;
        private static FieldInfo _currentlyHeldObjectServerField;
        private static FieldInfo _timeAtLastDamageDealtField;

        private void Awake()
        {
            _staticLogger = Logger;
            _staticLogger.LogInfo($"[TAIGU] {ModName} v{ModVersion} loading...");
            _staticLogger.LogInfo($"[TAIGU] Features: Auto-attack on hold + No knife cooldown");
            _staticLogger.LogInfo($"[TAIGU] Input: Input.GetMouseButton(0) (direct mouse detection)");

            // Initialize types via reflection
            if (!InitializeTypes())
            {
                _staticLogger.LogError("[TAIGU] Failed to initialize types, mod will not function");
                return;
            }

            // Manual patching with runtime-resolved types
            _harmony = new Harmony(ModGuid);
            ApplyPatches();
            _staticLogger.LogInfo($"[TAIGU] {ModName} v{ModVersion} loaded successfully.");
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
                    _staticLogger.LogError("[TAIGU] Assembly-CSharp not found");
                    return false;
                }

                // Get PlayerControllerB type
                _playerControllerBType = assemblyCSharp.GetType("GameNetcodeStuff.PlayerControllerB");
                if (_playerControllerBType == null)
                {
                    _staticLogger.LogError("[TAIGU] PlayerControllerB type not found");
                    return false;
                }

                // Get KnifeItem type
                _knifeItemType = assemblyCSharp.GetType("KnifeItem");
                if (_knifeItemType == null)
                {
                    _staticLogger.LogError("[TAIGU] KnifeItem type not found");
                    return false;
                }

                // Get Update method
                _updateMethod = _playerControllerBType.GetMethod("Update",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (_updateMethod == null)
                {
                    _staticLogger.LogWarning("[TAIGU] Update method not found, listing available methods...");
                    var allMethods = _playerControllerBType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var methodNames = allMethods.Select(m => m.Name).Distinct().OrderBy(n => n).ToList();
                    _staticLogger.LogWarning($"[TAIGU] Available methods in PlayerControllerB: {string.Join(", ", methodNames)}");
                    
                    // Try to find an alternative method to patch
                    // Look for methods that are called frequently (like LateUpdate, FixedUpdate, etc.)
                    _updateMethod = _playerControllerBType.GetMethod("LateUpdate",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (_updateMethod == null)
                    {
                        _updateMethod = _playerControllerBType.GetMethod("FixedUpdate",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    
                    if (_updateMethod == null)
                    {
                        _staticLogger.LogError("[TAIGU] No suitable update method found");
                        return false;
                    }
                    _staticLogger.LogInfo($"[TAIGU] Using {_updateMethod.Name} as alternative to Update");
                }

                // Get HitKnife method
                _hitKnifeMethod = _knifeItemType.GetMethod("HitKnife",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (_hitKnifeMethod == null)
                {
                    _staticLogger.LogError("[TAIGU] HitKnife method not found");
                    return false;
                }

                // Get UseItemOnClient method
                _useItemOnClientMethod = _playerControllerBType.GetMethod("UseItemOnClient",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (_useItemOnClientMethod == null)
                {
                    // Try with parameters
                    _useItemOnClientMethod = _playerControllerBType.GetMethod("UseItemOnClient",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null, new Type[] { typeof(int) }, null);
                }
                
                // Fallback: try ActivateItem_performed
                MethodInfo activateItemMethod = null;
                if (_useItemOnClientMethod == null)
                {
                    activateItemMethod = _playerControllerBType.GetMethod("ActivateItem_performed",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (activateItemMethod != null)
                    {
                        _staticLogger.LogInfo("[TAIGU] Using ActivateItem_performed as fallback");
                    }
                }
                
                if (_useItemOnClientMethod == null && activateItemMethod == null)
                {
                    var allMethods = _playerControllerBType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var matchingMethods = allMethods.Where(m => m.Name.Contains("Use") || m.Name.Contains("Item")).Select(m => m.Name).Distinct().ToList();
                    _staticLogger.LogError($"[TAIGU] No suitable method found. Available methods: {string.Join(", ", matchingMethods)}");
                    return false;
                }
                
                _activateItemMethod = activateItemMethod;

                // Get currentlyHeldObjectServer field
                _currentlyHeldObjectServerField = _playerControllerBType.GetField("currentlyHeldObjectServer",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (_currentlyHeldObjectServerField == null)
                {
                    _staticLogger.LogError("[TAIGU] currentlyHeldObjectServer field not found");
                    return false;
                }

                // Get timeAtLastDamageDealt field
                _timeAtLastDamageDealtField = _knifeItemType.GetField("timeAtLastDamageDealt",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (_timeAtLastDamageDealtField == null)
                {
                    _staticLogger.LogWarning("[TAIGU] timeAtLastDamageDealt field not found, trying alternatives...");
                    foreach (var field in _knifeItemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        string fieldName = field.Name.ToLower();
                        if (fieldName.Contains("time") && (fieldName.Contains("damage") || fieldName.Contains("attack") || fieldName.Contains("hit")))
                        {
                            _timeAtLastDamageDealtField = field;
                            _staticLogger.LogInfo($"[TAIGU] Found alternative field: {field.Name}");
                            break;
                        }
                    }
                }

                if (_timeAtLastDamageDealtField != null)
                {
                    _staticLogger.LogInfo($"[TAIGU] Cooldown field resolved: {_timeAtLastDamageDealtField.Name}");
                }
                else
                {
                    _staticLogger.LogWarning("[TAIGU] No cooldown field found, cooldown removal disabled");
                }

                return true;
            }
            catch (Exception ex)
            {
                _staticLogger.LogError($"[TAIGU] Type initialization failed: {ex.Message}");
                return false;
            }
        }

        private void ApplyPatches()
        {
            try
            {
                // Patch PlayerControllerB.Update (or fallback)
                if (_updateMethod != null)
                {
                    var updatePrefixMethod = typeof(AutoKnifePlugin).GetMethod("UpdatePrefix",
                        BindingFlags.Public | BindingFlags.Static);
                    if (updatePrefixMethod != null)
                    {
                        var updatePrefix = new HarmonyMethod(updatePrefixMethod);
                        _harmony.Patch(_updateMethod, prefix: updatePrefix);
                        _staticLogger.LogInfo($"[TAIGU] Patched PlayerControllerB.{_updateMethod.Name}");
                    }
                    else
                    {
                        _staticLogger.LogError("[TAIGU] UpdatePrefix method not found");
                    }
                }
                else
                {
                    _staticLogger.LogError("[TAIGU] Update method not found, auto-attack disabled");
                }

                // Patch KnifeItem.HitKnife
                if (_hitKnifeMethod != null && _timeAtLastDamageDealtField != null)
                {
                    var hitKnifePostfixMethod = typeof(AutoKnifePlugin).GetMethod("HitKnifePostfix",
                        BindingFlags.Public | BindingFlags.Static);
                    if (hitKnifePostfixMethod != null)
                    {
                        var hitKnifePostfix = new HarmonyMethod(hitKnifePostfixMethod);
                        _harmony.Patch(_hitKnifeMethod, postfix: hitKnifePostfix);
                        _staticLogger.LogInfo("[TAIGU] Patched KnifeItem.HitKnife");
                    }
                    else
                    {
                        _staticLogger.LogError("[TAIGU] HitKnifePostfix method not found");
                    }
                }
            }
            catch (Exception ex)
            {
                _staticLogger.LogError($"[TAIGU] Failed to apply patches: {ex.Message}");
            }
        }

        // Manual patch methods (not using attributes)
        public static bool UpdatePrefix(object __instance)
        {
            _staticLogger.LogDebug($"[TAIGU] UpdatePrefix called, instance: {__instance?.GetType().Name ?? "null"}");
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
                bool mouseHeld = Input.GetMouseButton(0);
                if (!mouseHeld)
                {
                    return true;
                }

                _staticLogger.LogInfo("[TAIGU] Left mouse button held, checking held item...");

                // Check if holding a knife
                object heldItem = _currentlyHeldObjectServerField.GetValue(__instance);
                if (heldItem == null)
                {
                    _staticLogger.LogInfo("[TAIGU] No item held");
                    return true;
                }

                _staticLogger.LogInfo($"[TAIGU] Held item type: {heldItem.GetType().Name}");

                if (!_knifeItemType.IsInstanceOfType(heldItem))
                {
                    _staticLogger.LogInfo("[TAIGU] Held item is not a knife");
                    return true;
                }

                _staticLogger.LogInfo("[TAIGU] Holding knife, checking attack interval...");

                // Check attack interval
                float currentTime = Time.realtimeSinceStartup;
                if (currentTime - _timeAtLastAttack < AttackInterval)
                {
                    return true;
                }

                _staticLogger.LogInfo("[TAIGU] Attack interval passed, triggering attack...");

                // Call the appropriate method
                if (_useItemOnClientMethod != null)
                {
                    var parameters = _useItemOnClientMethod.GetParameters();
                    if (parameters.Length == 0)
                    {
                        _useItemOnClientMethod.Invoke(__instance, null);
                        _staticLogger.LogInfo("[TAIGU] Called UseItemOnClient()");
                    }
                    else
                    {
                        object[] args = new object[parameters.Length];
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            if (parameters[i].ParameterType == typeof(int))
                            {
                                var currentItemSlotField = __instance.GetType().GetField("currentItemSlot",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (currentItemSlotField != null)
                                {
                                    args[i] = currentItemSlotField.GetValue(__instance);
                                }
                                else
                                {
                                    args[i] = 0;
                                }
                            }
                            else
                            {
                                args[i] = Type.Missing;
                            }
                        }
                        _useItemOnClientMethod.Invoke(__instance, args);
                        _staticLogger.LogInfo($"[TAIGU] Called UseItemOnClient with {parameters.Length} parameters");
                    }
                }
                else if (_activateItemMethod != null)
                {
                    var parameters = _activateItemMethod.GetParameters();
                    object[] args = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        args[i] = Type.Missing;
                    }
                    try
                    {
                        _activateItemMethod.Invoke(__instance, args);
                    }
                    catch
                    {
                        _activateItemMethod.Invoke(__instance, null);
                    }
                }
                _timeAtLastAttack = currentTime;

                return false;
            }
            catch
            {
                return true;
            }
        }

        public static void HitKnifePostfix(object __instance)
        {
            try
            {
                if (_timeAtLastDamageDealtField != null)
                {
                    _timeAtLastDamageDealtField.SetValue(__instance, -1f);
                }
            }
            catch
            {
                // Silently fail
            }
        }
    }
}
