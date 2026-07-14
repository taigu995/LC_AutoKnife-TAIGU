# AutoKnife - 致命公司自动挥刀模组

按住鼠标左键自动快速挥击小刀，同时移除攻击冷却，实现连续快速攻击。

## 功能特性

- **自动挥击**：按住鼠标左键自动连续挥击小刀
- **无冷却**：移除小刀攻击间隔限制
- **可配置**：通过配置文件自定义攻击频率
- **兼容 V81**：适配 Lethal Company V81 版本

## 安装说明

1. 确保已安装 [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases)
2. 将 `AutoKnife.dll` 放入游戏目录的 `BepInEx/plugins/` 文件夹
3. 启动游戏

```
Lethal Company/
├── BepInEx/
│   ├── plugins/
│   │   └── AutoKnife.dll  ← 放置于此
│   └── config/
│       └── TAIGU.AutoKnife.cfg  ← 自动生成
```

## 配置说明

首次启动后，配置文件自动生成在：

```
BepInEx/config/TAIGU.AutoKnife.cfg
```

### 配置项

```ini
[General]
# 攻击间隔（秒），数值越小攻击越快，默认 0.02
AttackInterval = 0.02
```

### 常用配置参考

| 数值 | 效果 |
|------|------|
| `0.01` | 每秒 100 次（极快） |
| `0.02` | 每秒 50 次（默认） |
| `0.05` | 每秒 20 次 |
| `0.1` | 每秒 10 次 |

支持游戏内热修改，保存配置文件后立即生效，无需重启游戏。

## 使用方法

1. 进入游戏，装备小刀
2. 按住鼠标左键即可自动连续挥击
3. 松开左键停止攻击

## 技术信息

- **署名**：TAIGU
- **版本**：1.0.27
- **依赖**：BepInEx 5.x, HarmonyLib
- **适配版本**：Lethal Company V81

### 实现原理

- 通过 Harmony 补丁拦截 `PlayerControllerB.Update`，检测鼠标左键按住状态
- 调用 `KnifeItem.UseItemOnClient(bool)` 触发挥击
- 通过 Harmony 补丁拦截 `KnifeItem.HitKnife`，将 `timeAtLastDamageDealt` 重置为 -1f 移除冷却
- 使用反射访问新版 Input System（`Mouse.current.leftButton.isPressed`）检测鼠标输入

## 致谢

基于以下模组合并开发：
- AutoKnifeAttack v1.0.0 by Yan01h
- FastKnife v0.0.3 by nexor
