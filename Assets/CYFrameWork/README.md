# CYFramework 2.3

**工业级 Unity 游戏框架** - 一套"可落地"的多平台底座

## 特性

- ✅ **零 GC** - 事件系统、对象池全程无装箱
- ✅ **多平台** - PC / Android / iOS / 微信小游戏 / WebGL
- ✅ **混合架构** - OOP 写逻辑，DOTS 做计算（PC 端可选）
- ✅ **平台适配** - 自动处理微信/WebGL 的 API 限制
- ✅ **统一入口** - `CY.Event` / `CY.Timer` / `CY.Entity` 简洁 API
- ✅ **开箱即用** - 流程、实体、数据表、网络、存档、音频一应俱全

## 快速开始

### 1. 安装

将 `CYFramework` 文件夹放入 `Assets/` 目录。

### 2. 创建游戏入口

```csharp
using CYFramework;
using CYFramework.Core;
using CYFramework.Core.Event;

public class MyGame : GameEntryBase
{
    // 开启自动注册流程（扫描 [AutoRegisterProcedure]）
    protected override bool AutoRegisterProcedures => true;
    
    protected override void OnGameInit()
    {
        // 初始化子系统
    }
    
    protected override void OnGameStart()
    {
        CY.Procedure.Start("Menu");  // 按名称启动
    }
    
    // [OnEvent] 自动订阅事件，无需手动 Subscribe
    [OnEvent]
    private void OnGameOver(ref GameOverEvent evt)
    {
        CY.Log.Info($"游戏结束: {evt.IsVictory}");
    }
}

// 流程类加特性自动注册
[AutoRegisterProcedure("Menu", order: 0)]
public class MenuProcedure : ProcedureBase { }

[AutoRegisterProcedure("Battle", order: 1)]
public class BattleProcedure : ProcedureBase { }
```

### 3. 使用 CY 统一入口

```csharp
// 事件（手动订阅 或 [OnEvent] 自动订阅）
CY.Event.Subscribe<GameEvent>(OnEvent, this);
CY.Event.Fire(new GameEvent { Score = 100 });

// 计时器
CY.Timer.Delay(2f, () => Debug.Log("2秒后"));
CY.Timer.Loop(1f, () => Debug.Log("每秒执行"));

// 实体
CY.Entity.Register("Enemy", enemyPrefab, 20);
var enemy = CY.Entity.Show<EnemyEntity>("Enemy");
CY.Entity.Hide(enemy);

// 数据表
CY.Data.LoadCsv<MonsterRow>(csvText);
var monster = CY.Data.GetRow<MonsterRow>(1001);

// 流程（按名称或类型）
CY.Procedure.Change("Battle");
CY.Procedure.Change<BattleProcedure>();
```

## 核心模块

### CY 统一入口

| 入口 | 说明 |
|------|------|
| `CY.Event` | 事件系统 |
| `CY.Timer` | 计时器 |
| `CY.Procedure` | 流程管理 |
| `CY.Entity` | 实体管理 |
| `CY.Data` | 数据表 |
| `CY.Log` | 日志 |
| `CY.Game` | 游戏入口 |

### 核心服务

| 模块 | 说明 | 命名空间 |
|------|------|----------|
| **GameEntryBase** | 游戏入口基类 | `CYFramework.Core` |
| **ProcedureManager** | 流程管理 | `CYFramework.Core.Procedure` |
| **EntityManager** | 实体管理 | `CYFramework.Core.Entity` |
| **DataTableManager** | 数据表管理 | `CYFramework.Core.DataTable` |
| **TimerManager** | 计时器 | `CYFramework.Core.Timer` |
| **FSM** | 有限状态机 | `CYFramework.Core.FSM` |
| **EventBus** | 零 GC 事件 | `CYFramework.Core.Event` |
| **PoolManager** | 对象池 | `CYFramework.Core.Pool` |
| **UIManager** | UI 面板管理 | `CYFramework.Core.UI` |
| **AudioService** | 音频管理 | `CYFramework.Core.Audio` |
| **NetworkService** | HTTP/WebSocket | `CYFramework.Core.Network` |
| **SaveService** | 加密存档 | `CYFramework.Core.Save` |
| **ConfigLoader** | 配置加载 | `CYFramework.Core.Config` |
| **ResourceLoader** | 资源加载 | `CYFramework.Core.Resource` |

## 平台宏定义

在 `Player Settings > Scripting Define Symbols` 添加：

| 平台 | 宏定义 |
|------|--------|
| 微信小游戏 | `CY_WECHAT;CY_SINGLE_THREAD` |
| PC 旗舰版 | `CY_PC;ENABLE_DOTS` |
| 移动端 | `CY_MOBILE` |

## 调试工具

| 工具 | 快捷键 | 功能 |
|------|--------|------|
| RuntimeProfiler | `F1` | FPS、内存、DrawCall 监控 |
| CheatConsole | `` ` `` | 命令控制台 |

## 目录结构

```
Assets/CYFramework/
├── Runtime/
│   ├── CY.cs                 # 统一入口
│   ├── Infrastructure/       # 启动器、服务定位器、生命周期
│   │   ├── CYBootstrap.cs    # 框架驱动器
│   │   ├── ServiceLocator.cs # 依赖注入
│   │   ├── Lifecycle.cs      # 生命周期接口
│   │   └── ServiceBase.cs    # 服务基类
│   ├── Platform/             # 平台适配器
│   ├── Core/                 # 核心服务
│   │   ├── Audio/            # 音频服务
│   │   ├── Config/           # 配置加载
│   │   ├── DataTable/        # 数据表管理
│   │   ├── Entity/           # 实体管理
│   │   ├── Event/            # 事件总线
│   │   ├── FSM/              # 有限状态机
│   │   ├── GameEntry/        # 游戏入口基类
│   │   ├── Pool/             # 对象池
│   │   ├── Procedure/        # 流程管理
│   │   ├── Timer/            # 计时器
│   │   └── UI/               # UI 框架 (MVVM)
│   ├── Gameplay/             # 玩法核心（可选）
│   └── Debug/                # 调试工具
├── Editor/                   # 编辑器工具
└── Documentation/            # 文档
    ├── API_Reference.md
    ├── Usage_Guide.md
    └── Lifecycle_Guide.md    # 生命周期指南
```

## 文档

- [API 参考文档](Documentation/API_Reference.md) - 完整 API 说明
- [使用指南](Documentation/Usage_Guide.md) - 框架教程
- [生命周期指南](Documentation/Lifecycle_Guide.md) - 生命周期详解
- [设计文档](CYFramework.md) - 架构设计白皮书

## 性能目标

| 指标 | 微信/WebGL | Mobile | PC |
|------|-----------|--------|----|
| 帧率 | 45-60 FPS | 60-90 FPS | 60-144 FPS |
| DrawCall | < 100 | < 300 | < 1000 |
| 每帧 GC | 0 | 0 | < 1KB |

## 许可证

MIT License

---

**版本**: 2.3 Unified Entry  
**Unity 版本**: 2021.3 LTS 及以上

