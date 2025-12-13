# CYFramework 2.2

**工业级 Unity 游戏框架** - 一套"可落地"的多平台底座

## 特性

- ✅ **高频零 GC** - 结构体事件 `Post(ref evt)` 零装箱；对象池减少频繁 Instantiate/Destroy（延迟事件派发会产生装箱，建议低频使用）
- ✅ **多平台** - PC / Android / iOS / 微信小游戏 / WebGL
- ✅ **混合架构** - OOP 写逻辑，DOTS 做计算（PC 端可选）
- ✅ **平台适配** - 自动处理微信/WebGL 的 API 限制
- ✅ **统一入口** - `CY.xxx` 直接暴露 Manager，无中间封装
- ✅ **可扩展** - partial class 支持游戏项目扩展自定义系统
- ✅ **开箱即用** - 流程、实体、UI、数据表、音频、存档一应俱全

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
    
    // [OnEvent] 自动订阅事件：由 GameEntryBase 在生命周期中调用 EventBus.SubscribeAll/UnsubscribeAll
    [OnEvent]
    private void OnGameOver(ref GameOverEvent evt)
    {
        Debug.Log($"游戏结束: {evt.IsVictory}");
    }
}

// 流程类加特性自动注册
[AutoRegisterProcedure("Menu", order: 0)]
public class MenuProcedure : ProcedureBase { }

[AutoRegisterProcedure("Battle", order: 1)]
public class BattleProcedure : ProcedureBase { }

// 当你新增/修改流程后：在 Unity 菜单执行
// CYFramework/Generate Procedure Registry
// 生成 Resources/CYFramework/ProcedureRegistry.asset，运行时优先从注册表加载流程（WebGL/微信无需扫程序集）
```

### 3. 使用 CY 统一入口

CY 直接暴露 Manager，可访问全部 API：

```csharp
// 事件（CY.Event 返回 EventBus）
CY.Event.Subscribe<GameEvent>(OnEvent, this);
CY.Event.Post(ref evt);  // 发布事件

// 计时器（CY.Timer 返回 TimerManager）
CY.Timer.Delay(2f, () => Debug.Log("2秒后"));
CY.Timer.Loop(1f, () => Debug.Log("每秒执行"));

// 实体（CY.Entity 返回 EntityManager）
CY.Entity.RegisterEntity("Enemy", enemyPrefab, 20);
var enemy = CY.Entity.ShowEntity<EnemyEntity>("Enemy");
CY.Entity.HideEntity(enemy);
CY.Entity.PauseEntity(enemy.Id);  // 暂停实体

// UI（CY.UI 返回 UIManager）
CY.UI.Open<ShopUI>();
CY.UI.ShowConfirm("提示", "确定吗？", onConfirm, onCancel);
CY.UI.ShowToast("购买成功");

// 数据表（CY.Data 返回 DataTableManager）
CY.Data.LoadFromCsv<MonsterRow>(csvText);
var monster = CY.Data.GetDataTable<MonsterRow>().GetRow(1001);

// 流程（CY.Procedure 返回 ProcedureManager）
CY.Procedure.Change("Battle");
CY.Procedure.ChangeProcedure<BattleProcedure>();

// 音频（CY.Audio 返回 IAudioService）
CY.Audio.PlayBGM("battle");
CY.Audio.PlaySFX("click");
```

### 4. 扩展自定义系统

CY 是 partial class，可在游戏项目中扩展：

```csharp
// Assets/_Game/Scripts/Core/CY.Game.cs
namespace CYFramework
{
    public static partial class CY
    {
        private static QuestManager _quest;
        
        /// <summary>
        /// 任务系统
        /// </summary>
        public static QuestManager Quest => _quest ??= Get<QuestManager>();
    }
}

// 使用
CY.Quest.AcceptQuest(1001);
```

## 核心模块

### CY 统一入口

| 入口 | 类型 | 说明 | 场景 |
|------|------|------|------|
| `CY.Event` | EventBus | 事件系统 | 模块解耦通信 |
| `CY.Timer` | TimerManager | 计时器 | 技能冷却、定时刷怪 |
| `CY.Procedure` | ProcedureManager | 流程管理 | 菜单→战斗→结算 |
| `CY.Entity` | EntityManager | 实体管理 | 敌人、子弹、特效 |
| `CY.UI` | UIManager | UI 面板 | 背包、商店、对话框 |
| `CY.Data` | DataTableManager | 数据表 | 配置表读取 |
| `CY.Audio` | IAudioService | 音频 | BGM、音效 |
| `CY.Save` | SaveService | 存档 | 进度保存 |
| `CY.Pool` | PoolManager | 对象池 | 复用 GameObject |
| `CY.Game` | GameEntryBase | 游戏入口 | 访问全局实例 |

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
| 微信小游戏 | `CY_WECHAT` |
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
