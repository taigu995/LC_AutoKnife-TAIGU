using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace AutoKnife
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class AutoKnifePlugin : BaseUnityPlugin
    {
        public const string ModGuid = "TAIGU.AutoKnife";
        public const string ModName = "AutoKnife";
        public const string ModVersion = "1.0.27";

        private Harmony _harmony;
        private static float _timeAtLastAttack = 0f;
        private static float _attackInterval = 0.02f;
        private static BepInEx.Logging.ManualLogSource _staticLogger;
        private ConfigEntry<float> _configAttackInterval;

        // Cached types
        private static Type _playerControllerBType;
        private static Type _knifeItemType;
        private static MethodInfo _useItemOnClientMethod;
        private static MethodInfo _knifeUseItemOnClientMethod;
        private static MethodInfo _activateItemMethod;
        private static MethodInfo _updateMethod;
        private static MethodInfo _hitKnifeMethod;
        private static FieldInfo _currentlyHeldObjectServerField;
        private static FieldInfo _timeAtLastDamageDealtField;

        private void Awake()
        {
            _staticLogger = Logger;

            // Configuration
            _configAttackInterval = Config.Bind<float>(
                "General",
                "AttackInterval",
                0.02f,
                "Attack interval in seconds (lower = faster). Default: 0.02");
            _attackInterval = _configAttackInterval.Value;
            _configAttackInterval.SettingChanged += (s, e) => { _attackInterval = _configAttackInterval.Value; };

            _staticLogger.LogInfo($"[TAIGU] {ModName} v{ModVersion} loading...");
            _staticLogger.LogInfo($"[TAIGU] Attack interval: {_attackInterval}s");

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
                    // Try with bool parameter (V81 signature: UseItemOnClient(bool))
                    _useItemOnClientMethod = _playerControllerBType.GetMethod("UseItemOnClient",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null, new Type[] { typeof(bool) }, null);
                    if (_useItemOnClientMethod != null)
                    {
                        _staticLogger.LogInfo("[TAIGU] Found UseItemOnClient(bool)");
                    }
                }
                if (_useItemOnClientMethod == null)
                {
                    // Try with int parameter
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
                }
                
                if (_useItemOnClientMethod == null && activateItemMethod == null)
                {
                    _staticLogger.LogError($"[TAIGU] No suitable method found");
                    return false;
                }
                
                _activateItemMethod = activateItemMethod;

                // Get UseItemOnClient(bool) on KnifeItem (the actual method that triggers item use)
                _knifeUseItemOnClientMethod = _knifeItemType.GetMethod("UseItemOnClient",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new Type[] { typeof(bool) }, null);
                if (_knifeUseItemOnClientMethod != null)
                {
                    _staticLogger.LogInfo("[TAIGU] Found KnifeItem.UseItemOnClient(bool)");
                }
                else
                {
                    // Try parameterless
                    _knifeUseItemOnClientMethod = _knifeItemType.GetMethod("UseItemOnClient",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (_knifeUseItemOnClientMethod != null)
                    {
                        _staticLogger.LogInfo("[TAIGU] Found KnifeItem.UseItemOnClient()");
                    }
                }

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

        // Cached reflection for new Input System (Mouse.current.leftButton.isPressed)
        private static System.Reflection.PropertyInfo _mouseCurrentProp;
        private static System.Reflection.PropertyInfo _mouseLeftButtonProp;
        private static System.Reflection.PropertyInfo _buttonIsPressedProp;
        private static bool _inputSystemResolved = false;

        private static bool TryResolveInputSystem()
        {
            if (_inputSystemResolved) return _mouseCurrentProp != null;
            _inputSystemResolved = true;
            try
            {
                // Find Unity.InputSystem assembly
                System.Reflection.Assembly inputAssembly = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "Unity.InputSystem")
                    {
                        inputAssembly = asm;
                        break;
                    }
                }
                if (inputAssembly == null)
                {
                    _staticLogger.LogWarning("[TAIGU] Unity.InputSystem assembly not found");
                    return false;
                }
                _staticLogger.LogInfo($"[TAIGU] Found Unity.InputSystem {inputAssembly.GetName().Version}");

                // Mouse type
                var mouseType = inputAssembly.GetType("UnityEngine.InputSystem.Mouse");
                if (mouseType == null)
                {
                    _staticLogger.LogWarning("[TAIGU] Mouse type not found in InputSystem");
                    return false;
                }

                // Mouse.current (static property)
                _mouseCurrentProp = mouseType.GetProperty("current",
                    BindingFlags.Public | BindingFlags.Static);
                if (_mouseCurrentProp == null)
                {
                    _staticLogger.LogWarning("[TAIGU] Mouse.current property not found");
                    return false;
                }

                // Get the actual Mouse instance to find leftButton property type
                var mouseInstance = _mouseCurrentProp.GetValue(null);
                if (mouseInstance == null)
                {
                    _staticLogger.LogWarning("[TAIGU] Mouse.current is null (not initialized yet)");
                    return false;
                }

                // mouse.leftButton - find it on the Mouse class or its base classes
                _mouseLeftButtonProp = mouseType.GetProperty("leftButton",
                    BindingFlags.Public | BindingFlags.Instance);
                if (_mouseLeftButtonProp == null)
                {
                    // Try base types (Mouse -> Pointer -> InputDevice)
                    var baseType = mouseType.BaseType;
                    while (baseType != null && _mouseLeftButtonProp == null)
                    {
                        _mouseLeftButtonProp = baseType.GetProperty("leftButton",
                            BindingFlags.Public | BindingFlags.Instance);
                        baseType = baseType.BaseType;
                    }
                }
                if (_mouseLeftButtonProp == null)
                {
                    _staticLogger.LogWarning("[TAIGU] leftButton property not found on Mouse");
                    return false;
                }

                // leftButton.isPressed - find on ButtonControl or its base types
                var leftButtonInstance = _mouseLeftButtonProp.GetValue(mouseInstance);
                if (leftButtonInstance == null)
                {
                    _staticLogger.LogWarning("[TAIGU] leftButton instance is null");
                    return false;
                }

                var buttonType = leftButtonInstance.GetType();
                _buttonIsPressedProp = buttonType.GetProperty("isPressed",
                    BindingFlags.Public | BindingFlags.Instance);
                if (_buttonIsPressedProp == null)
                {
                    // Try base types (ButtonControl -> InputControl<ButtonState>)
                    var btnBase = buttonType.BaseType;
                    while (btnBase != null && _buttonIsPressedProp == null)
                    {
                        _buttonIsPressedProp = btnBase.GetProperty("isPressed",
                            BindingFlags.Public | BindingFlags.Instance);
                        btnBase = btnBase.BaseType;
                    }
                }
                if (_buttonIsPressedProp == null)
                {
                    _staticLogger.LogWarning("[TAIGU] isPressed property not found on ButtonControl");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _staticLogger.LogWarning($"[TAIGU] Failed to resolve Input System: {ex.Message}");
                return false;
            }
        }

        private static bool IsLeftMousePressed()
        {
            try
            {
                if (!TryResolveInputSystem()) return false;

                var mouse = _mouseCurrentProp.GetValue(null);
                if (mouse == null) return false;

                var leftButton = _mouseLeftButtonProp.GetValue(mouse);
                if (leftButton == null) return false;

                return (bool)_buttonIsPressedProp.GetValue(leftButton);
            }
            catch (Exception ex)
            {
                _staticLogger.LogWarning($"[TAIGU] IsLeftMousePressed error: {ex.Message}");
                return false;
            }
        }

        // Manual patch methods (not using attributes)
        public static bool UpdatePrefix(object __instance)
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

                // Check if left mouse button is held down (new Input System via reflection)
                bool mouseHeld = IsLeftMousePressed();
                if (!mouseHeld)
                {
                    return true;
                }

                // Check if holding a knife
                object heldItem = _currentlyHeldObjectServerField.GetValue(__instance);
                if (heldItem == null) return true;
                if (!_knifeItemType.IsInstanceOfType(heldItem)) return true;

                // Check attack interval
                float currentTime = Time.realtimeSinceStartup;
                if (currentTime - _timeAtLastAttack < _attackInterval)
                {
                    return true;
                }

                // Priority 1: Call UseItemOnClient on the knife item itself
                if (_knifeUseItemOnClientMethod != null)
                {
                    var knifeParams = _knifeUseItemOnClientMethod.GetParameters();
                    if (knifeParams.Length == 1 && knifeParams[0].ParameterType == typeof(bool))
                    {
                        _knifeUseItemOnClientMethod.Invoke(heldItem, new object[] { true });
                    }
                    else if (knifeParams.Length == 0)
                    {
                        _knifeUseItemOnClientMethod.Invoke(heldItem, null);
                    }
                }
                // Priority 2: Call UseItemOnClient on PlayerControllerB
                else if (_useItemOnClientMethod != null)
                {
                    var parameters = _useItemOnClientMethod.GetParameters();
                    if (parameters.Length == 0)
                    {
                        _useItemOnClientMethod.Invoke(__instance, null);
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
                            else if (parameters[i].ParameterType == typeof(bool))
                            {
                                args[i] = true;
                            }
                            else
                            {
                                args[i] = Type.Missing;
                            }
                        }
                        _useItemOnClientMethod.Invoke(__instance, args);
                    }
                }
                else if (_activateItemMethod != null)
                {
                    var parameters = _activateItemMethod.GetParameters();
                    object[] args = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        if (paramType.IsValueType)
                        {
                            args[i] = System.Activator.CreateInstance(paramType);
                        }
                        else
                        {
                            args[i] = null;
                        }
                    }
                    try
                    {
                        _activateItemMethod.Invoke(__instance, args);
                    }
                    catch (Exception ex)
                    {
                        _staticLogger.LogError($"[TAIGU] ActivateItem_performed failed: {ex.InnerException?.Message ?? ex.Message}");
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
