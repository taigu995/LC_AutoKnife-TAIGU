## [1.0.27] - 配置文件支持

### 新增
- 添加 BepInEx 配置文件支持（`BepInEx/config/TAIGU.AutoKnife.cfg`）
- 支持通过修改 `AttackInterval` 配置项自定义攻击频率
- 支持游戏内热修改配置，保存后立即生效

### 变更
- 攻击间隔从硬编码常量改为可配置静态变量

---

## [1.0.26] - 精简日志

### 变更
- 移除每帧调试日志（UpdatePrefix、isPlayerDead、mouseState）
- 移除每次攻击的过程日志（checking held item、triggering attack、Called UseItemOnClient）
- 移除方法枚举列表日志
- 仅保留模组加载、补丁应用、错误等关键日志
- DLL 体积从 19KB 缩减至 14KB

---

## [1.0.25] - 修复攻击触发目标

### 修复
- 修正 `UseItemOnClient(bool)` 方法调用目标：从 PlayerControllerB 改为 KnifeItem
- 在 heldItem（小刀实例）上调用 `UseItemOnClient(true)` 触发实际挥击
- 自动挥击功能现在正常工作

---

## [1.0.24] - 修正方法参数类型

### 修复
- 修正 `UseItemOnClient` 参数类型搜索：从 `int` 改为 `bool`
- V81 中 `UseItemOnClient` 签名为 `UseItemOnClient(Boolean)`
- 调用时传入 `true` 作为参数

---

## [1.0.23] - 诊断攻击触发

### 新增
- 枚举 PlayerControllerB 和 KnifeItem 上所有相关方法并输出到日志
- 添加 `ItemActivate` 回退调用（在 GrabbableObject 上尝试）

### 修复
- 为 `ActivateItem_performed` 构造正确的 `CallbackContext` 结构体参数

---

## [1.0.22] - 修复 CallbackContext 参数

### 修复
- 修复 `ActivateItem_performed` 参数传递错误
- 使用 `Activator.CreateInstance()` 为值类型参数（`CallbackContext`）创建默认实例
- 添加参数类型诊断日志

---

## [1.0.21] - 诊断攻击方法调用

### 新增
- 为 `ActivateItem_performed` 调用添加详细日志（调用前、成功、失败）
- 区分不同失败原因的错误信息

### 诊断结果
- 确认 `ActivateItem_performed` 需要 `InputAction.CallbackContext` 参数
- 发现传入默认 Context 后方法调用成功但无实际效果

---

## [1.0.20] - 切换至新版 Input System

### 修复
- 移除旧版 `Input.GetMouseButton(0)` 调用（V81 已禁用旧版 Input Manager）
- 改用纯反射访问新版 Input System：`Mouse.current.leftButton.isPressed`
- 运行时查找 `Unity.InputSystem` 程序集，避免版本不匹配问题
- 缓存 PropertyInfo 对象，仅首次调用时解析

### 诊断结果
- 确认 V81 使用 Unity Input System 1.14.0.0
- 鼠标检测正常工作

---

## [1.0.19] - 修复 Input 异常捕获

### 修复
- 将 `Input.GetMouseButton(0)` 放入 try-catch 块
- 避免异常阻止后续日志输出和逻辑执行

---

## [1.0.18] - 添加调试日志

### 新增
- 添加 UpdatePrefix 入口调试日志
- 添加 isPlayerDead 和 GetMouseButton 状态日志
- 修复版本号显示问题

---

## [1.0.17] - 修复 Logger 静态引用

### 修复
- 修复静态方法中使用实例属性 Logger 的问题
- 添加 `_staticLogger` 静态字段

---

## [1.0.16] - 修复 BindingFlags

### 修复
- 修正方法搜索的 BindingFlags：从 `NonPublic | Static` 改为 `Public | Static`

---

## [1.0.15] - 添加 HarmonyMethod 空检查

### 修复
- 添加 HarmonyMethod 构造后的空值检查

---

## [1.0.14] - 添加 Update 方法空检查

### 修复
- 添加 `_updateMethod` 空值检查，避免补丁应用时崩溃

---

## [1.0.13] - 添加 Update 方法回退

### 修复
- 添加 LateUpdate / FixedUpdate 回退查找
- 最终确认 V81 中 `PlayerControllerB.Update` 存在

---

## [1.0.12] - 切换至 PatchAll

### 修复
- 从手动 `Patch()` 调用切换为 `PatchAll()`
- 修复 Harmony Patch 方法签名不匹配问题

---

## [1.0.11] - 修复 HarmonyMethod 构造

### 修复
- 修复 `HarmonyMethod` 构造函数：使用 `MethodInfo` 参数而非 `(Type, string)`

---

## [1.0.10] - 切换至手动 Patch

### 修复
- 从特性补丁切换为手动 `Patch()` 调用
- 修复 HarmonyMethod 构造函数不匹配问题

---

## [1.0.9] - 添加 ActivateItem 回退

### 修复
- 添加 `ActivateItem_performed` 作为 `UseItemOnClient` 的回退方法
- 添加 `BindingFlags.NonPublic` 搜索非公开方法

---

## [1.0.8] - 扩展方法搜索范围

### 修复
- 添加 `BindingFlags.NonPublic` 搜索 `UseItemOnClient` 方法
- 解决反射查找方法失败问题

---

## [1.0.7] - 切换至纯反射

### 修复
- 移除所有对游戏类型的直接引用
- 全部改用纯反射机制访问游戏类型
- 解决 Harmony 干扰类型解析导致的 `TypeLoadException`

---

## [1.0.6] - 使用实际游戏 DLL 编译

### 修复
- 使用实际游戏 DLL（Assembly-CSharp.dll、UnityEngine 等）进行编译
- 解决程序集版本不匹配问题

---

## [1.0.5] - 修复 Input 版本不匹配

### 修复
- 修复 `Input.GetMouseButton` 因 `UnityEngine.CoreModule` 版本不匹配导致的 `TypeLoadException`

---

## [1.0.4] - 修复 Mouse.current 类型问题

### 修复
- 修复 `Mouse.current.leftButton.isPressed` 因 InputSystem 版本不匹配导致的 `TypeLoadException`
- 回退使用 `Input.GetMouseButton(0)`

---

## [1.0.3] - 修复 playerInput 字段

### 修复
- 修复 `playerInput` 字段不存在导致的 `MissingFieldException`
- V81 中 PlayerControllerB 和 IngamePlayerSettings 均无 `playerInput` 字段
- 改用 `Mouse.current.leftButton.isPressed` 检测鼠标

---

## [1.0.2] - 移除 isLocalPlayerController

### 修复
- 移除 `isLocalPlayerController` 字段访问
- V81 中 PlayerControllerB 无此字段，导致每帧 `MissingFieldException`

---

## [1.0.1] - 初始修复

### 修复
- 将 `HarmonyPrefix` 改为 `HarmonyPostfix`
- 修正输入动作名从 `"Use"` 改为 `"ActivateItem"`
- 移除不存在的 `ItemActivate` 补丁

---

## [1.0.0] - 初始版本

### 功能
- 创建适配 Lethal Company V81的管家小刀辅助挥动模组

### 已知问题
- 编译时使用桩文件，运行时存在程序集版本不匹配
