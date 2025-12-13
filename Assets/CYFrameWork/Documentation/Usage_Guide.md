# CYFramework 2.2 超详细使用指南

> 本指南假设你是零基础，会一步一步带你理解框架的原理和使用方法。

---

## 目录

1. [框架是什么？解决什么问题？](#1-框架是什么)
2. [框架的核心原理](#2-框架的核心原理)
3. [完整的生命周期流程](#3-完整的生命周期流程)
4. [第一步：让框架跑起来](#4-第一步让框架跑起来)
5. [CY 统一入口](#5-cy-统一入口)
6. [服务定位器详解](#6-服务定位器详解)
7. [事件系统详解](#7-事件系统详解)
8. [流程系统详解](#8-流程系统详解) **[NEW]**
9. [计时器系统详解](#9-计时器系统详解) **[NEW]**
10. [有限状态机详解](#10-有限状态机详解) **[NEW]**
11. [实体系统详解](#11-实体系统详解) **[NEW]**
12. [数据表系统详解](#12-数据表系统详解) **[NEW]**
13. [UI 系统完整教程](#13-ui-系统完整教程)
14. [存档系统详解](#14-存档系统详解)
15. [音频系统详解](#15-音频系统详解)
16. [对象池详解](#16-对象池详解)
17. [网络通信详解](#17-网络通信详解)
18. [玩法核心层详解](#18-玩法核心层详解)
19. [完整项目实战](#19-完整项目实战)
20. [常见问题解答](#20-常见问题解答)

---

## 1. 框架是什么？

### 1.1 为什么需要框架？

想象你要盖房子：
- **没有框架** = 每次都从烧砖头开始
- **有框架** = 直接用现成的砖头、水泥、钢筋

游戏开发中，这些功能几乎每个游戏都需要：
- 播放音效音乐
- 保存读取存档
- 管理 UI 界面
- 发送网络请求
- 对象池优化性能
- 打印调试日志

**CYFramework 就是把这些都封装好了，你直接用就行。**

### 1.2 框架能做什么？

| 我想做的事 | 框架提供的工具 | 一句话说明 |
|-----------|---------------|-----------|
| 播放音效 | `IAudioService` | `audio.PlaySFX("click")` |
| 保存数据 | `SaveService` | `CY.SaveData("player", data)` |
| 管理 UI | `UIManager` | `ui.Open<ShopPanel>()` |
| 发请求 | `NetworkService` | `await network.Get(url)` |
| 对象池 | `PoolManager` | `pool.GetOrCreatePool("Bullet", prefab).Get()` |
| 发事件 | `EventBus` | `eventBus.Post(ref evt)` |
| 打日志 | `CYLog` | `CYLog.Info("消息")` |

### 1.3 框架的分层设计

```
┌─────────────────────────────────────────────────────────┐
│                     你的游戏代码                          │
│         (GameManager, Player, Enemy, UI...)              │
└─────────────────────────────────────────────────────────┘
                           ↓ 调用
┌─────────────────────────────────────────────────────────┐
│                   CYFramework 框架                        │
│  ┌─────────────────────────────────────────────────────┐│
│  │ 表现层: UIManager, AudioService                      ││
│  ├─────────────────────────────────────────────────────┤│
│  │ 核心层: EventBus, SaveService, NetworkService, Pool  ││
│  ├─────────────────────────────────────────────────────┤│
│  │ 基础层: ServiceLocator, CYBootstrap, CYLog           ││
│  ├─────────────────────────────────────────────────────┤│
│  │ 平台层: Unity适配器 / 微信适配器                       ││
│  └─────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────┘
                           ↓ 调用
┌─────────────────────────────────────────────────────────┐
│                     Unity 引擎                           │
└─────────────────────────────────────────────────────────┘
```

---

## 2. 框架的核心原理

### 2.1 服务定位器模式（最重要！）

**问题**：你的代码需要用音频服务、存档服务、UI 服务...怎么拿到它们？

**传统做法（不好）**：
```csharp
// ❌ 到处 Find，性能差，耦合严重
var audio = GameObject.Find("AudioManager").GetComponent<AudioManager>();
```

**框架做法（好）**：
```csharp
// ✅ 统一从"服务中心"获取
var audio = ServiceLocator.Get<IAudioService>();
```

**原理图**：
```
┌─────────────────────────────────────────────┐
│              ServiceLocator                  │
│                (服务中心)                     │
│  ┌─────────────────────────────────────────┐│
│  │ IAudioService  → UnityAudioService      ││
│  │ SaveService    → SaveService            ││
│  │ UIManager      → UIManager              ││
│  │ EventBus       → EventBus               ││
│  │ PoolManager    → PoolManager            ││
│  │ ...                                      ││
│  └─────────────────────────────────────────┘│
└─────────────────────────────────────────────┘
        ↑ Get<T>()              ↑ Register<T>()
        │                       │
   你的代码获取服务          框架启动时注册服务
```

### 2.2 零 GC 事件系统

**问题**：游戏中经常需要"通知"其他模块，比如玩家死亡要通知 UI、音效、成就系统...

**传统做法（耦合严重）**：
```csharp
// ❌ 每加一个系统就要改这里
void OnPlayerDie() {
    uiManager.ShowGameOver();
    audioManager.PlayDeathSound();
    achievementManager.Check();
    // 加新系统还要改...
}
```

**框架做法（解耦）**：
```csharp
// ✅ 只管发事件，谁需要谁订阅
void OnPlayerDie() {
    var evt = new PlayerDiedEvent();
    eventBus.Post(ref evt);  // 发出去就完事了
}

// UI 自己订阅
eventBus.Subscribe<PlayerDiedEvent>((ref PlayerDiedEvent e) => ShowGameOver(), this);

// 音效自己订阅
eventBus.Subscribe<PlayerDiedEvent>((ref PlayerDiedEvent e) => PlayDeathSound(), this);
```

**为什么用 `ref`？**
- 事件是 `struct`（结构体），不是 `class`
- 用 `ref` 传递不会产生内存分配（GC）
- 游戏更流畅，不会卡顿

---

## 3. 完整的生命周期流程

### 3.1 框架启动流程

当你运行游戏，框架按这个顺序启动：

```
游戏启动
    │
    ▼
┌─────────────────────────────────────────┐
│ 1. Unity 加载场景                         │
│    找到 CYBootstrap 组件                  │
└─────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────┐
│ 2. CYBootstrap.Awake()                   │
│    - 设置 DontDestroyOnLoad              │
│    - 调用 InitializeFramework()          │
└─────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────┐
│ 3. InitializeFramework()                 │
│    a) CYLog.Initialize() - 初始化日志     │
│    b) 设置 Time.fixedDeltaTime           │
│    c) RegisterCoreServices() - 注册服务   │
│    d) ServiceLocator.InitializeAll()     │
│    e) 注册异常处理                        │
└─────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────┐
│ 4. RegisterCoreServices()                │
│    按顺序注册：                           │
│    - EventBus                            │
│    - PoolManager                         │
│    - ConfigLoader                        │
│    - ResourceLoader                      │
│    - NetworkService                      │
│    - SaveService                         │
│    - HotUpdateService                    │
│    - AudioService                        │
│    - UIManager                           │
└─────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────┐
│ 5. ServiceLocator.InitializeAll()        │
│    遍历所有 IInitializable 服务            │
│    按 InitOrder 顺序调用 Initialize()     │
└─────────────────────────────────────────┘
    │
    ▼
   框架初始化完成！你可以开始用了
```

### 3.2 每帧更新流程

```
Unity 游戏循环
    │
    ├─────────────────────────────────────────┐
    │                                         │
    ▼                                         ▼
┌───────────────────┐              ┌───────────────────┐
│   FixedUpdate     │              │     Update        │
│   (固定频率)       │              │   (每帧)          │
│                   │              │                   │
│ CYBootstrap 遍历   │              │ CYBootstrap 遍历   │
│ 所有 ITickable    │              │ 所有 IUpdateable  │
│ 调用 Tick(dt)     │              │ 调用 OnUpdate(dt) │
│                   │              │                   │
│ 用途：逻辑计算     │              │ 用途：渲染、输入   │
└───────────────────┘              └───────────────────┘
```

### 3.3 服务生命周期

```
┌─────────────────────────────────────────────────────────┐
│                    服务的一生                            │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  1. 注册 (Register)                                     │
│     └─ ServiceLocator.Register<T>()                     │
│        此时只是"登记"，还没创建实例                        │
│                                                         │
│  2. 创建 (Create)                                       │
│     └─ 第一次 ServiceLocator.Get<T>() 时创建              │
│        或者 InitializeAll() 时创建                       │
│                                                         │
│  3. 初始化 (Initialize)                                 │
│     └─ 如果实现了 IInitializable                         │
│        ServiceLocator.InitializeAll() 会调用             │
│        按 InitOrder 顺序执行                             │
│                                                         │
│  4. 运行中 (Running)                                    │
│     ├─ ITickable.Tick() - 每个 FixedUpdate 调用          │
│     ├─ IUpdateable.OnUpdate() - 每帧调用                 │
│     └─ IPausable.OnPause/OnResume() - 切后台时调用        │
│                                                         │
│  5. 销毁 (Dispose)                                      │
│     └─ ServiceLocator.DisposeAll() 时调用                │
│        按 DisposeOrder 逆序执行                          │
│        游戏退出或场景切换时触发                            │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## 4. 第一步：让框架跑起来

### 4.1 准备工作

1. **确保 CYFramework 文件夹在 Assets 目录下**
   ```
   Assets/
   └── CYFramework/
       ├── Runtime/
       ├── Editor/
       ├── Documentation/
       └── ...
   ```

2. **等待 Unity 编译完成**（没有红色错误）

### 4.2 创建启动场景

**第一步：创建场景**
1. `File > New Scene` 创建新场景
2. 保存为 `Assets/Scenes/Bootstrap.unity`

**第二步：创建启动器对象**
1. 在 Hierarchy 右键 > `Create Empty`
2. 重命名为 `[CYFramework]`
3. 选中它，在 Inspector 点 `Add Component`
4. 搜索 `CYBootstrap`，添加

**第三步：配置 Build Settings**
1. `File > Build Settings`
2. 点 `Add Open Scenes` 添加 Bootstrap 场景
3. 确保它在最上面（Index 0）

### 4.3 运行测试

点击 Play，你应该看到这些日志：

```
=== CYFramework 2.2 启动 ===
平台: WindowsEditor
逻辑帧率: 30Hz
[CYBootstrap] 平台: Native
[CYBootstrap] 核心服务注册完成
[ServiceLocator] 初始化完成: EventBus
[ServiceLocator] 初始化完成: ...
[ServiceLocator] 所有服务初始化完成，共 9 个
=== CYFramework 初始化完成 ===
```

**如果看到这些，恭喜你！框架已经跑起来了！**

### 4.4 验证框架可用

创建一个测试脚本验证：

```csharp
using UnityEngine;
using CYFramework.Infrastructure;

public class FrameworkTest : MonoBehaviour
{
    void Start()
    {
        // 测试日志
        CYLog.Info("Hello CYFramework!");
        
        // 测试获取服务
        var eventBus = ServiceLocator.Get<EventBus>();
        CYLog.Info($"EventBus 获取成功: {eventBus != null}");
        
        // 测试 UI 管理器
        var uiManager = ServiceLocator.Get<UIManager>();
        CYLog.Info($"UIManager 获取成功: {uiManager != null}");
    }
}
```

把这个脚本挂到任意 GameObject 上运行，应该看到：
```
Hello CYFramework!
EventBus 获取成功: True
UIManager 获取成功: True
```

---

## 5. CY 统一入口

### 5.1 什么是 CY 统一入口？

**CY 类**是框架的统一入口，类似 GameFramework 的 GameEntry。它把所有常用功能封装成简单的静态方法，让你不用记忆各种 ServiceLocator.Get<> 调用。

### 5.2 快速上手

```csharp
using CYFramework;  // 只需要这一个命名空间

public class MyGame : MonoBehaviour
{
    void Start()
    {
        // 事件系统
        CY.Event.Subscribe<GameStartEvent>(OnGameStart, this);
        var startEvt = new GameStartEvent { StageId = 1 };
        CY.Event.Post(ref startEvt);
        
        // 日志
        CY.LogInfo("游戏启动");
        CY.LogWarning("这是警告");
        
        // 计时器
        CY.Timer.Delay(2f, () => CY.LogInfo("2秒后执行"));
        CY.Timer.Loop(1f, () => CY.LogInfo("每秒执行一次"));
        
        // 流程切换
        CY.Procedure.Change<BattleProcedure>();
    }
    
    void OnDestroy()
    {
        CY.Event.UnsubscribeAll(this);  // 清理订阅
    }
    
    void OnGameStart(ref GameStartEvent evt)
    {
        CY.LogInfo($"关卡 {evt.StageId} 开始!");
    }
}
```

### 5.3 CY vs ServiceLocator 对比

| 功能 | ServiceLocator 写法 | CY 写法 |
|------|---------------------|---------|
| 事件订阅 | `ServiceLocator.Get<EventBus>().Subscribe(...)` | `CY.Event.Subscribe(...)` |
| 发布事件 | `ServiceLocator.Get<EventBus>().Post(ref evt)` | `CY.Event.Post(ref evt)` |
| 日志 | `CYLog.Info("msg")` | `CY.LogInfo("msg")` |
| 获取服务 | `ServiceLocator.Get<T>()` | `CY.Get<T>()` |

**推荐使用 CY 类**，代码更简洁。

---

## 6. 服务定位器详解

### 6.1 什么是服务？

**服务 = 提供某种功能的类**

比如：
- `IAudioService` 提供播放音频的功能
- `SaveService` 提供保存读取数据的功能
- `UIManager` 提供管理 UI 界面的功能

### 5.2 如何获取服务？

```csharp
using CYFramework.Infrastructure;

// 方法一：直接获取（推荐）
var audio = ServiceLocator.Get<IAudioService>();
audio.PlaySFX("click");

// 方法二：安全获取（不确定服务是否存在时）
if (ServiceLocator.TryGet<IAudioService>(out var audio))
{
    audio.PlaySFX("click");
}
```

### 5.3 服务获取的最佳实践

```csharp
public class MyGameManager : MonoBehaviour
{
    // ✅ 推荐：在类级别缓存服务引用
    private IAudioService _audio;
    private SaveService _save;
    private EventBus _eventBus;
    
    void Start()
    {
        // 在 Start 中获取（框架已初始化完成）
        _audio = ServiceLocator.Get<IAudioService>();
        _save = ServiceLocator.Get<SaveService>();
        _eventBus = ServiceLocator.Get<EventBus>();
    }
    
    void PlayClickSound()
    {
        // 直接使用缓存的引用
        _audio.PlaySFX("click");
    }
}
```

**不要这样做**：
```csharp
void Update()
{
    // ❌ 错误！每帧都获取服务，浪费性能
    var audio = ServiceLocator.Get<IAudioService>();
    if (Input.GetKeyDown(KeyCode.Space))
    {
        audio.PlaySFX("click");
    }
}
```

### 5.4 如何注册自己的服务？

如果你想添加自己的服务：

```csharp
// 第一步：定义接口（可选，但推荐）
public interface IMyService
{
    void DoSomething();
}

// 第二步：实现服务
public class MyService : IMyService, IInitializable, IDisposableEx
{
    public int InitOrder => 100;  // 初始化顺序
    public int DisposeOrder => 100;
    
    public void Initialize()
    {
        CYLog.Info("MyService 初始化");
    }
    
    public void DoSomething()
    {
        CYLog.Info("MyService 做事情");
    }
    
    public void Dispose()
    {
        CYLog.Info("MyService 销毁");
    }
}

// 第三步：在 CYBootstrap.RegisterCoreServices() 中添加
// 打开 CYBootstrap.cs，找到 RegisterCoreServices 方法，添加：
ServiceLocator.Register<IMyService, MyService>();
```

---

## 7. 事件系统详解

### 7.1 为什么需要事件系统？

**场景**：玩家升级了，需要：
- UI 刷新等级显示
- 播放升级音效
- 检查成就
- 记录日志

**没有事件系统（耦合严重）**：
```csharp
// Player.cs
void LevelUp()
{
    level++;
    
    // ❌ Player 需要知道所有相关的系统
    uiManager.RefreshLevel();      // 依赖 UI
    audioManager.PlayLevelUp();    // 依赖音效
    achievementManager.Check();    // 依赖成就
    analyticsManager.Log();        // 依赖统计
    // 每加一个功能就要改这里！
}
```

**有事件系统（解耦）**：
```csharp
// Player.cs - 只管发事件
void LevelUp()
{
    level++;
    
    var evt = new PlayerLevelUpEvent { NewLevel = level };
    _eventBus.Post(ref evt);  // ✅ 发出去就完事了
}

// UI 自己订阅
// AudioManager 自己订阅
// AchievementManager 自己订阅
// 各自独立，互不影响！
```

### 7.2 事件的完整使用流程

**第一步：定义事件（必须是 struct）**

```csharp
// Events.cs - 建议统一放在一个文件

// 玩家等级提升事件
public struct PlayerLevelUpEvent
{
    public int OldLevel;
    public int NewLevel;
}

// 金币变化事件
public struct GoldChangedEvent
{
    public int OldAmount;
    public int NewAmount;
    public int Delta;  // 变化量
}

// 敌人死亡事件
public struct EnemyDiedEvent
{
    public int EnemyId;
    public Vector3 Position;
    public int DropGold;
}
```

**第二步：订阅事件**

```csharp
public class LevelUI : MonoBehaviour
{
    private EventBus _eventBus;
    
    void Start()
    {
        _eventBus = ServiceLocator.Get<EventBus>();
        
        // 订阅事件
        _eventBus.Subscribe<PlayerLevelUpEvent>(OnPlayerLevelUp, this);
    }
    
    // 事件处理方法（注意：参数必须是 ref）
    void OnPlayerLevelUp(ref PlayerLevelUpEvent evt)
    {
        levelText.text = $"Lv.{evt.NewLevel}";
        
        // 显示升级特效
        if (evt.NewLevel > evt.OldLevel)
        {
            ShowLevelUpEffect();
        }
    }
    
    void OnDestroy()
    {
        // ⚠️ 重要！销毁时取消订阅
        _eventBus?.UnsubscribeAll(this);
    }
}
```

**第三步：发布事件**

```csharp
public class Player : MonoBehaviour
{
    private EventBus _eventBus;
    private int _level = 1;
    
    void Start()
    {
        _eventBus = ServiceLocator.Get<EventBus>();
    }
    
    public void GainExp(int amount)
    {
        exp += amount;
        
        if (exp >= expToNextLevel)
        {
            int oldLevel = _level;
            _level++;
            exp -= expToNextLevel;
            
            // 发布事件
            var evt = new PlayerLevelUpEvent
            {
                OldLevel = oldLevel,
                NewLevel = _level
            };
            _eventBus.Post(ref evt);  // ⚠️ 必须用 ref
        }
    }
}
```

### 7.3 事件优先级

订阅时可以指定优先级，数字大的先执行：

```csharp
// 高优先级（先执行）- 比如音效要立即响应
_eventBus.Subscribe<PlayerDiedEvent>(OnDied, this, priority: 100);

// 普通优先级（默认 0）
_eventBus.Subscribe<PlayerDiedEvent>(OnDied, this);

// 低优先级（后执行）- 比如统计可以晚点
_eventBus.Subscribe<PlayerDiedEvent>(OnDied, this, priority: -100);
```

### 7.4 常见错误

```csharp
// ❌ 错误1：忘记用 ref
void OnEvent(PlayerLevelUpEvent evt) { }  // 应该是 ref PlayerLevelUpEvent

// ❌ 错误2：事件用 class
public class MyEvent { }  // 应该是 struct

// ❌ 错误3：忘记取消订阅
void OnDestroy()
{
    // 必须取消订阅，否则会内存泄漏！
    _eventBus.UnsubscribeAll(this);
}

// ❌ 错误4：Post 时忘记 ref
_eventBus.Post(evt);  // 应该是 Post(ref evt)
```

---

## 8. 流程系统详解

### 8.1 为什么需要流程系统？

流程（Procedure）用于管理“游戏主流程状态机”，典型场景：启动 → 菜单 → 战斗 → 结算 → 回菜单。

- 把“主流程切换”从业务代码中抽离，避免到处写 `LoadScene/ShowUI/InitSystem` 的硬编码顺序。
- 支持按名称/按类型切换流程。
- **WebGL/微信平台限制**：不依赖运行期扫程序集，推荐使用流程注册表资产（见 8.3）。

### 8.2 定义流程类

```csharp
using CYFramework;
using CYFramework.Core.Procedure;

[AutoRegisterProcedure("Menu", order: 0)]
public sealed class MenuProcedure : ProcedureBase
{
    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        // 进入菜单：打开菜单 UI、播放 BGM 等
        CY.UI.Open<MainMenuPanel>();
    }

    protected override void OnUpdate(float deltaTime)
    {
        // 轮询：等待玩家点击“开始”按钮，然后切换流程
        // ChangeProcedure<BattleProcedure>();
    }

    protected override void OnLeave(ProcedureBase nextProcedure)
    {
        // 离开菜单：关闭 UI
        CY.UI.Close<MainMenuPanel>();
    }
}

[AutoRegisterProcedure("Battle", order: 1)]
public sealed class BattleProcedure : ProcedureBase
{
    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        // 进入战斗：加载关卡、打开 HUD
        CY.UI.Open<BattleHudPanel>();
    }
}
```

> 注意：`[AutoRegisterProcedure]` 只是一种“标记”。是否能在运行时注册，取决于 `ProcedureManager` 的注册策略（见 8.3）。

### 8.3 注册与启动（强烈推荐：流程注册表）

框架运行时会优先尝试从 `Resources/CYFramework/ProcedureRegistry` 加载流程注册表资产，以避免启动时反射扫程序集。

当你新增/修改流程后，在 Unity 菜单执行：

`CYFramework/Generate Procedure Registry`

会生成：`Assets/CYFramework/Resources/CYFramework/ProcedureRegistry.asset`

### 8.4 启动与切换

```csharp
// 启动第一个流程（按名称）
CY.Procedure.Start("Menu");

// 切换流程（按名称）
CY.Procedure.Change("Battle");

// 切换流程（按类型）
CY.Procedure.ChangeProcedure<BattleProcedure>();
```

---

## 9. 计时器系统详解

Timer 用于“延时/循环/下一帧”调度，不依赖业务侧 MonoBehaviour 的协程。

### 9.1 延时（Delay）

```csharp
// 2 秒后执行
CY.Timer.Delay(2f, () => CY.LogInfo("2 秒后执行"));

// 使用不受 Time.timeScale 影响的时间
CY.Timer.Delay(2f, () => CY.LogInfo("Unscaled Delay"), useUnscaledTime: true);
```

### 9.2 循环（Loop）

```csharp
// 每 1 秒执行一次
var timer = CY.Timer.Loop(1f, () => CY.LogInfo("每秒执行一次"));

// 可暂停/恢复/停止（Timer 对象）
timer.Pause();
timer.Resume();
timer.Stop();
```

### 9.3 进度回调（OnUpdate）

```csharp
CY.Timer.Delay(3f, () => CY.LogInfo("完成"))
    .OnUpdate(progress01 =>
    {
        // progress01: 0~1（线性）
    });
```

### 9.4 下一帧（NextFrame）

```csharp
CY.Timer.NextFrame(() => CY.LogInfo("下一帧执行"));
```

---

## 10. 有限状态机详解

FSM 适合管理“局部状态”（如角色状态、AI 状态、UI 子状态），与流程系统（主流程）分工明确。

### 10.1 定义状态枚举

```csharp
public enum PlayerState
{
    Idle,
    Move,
    Attack
}
```

### 10.2 定义状态类

```csharp
using CYFramework.Core.FSM;
using UnityEngine;

public sealed class PlayerIdleState : StateBase<PlayerState>
{
    public override PlayerState StateType => PlayerState.Idle;

    public override void OnEnter()
    {
        Debug.Log("进入 Idle");
    }

    public override void OnUpdate(float deltaTime)
    {
        // 满足条件后切换：ChangeState(PlayerState.Move);
    }
}
```

### 10.3 创建与驱动

`FSMManager` 会被框架自动驱动更新，你只需要创建 FSM、注册状态并启动：

```csharp
using CYFramework;
var fsm = CY.FSM.Create<PlayerState>("PlayerFSM");
fsm.AddState(new PlayerIdleState());
fsm.Start(PlayerState.Idle);
```

---

## 11. 实体系统详解

### 11.1 实体系统架构

```
┌─────────────────────────────────────────────────────────┐
│                   EntityManager                          │
│                   (实体管理器)                            │
│  ┌─────────────────────────────────────────────────────┐│
│  │ 职责：                                               ││
│  │ - 显示/隐藏实体                                       ││
│  │ - 实体对象池                                          ││
│  │ - 分组管理                                            ││
│  │ - 暂停/恢复（支持单个、分组、全部）                    ││
│  └─────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────┘
                           │
                           │ 管理
                           ▼
┌─────────────────────────────────────────────────────────┐
│                      EntityBase                          │
│                     (实体基类)                           │
│  ┌─────────────────────────────────────────────────────┐│
│  │ 生命周期：                                           ││
│  │   OnEntityInit(userData)       创建/从池取出         ││
│  │   OnEntityShow(userData)       显示                  ││
│  │   OnEntityFixedUpdate(dt)      固定帧（物理/AI）      ││
│  │   OnEntityUpdate(dt)           每帧更新              ││
│  │   OnEntityLateUpdate(dt)       延迟更新              ││
│  │   OnEntityPause()              暂停（停止移动）       ││
│  │   OnEntityResume()             恢复                  ││
│  │   OnEntityHide()               隐藏                  ││
│  │   OnEntityRecycle()            回收到池              ││
│  └─────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────┘
```

### 11.2 实体暂停说明

**暂停时**：
- `OnEntityFixedUpdate` 不调用 → 物理/AI 停止
- `OnEntityUpdate` 不调用 → 移动逻辑停止
- `OnEntityLateUpdate` 不调用 → 跟随逻辑停止
- **动画继续播放**（Animator 由 Unity 驱动，不受影响）

**使用场景**：
- 冻结敌人但玩家继续移动
- 技能效果：时间停止

### 11.3 完整 API

```csharp
// 显示实体
var enemy = CY.Entity.ShowEntity<Enemy>("Enemy", enemyData);

// 隐藏实体
CY.Entity.HideEntity(enemy.Id);
CY.Entity.HideEntity(enemy);
CY.Entity.HideAllEntities("Enemy");  // 隐藏所有敌人
CY.Entity.HideAllEntities();         // 隐藏全部

// 暂停/恢复 - 单个
CY.Entity.PauseEntity(entityId);
CY.Entity.ResumeEntity(entityId);

// 暂停/恢复 - 分组（按类型）
CY.Entity.PauseEntities("Enemy");    // 暂停所有敌人
CY.Entity.ResumeEntities("Enemy");   // 恢复所有敌人

// 暂停/恢复 - 全部
CY.Entity.PauseAllEntities();
CY.Entity.ResumeAllEntities();

// 查询
var entity = CY.Entity.GetEntity(entityId);
var enemies = CY.Entity.GetEntities("Enemy");
int count = CY.Entity.GetEntityCount("Enemy");
bool exists = CY.Entity.HasEntity(entityId);
```

### 11.4 实体示例

```csharp
public class Enemy : EntityBase
{
    public override string EntityType => "Enemy";
    
    private float _speed = 5f;
    private Animator _animator;
    
    protected override void OnEntityInit(object userData)
    {
        _animator = GetComponent<Animator>();
    }
    
    protected override void OnEntityShow(object userData)
    {
        var data = userData as EnemyData;
        _speed = data?.Speed ?? 5f;
        _animator.Play("Walk");
    }
    
    protected override void OnEntityUpdate(float deltaTime)
    {
        // 暂停时不执行（IsPaused = true）
        transform.Translate(Vector3.forward * _speed * deltaTime);
    }
    
    protected override void OnEntityPause()
    {
        // 可选：切换到待机动画
        _animator.Play("Idle");
    }
    
    protected override void OnEntityResume()
    {
        // 可选：恢复行走动画
        _animator.Play("Walk");
    }
    
    protected override void OnEntityHide()
    {
        // 清理状态
    }
}
```

---

## 12. 数据表系统详解

数据表用于管理“只读配置”（怪物、道具、技能、关卡等）。框架提供 `DataTableManager`，支持从 CSV 文本加载。

### 12.1 定义数据行（必须实现 IDataRow）

```csharp
using CYFramework.Core.DataTable;

// CSV 示例：Id,Name,Hp,Speed
public sealed class MonsterRow : IDataRow
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    public int Hp { get; private set; }
    public float Speed { get; private set; }

    public void ParseRow(string[] values)
    {
        // values[0] = Id, values[1] = Name, ...
        Id = int.Parse(values[0]);
        Name = values[1];
        Hp = int.Parse(values[2]);
        Speed = float.Parse(values[3]);
    }
}
```

### 12.2 加载与读取

```csharp
using CYFramework;
using UnityEngine;

// 从 Resources 加载 CSV 文本
var csvText = Resources.Load<TextAsset>("Config/Monster").text;
CY.Data.LoadFromCsv<MonsterRow>(csvText);

// 读取数据
var table = CY.Data.GetDataTable<MonsterRow>();
var monster = table.GetRow(1001);
if (monster != null)
{
    CYLog.Info($"怪物：{monster.Name} HP={monster.Hp} Speed={monster.Speed}");
}
```

### 12.3 条件查询（注意 GC）

`GetRows(...)` 会分配新 List；高频路径建议使用 `GetRowsNonAlloc(...)` 复用 List：

```csharp
using System.Collections.Generic;

var table = CY.Data.GetDataTable<MonsterRow>();
var result = new List<MonsterRow>(64);
table.GetRowsNonAlloc(row => row.Hp > 1000, result);
```

---

## 13. UI 系统完整教程

### 13.1 UI 系统架构

```
┌─────────────────────────────────────────────────────────┐
│                      UIManager                           │
│                     (UI 管理器)                           │
│  ┌─────────────────────────────────────────────────────┐│
│  │ 职责：                                               ││
│  │ - 打开/关闭面板                                       ││
│  │ - 管理面板栈（支持返回）                               ││
│  │ - 面板层级排序                                        ││
│  │ - 面板对象池                                          ││
│  │ - 预加载面板                                          ││
│  └─────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────┘
                           │
                           │ 管理
                           ▼
┌─────────────────────────────────────────────────────────┐
│                       UIPanel                            │
│                      (面板基类)                           │
│  ┌─────────────────────────────────────────────────────┐│
│  │ 生命周期及调用时机：                                  ││
│  │                                                     ││
│  │ OnInit(userData)       首次创建 或 从对象池取出时     ││
│  │ OnBindUI()             OnInit 之后，绑定按钮事件      ││
│  │ OnOpen(userData)       面板打开，初始化数据           ││
│  │ OnShow()               从隐藏状态恢复显示             ││
│  │ OnUpdate(dt,realDt)    每帧调用（打开状态）           ││
│  │ OnLateUpdate(dt,realDt)每帧延迟调用（打开状态）       ││
│  │ OnPause()              新面板覆盖当前面板时           ││
│  │ OnResume()             覆盖面板关闭，恢复栈顶时       ││
│  │ OnRefresh(userData)    已打开状态再次 Open 时        ││
│  │ OnHide()               面板隐藏（不关闭）时           ││
│  │ OnClose(isShutdown,ud) 面板关闭时                    ││
│  │ OnUnbindUI()           OnClose 之后，解绑按钮事件     ││
│  │ OnRecycle()            回收到对象池，等待复用         ││
│  └─────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────┘
                           │
                           │ 继承
                           ▼
┌─────────────────────────────────────────────────────────┐
│              你的具体面板类                               │
│      MainPanel, ShopPanel, SettingsPanel...             │
└─────────────────────────────────────────────────────────┘
```

### 13.2 UI 生命周期完整流程

```
═══════════════════════════════════════════════════════════════════
                    首次打开面板 A
═══════════════════════════════════════════════════════════════════

CY.UI.Open<PanelA>(data)
         │
         ▼
    ┌─────────┐
    │ 创建面板 │ (或从对象池取出)
    └────┬────┘
         │
         ▼
┌─────────────────┐
│ OnInit(data)    │  ← 缓存组件引用、重置状态
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ OnBindUI()      │  ← 绑定按钮点击事件
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ OnOpen(data)    │  ← 业务初始化、刷新 UI
└────────┬────────┘
         │
         ▼
    【面板运行中】
         │
    ┌────┴────┐
    │ 每帧循环 │
    └────┬────┘
         │
    ┌────▼────┐
    │OnUpdate │ → │OnLateUpdate│
    └─────────┘

═══════════════════════════════════════════════════════════════════
                 打开新面板 B 覆盖 A
═══════════════════════════════════════════════════════════════════

CY.UI.Open<PanelB>(data)
         │
         ├───────────────────────┐
         │                       │
         ▼                       ▼
┌─────────────────┐     ┌─────────────────┐
│ A.OnPause()     │     │ B.OnInit(data)  │
│ (A 被覆盖暂停)   │     │ B.OnBindUI()    │
└─────────────────┘     │ B.OnOpen(data)  │
                        └─────────────────┘

═══════════════════════════════════════════════════════════════════
                      关闭面板 B
═══════════════════════════════════════════════════════════════════

CY.UI.Close<PanelB>()
         │
         ├───────────────────────┐
         │                       │
         ▼                       ▼
┌─────────────────┐     ┌─────────────────┐
│ B.OnClose()     │     │ A.OnResume()    │
│ B.OnUnbindUI()  │     │ (A 恢复栈顶)    │
│ B.OnRecycle()   │     └─────────────────┘
│ (回收到对象池)   │
└─────────────────┘

═══════════════════════════════════════════════════════════════════
                  再次打开已打开的 A
═══════════════════════════════════════════════════════════════════

CY.UI.Open<PanelA>(newData)  // A 已经打开
         │
         ▼
┌─────────────────┐
│ A.OnRefresh()   │  ← 只刷新数据，不重新 Init/Open
└─────────────────┘

═══════════════════════════════════════════════════════════════════
                    隐藏/显示面板（不关闭）
═══════════════════════════════════════════════════════════════════

panel.InternalHide()          panel.InternalShow()
         │                            │
         ▼                            ▼
┌─────────────────┐          ┌─────────────────┐
│ OnHide()        │          │ OnShow()        │
│ SetActive(false)│          │ SetActive(true) │
└─────────────────┘          └─────────────────┘

═══════════════════════════════════════════════════════════════════
                      关闭面板 A
═══════════════════════════════════════════════════════════════════

CY.UI.Close<PanelA>()
         │
         ▼
┌─────────────────┐
│ OnClose()       │  ← 清理资源
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ OnUnbindUI()    │  ← 解绑按钮事件
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ OnRecycle()     │  ← 回收到对象池（如果 IsPoolable）
└─────────────────┘   或 Destroy（如果不可池化）
```

### 13.3 UI 层级说明

```
┌─────────────────────────────────────────────────────────┐
│  System (600)    │ 系统级弹窗，如崩溃提示                  │
├──────────────────┼──────────────────────────────────────┤
│  Loading (500)   │ 加载界面，全屏遮挡                     │
├──────────────────┼──────────────────────────────────────┤
│  Guide (400)     │ 新手引导                              │
├──────────────────┼──────────────────────────────────────┤
│  Tips (300)      │ Toast 提示                           │
├──────────────────┼──────────────────────────────────────┤
│  Popup (200)     │ 弹窗，如确认框、商店                    │
├──────────────────┼──────────────────────────────────────┤
│  Main (100)      │ 主界面，如主菜单、战斗 HUD              │
├──────────────────┼──────────────────────────────────────┤
│  Background (0)  │ 背景层                                │
└─────────────────────────────────────────────────────────┘
        ↑ 越上面层级越高，显示在更前面
```

### 13.4 创建你的第一个 UI 面板

**第一步：创建预制体**

1. 在场景中创建 UI：
   - 右键 Hierarchy > UI > Canvas（如果没有的话）
   - 在 Canvas 下右键 > UI > Panel
   
2. 设计你的 UI：
   - 添加按钮、文本等
   - 记住给需要代码控制的 UI 起个名字

3. 保存为预制体：
   - 把 Panel 拖到 `Resources/UI/Panels/` 文件夹
   - 命名为 `MainMenuPanel`
   
4. 删除场景中的 Panel（已保存为预制体）

**第二步：创建面板脚本**

```csharp
using UnityEngine;
using UnityEngine.UI;
using CYFramework.Infrastructure;
using CYFramework.Core.UI;

// 指定预制体路径（相对于 Resources）
[UIPrefab("UI/Panels/MainMenuPanel")]
public class MainMenuPanel : UIPanel
{
    // ========== UI 引用 ==========
    [Header("按钮")]
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _quitButton;
    
    [Header("文本")]
    [SerializeField] private Text _versionText;
    
    // ========== 属性配置 ==========
    
    // 设置层级为主界面层
    public override UILayer Layer => UILayer.Main;
    
    // 允许对象池复用
    public override bool IsPoolable => true;
    
    // ========== 生命周期方法 ==========
    
    /// <summary>
    /// 绑定 UI 事件
    /// 在这里添加按钮点击监听
    /// </summary>
    protected override void OnBindUI()
    {
        base.OnBindUI();
        
        _startButton.onClick.AddListener(OnStartClicked);
        _settingsButton.onClick.AddListener(OnSettingsClicked);
        _quitButton.onClick.AddListener(OnQuitClicked);
    }
    
    /// <summary>
    /// 解绑 UI 事件
    /// 在这里移除所有监听
    /// </summary>
    protected override void OnUnbindUI()
    {
        base.OnUnbindUI();
        
        _startButton.onClick.RemoveListener(OnStartClicked);
        _settingsButton.onClick.RemoveListener(OnSettingsClicked);
        _quitButton.onClick.RemoveListener(OnQuitClicked);
    }
    
    /// <summary>
    /// 面板显示时调用
    /// 在这里初始化数据、刷新 UI
    /// </summary>
    protected override void OnShow(object data)
    {
        // 显示版本号
        _versionText.text = $"v{Application.version}";
        
        // 播放 BGM
        var audio = ServiceLocator.Get<IAudioService>();
        audio.PlayBGM("bgm_menu");
    }
    
    /// <summary>
    /// 面板隐藏时调用
    /// 在这里清理状态
    /// </summary>
    protected override void OnHide()
    {
        // 可以在这里做清理工作
    }
    
    // ========== 按钮事件 ==========
    
    private void OnStartClicked()
    {
        PlayClickSound();
        
        // 关闭当前面板
        CloseSelf();
        
        // 打开游戏场景（你的逻辑）
        CYLog.Info("开始游戏！");
    }
    
    private void OnSettingsClicked()
    {
        PlayClickSound();
        
        // 打开设置面板
        Manager.Open<SettingsPanel>();
    }
    
    private void OnQuitClicked()
    {
        PlayClickSound();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    
    private void PlayClickSound()
    {
        var audio = ServiceLocator.Get<IAudioService>();
        audio.PlaySFX("sfx_click");
    }
}
```

**第三步：在预制体上挂载脚本**

1. 打开 `Resources/UI/Panels/MainMenuPanel.prefab`
2. 在根节点添加 `MainMenuPanel` 脚本
3. 把按钮、文本拖到脚本对应的字段上
4. 保存预制体

**第四步：打开面板**

```csharp
// 在任何地方打开面板
var uiManager = ServiceLocator.Get<UIManager>();
uiManager.Open<MainMenuPanel>();
```

### 13.5 面板之间传递数据

```csharp
// 定义数据类
public class ShopPanelData
{
    public int PlayerGold;
    public List<ShopItem> Items;
}

// 打开时传递数据
var data = new ShopPanelData
{
    PlayerGold = player.Gold,
    Items = shopItems
};
uiManager.Open<ShopPanel>(data);

// 在 ShopPanel 中接收
protected override void OnShow(object data)
{
    var shopData = data as ShopPanelData;
    if (shopData != null)
    {
        _goldText.text = shopData.PlayerGold.ToString();
        RefreshItemList(shopData.Items);
    }
}
```

### 13.6 使用通用组件

#### Toast 提示

```csharp
using CYFramework.Core.UI.Components;

// 普通提示
UIToast.Show("操作成功");

// 成功提示（绿色）
UIToast.ShowSuccess("购买成功！");

// 错误提示（红色）
UIToast.ShowError("网络连接失败");

// 警告提示（黄色）
UIToast.ShowWarning("余额不足");

// 自定义显示时间
UIToast.Show("这条消息显示 5 秒", 5f);
```

#### 对话框

```csharp
using CYFramework.Core.UI.Components;

// 提示框（仅确认按钮）
UIDialog.Alert("你的账号已过期", "提示", () => {
    CYLog.Info("用户点击了确认");
});

// 确认框（确认 + 取消）
UIDialog.Confirm(
    "确定要删除这个存档吗？",
    onConfirm: () => {
        // 删除存档（示例 Key：player）
        CY.DeleteSave("player");
        UIToast.Show("删除成功");
    },
    onCancel: () => {
        CYLog.Info("用户取消了删除");
    },
    title: "确认删除"
);

// 输入框
UIDialog.Input(
    "请输入你的角色名",
    onConfirm: (name) => {
        player.Name = name;
        UIToast.Show($"欢迎，{name}！");
    },
    defaultValue: "玩家1",
    title: "创建角色"
);
```

#### Loading 加载界面

```csharp
using CYFramework.Core.UI.Components;

// 显示 Loading
UILoading.Show("正在加载资源...");

// 更新进度 (0-1)
UILoading.Progress(0.3f);
UILoading.Progress(0.6f);
UILoading.Progress(1.0f);

// 更新提示文字
UILoading.Tips("正在初始化游戏...");

// 隐藏 Loading
UILoading.Hide();

// 配合协程使用
IEnumerator LoadGameAsync()
{
    UILoading.Show("加载中...");
    
    // 加载场景
    var operation = SceneManager.LoadSceneAsync("GameScene");
    while (!operation.isDone)
    {
        UILoading.Progress(operation.progress);
        yield return null;
    }
    
    UILoading.Hide();
}
```

### 13.7 MVVM 数据绑定

当面板数据经常变化时，使用 MVVM 模式：

**第一步：创建 ViewModel**

```csharp
using CYFramework.Core.UI.MVVM;

public class PlayerInfoViewModel : ViewModel
{
    // 属性名常量
    public const string PROP_NAME = "Name";
    public const string PROP_LEVEL = "Level";
    public const string PROP_HP = "HP";
    public const string PROP_MAX_HP = "MaxHP";
    public const string PROP_GOLD = "Gold";
    
    // 属性
    public string Name
    {
        get => GetProperty<string>(PROP_NAME, "玩家");
        set => SetProperty(PROP_NAME, value);
    }
    
    public int Level
    {
        get => GetProperty<int>(PROP_LEVEL, 1);
        set => SetProperty(PROP_LEVEL, value);
    }
    
    public int HP
    {
        get => GetProperty<int>(PROP_HP, 100);
        set => SetProperty(PROP_HP, value);
    }
    
    public int MaxHP
    {
        get => GetProperty<int>(PROP_MAX_HP, 100);
        set => SetProperty(PROP_MAX_HP, value);
    }
    
    public int Gold
    {
        get => GetProperty<int>(PROP_GOLD, 0);
        set => SetProperty(PROP_GOLD, value);
    }
    
    // 计算属性（只读）
    public float HPPercent => MaxHP > 0 ? (float)HP / MaxHP : 0f;
}
```

**第二步：创建 MVVM 面板**

```csharp
using CYFramework.Core.UI;
using CYFramework.Core.UI.MVVM;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInfoPanel : MVVMPanel<PlayerInfoViewModel>
{
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _levelText;
    [SerializeField] private Slider _hpBar;
    [SerializeField] private Text _hpText;
    [SerializeField] private Text _goldText;
    
    protected override void OnShow(object data)
    {
        // 初始刷新
        RefreshAll();
    }
    
    // 响应 ViewModel 属性变更
    protected override void OnViewModelPropertyChanged(string propertyName, object oldValue, object newValue)
    {
        switch (propertyName)
        {
            case PlayerInfoViewModel.PROP_NAME:
                _nameText.text = (string)newValue;
                break;
                
            case PlayerInfoViewModel.PROP_LEVEL:
                _levelText.text = $"Lv.{newValue}";
                break;
                
            case PlayerInfoViewModel.PROP_HP:
            case PlayerInfoViewModel.PROP_MAX_HP:
                RefreshHP();
                break;
                
            case PlayerInfoViewModel.PROP_GOLD:
                _goldText.text = $"金币: {newValue}";
                break;
        }
    }
    
    private void RefreshAll()
    {
        _nameText.text = ViewModel.Name;
        _levelText.text = $"Lv.{ViewModel.Level}";
        _goldText.text = $"金币: {ViewModel.Gold}";
        RefreshHP();
    }
    
    private void RefreshHP()
    {
        _hpBar.value = ViewModel.HPPercent;
        _hpText.text = $"{ViewModel.HP}/{ViewModel.MaxHP}";
    }
}
```

**第三步：修改 ViewModel，UI 自动更新**

```csharp
// 获取面板
var panel = uiManager.Get<PlayerInfoPanel>();

// 修改 ViewModel 的属性，UI 会自动更新！
panel.ViewModel.Gold += 100;  // 金币显示自动刷新
panel.ViewModel.Level++;       // 等级显示自动刷新
panel.ViewModel.HP -= 20;      // 血条自动刷新
```

---

## 14. 存档系统详解

### 14.1 基本使用

```csharp
using System;
using System.Collections.Generic;
using CYFramework;
using CYFramework.Core.Save;

// 定义存档数据（注意：存档类型必须继承 SaveDataBase）
[Serializable]
public sealed class PlayerSaveData : SaveDataBase
{
    public string Name = "玩家";
    public int Level = 1;
    public int Gold = 0;
    public List<int> UnlockedItems = new List<int>();
}

// 读取（不存在会返回 new T()）
var data = CY.LoadData<PlayerSaveData>("player");
data.Level = 10;
data.Gold = 5000;

// 保存
CY.SaveData("player", data);

// 检查存档是否存在
if (CY.HasSave("player"))
{
    CYLog.Info($"等级: {data.Level}, 金币: {data.Gold}");
}

// 删除存档
CY.DeleteSave("player");
```

### 14.2 完整的存档管理器示例

```csharp
public class GameSaveManager
{
    private static GameSaveManager _instance;
    public static GameSaveManager Instance => _instance ??= new GameSaveManager();
    
    private const string PlayerKey = "player_save";

    private PlayerSaveData _playerData;
    public PlayerSaveData Data => _playerData;
    
    private GameSaveManager()
    {
        _playerData = new PlayerSaveData();
    }
    
    /// <summary>
    /// 加载存档
    /// </summary>
    public bool Load()
    {
        if (CY.HasSave(PlayerKey))
        {
            _playerData = CY.LoadData<PlayerSaveData>(PlayerKey);
            CYLog.Info($"存档加载成功，等级: {_playerData.Level}");
            return true;
        }
        
        // 没有存档，创建新的
        _playerData = new PlayerSaveData();
        CYLog.Info("创建新存档");
        return false;
    }
    
    /// <summary>
    /// 保存存档
    /// </summary>
    public void Save()
    {
        CY.SaveData(PlayerKey, _playerData);
        CYLog.Info("存档保存成功");
    }
    
    /// <summary>
    /// 删除存档（重新开始）
    /// </summary>
    public void Reset()
    {
        CY.DeleteSave(PlayerKey);
        _playerData = new PlayerSaveData();
        CYLog.Info("存档已重置");
    }
}
```

---

## 15. 音频系统详解

### 15.1 基本使用

```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Audio;

// 获取音频服务
var audio = ServiceLocator.Get<IAudioService>();

// 播放 BGM
audio.PlayBGM("bgm_battle");         // 默认音量，循环
audio.PlayBGM("bgm_boss", 0.8f);     // 自定义音量
audio.PlayBGM("bgm_intro", 1f, false); // 不循环

// 停止 BGM
audio.StopBGM();       // 立即停止
audio.StopBGM(1.0f);   // 1秒淡出

// 暂停/恢复 BGM
audio.PauseBGM();
audio.ResumeBGM();

// 播放音效
audio.PlaySFX("sfx_click");
audio.PlaySFX("sfx_explosion", 0.5f); // 自定义音量

// 音量控制
audio.SetMasterVolume(0.8f);  // 主音量
audio.SetBGMVolume(0.6f);     // BGM 音量
audio.SetSFXVolume(1.0f);     // 音效音量

// 静音
audio.Mute(true);   // 静音
audio.Mute(false);  // 取消静音
```

### 15.2 音频资源放置

```
Resources/
└── Audio/
    ├── BGM/
    │   ├── bgm_menu.mp3
    │   ├── bgm_battle.mp3
    │   └── bgm_boss.mp3
    └── SFX/
        ├── sfx_click.wav
        ├── sfx_explosion.wav
        └── sfx_coin.wav
```

---

## 16. 对象池详解

### 16.1 为什么需要对象池？

**问题**：频繁创建销毁对象会产生 GC（垃圾回收），导致游戏卡顿

**解决**：用完的对象不销毁，放回"池子"里，下次需要时取出来复用

### 16.2 使用 GameObject 池

```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Pool;
using UnityEngine;

public class BulletSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _bulletPrefab;
    
    private PoolManager _pool;
    private GameObjectPool _bulletPool;
    
    void Start()
    {
        _pool = ServiceLocator.Get<PoolManager>();
        
        // 获取或创建子弹池（不存在则创建），预热 20 个
        _bulletPool = _pool.GetOrCreatePool("Bullet", _bulletPrefab, new PoolConfig
        {
            WarmupCount = 20
        });
    }
    
    public void Fire(Vector3 position, Vector3 direction)
    {
        // 从池中获取子弹
        var bullet = _bulletPool.Get(position, Quaternion.LookRotation(direction));
        
        // 设置子弹方向...
        bullet.GetComponent<Bullet>().SetDirection(direction);
    }
    
    public void RecycleBullet(GameObject bullet)
    {
        // 回收子弹到池中
        _bulletPool.Return(bullet);
    }
}
```

### 16.3 使用数据对象池

```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Pool;

// 定义可池化的数据类
public class DamageData : IPoolable
{
    public int Damage;
    public int SourceId;
    public int TargetId;
    
    public void OnSpawn()
    {
        // 从池中取出时调用
    }
    
    public void OnDespawn()
    {
        // 归还池时调用，重置状态
        Damage = 0;
        SourceId = 0;
        TargetId = 0;
    }
}

// 使用
var poolManager = ServiceLocator.Get<PoolManager>();

// 获取或创建池（不存在则创建）
var damagePool = poolManager.GetOrCreatePool(() => new DamageData(), new PoolConfig
{
    InitialCapacity = 50,
    MaxCapacity = 200,
    WarmupCount = 50
});

// 获取
var dmg = damagePool.Get();
dmg.Damage = 100;
dmg.SourceId = 1;
dmg.TargetId = 2;

// 处理完后归还
damagePool.Return(dmg);
```

---

## 17. 网络通信详解

### 17.1 HTTP 请求

```csharp
using System.Collections.Generic;
using CYFramework.Infrastructure;
using CYFramework.Core.Network;
using UnityEngine;

var network = ServiceLocator.Get<NetworkService>();

// GET 请求
var playerResp = await network.Get("/api/player/123");
if (playerResp.IsSuccess)
{
    var playerData = JsonUtility.FromJson<PlayerData>(playerResp.Data);
}
else
{
    CYLog.Warning($"GET 失败: {playerResp.StatusCode} {playerResp.Error}");
}

// POST 请求
var loginRequest = new LoginRequest { username = "test", password = "123" };
var loginJson = JsonUtility.ToJson(loginRequest);
var loginResp = await network.Post("/api/login", loginJson, "application/json");
if (loginResp.IsSuccess)
{
    var loginResult = JsonUtility.FromJson<LoginResponse>(loginResp.Data);
}

// 注意：当前 NetworkService 示例 API 未暴露“自定义请求头”参数；
// 若业务确实需要 Header，请在 NetworkService 内扩展（保持平台适配层不泄漏到业务层）。
```

### 17.2 WebSocket

```csharp
// 连接
await network.ConnectWebSocket("wss://game.server.com/ws");

// 监听消息
network.OnMessage += (message) => {
    var data = JsonUtility.FromJson<ServerMessage>(message);
    HandleMessage(data);
};

// 监听状态变化
network.OnStateChanged += (state) =>
{
    CYLog.Info($"WS 状态: {state}");
};

// 发送消息
var cmd = new MoveCommand { x = 1, y = 2 };
network.SendWebSocket(JsonUtility.ToJson(cmd));

// 断开
network.CloseWebSocket();
```

---

## 18. 玩法核心层详解

### 18.1 逻辑帧与渲染帧分离

```
┌─────────────────────────────────────────────────────────┐
│                      游戏循环                            │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  FixedUpdate (30Hz)          Update (60-144Hz)          │
│  ┌─────────────────┐         ┌─────────────────┐        │
│  │ 逻辑帧          │         │ 渲染帧          │        │
│  │ - 物理计算      │         │ - 画面渲染      │        │
│  │ - AI 决策       │    →    │ - 动画播放      │        │
│  │ - 状态更新      │  快照   │ - 位置插值      │        │
│  │ - 碰撞检测      │         │ - 特效显示      │        │
│  └─────────────────┘         └─────────────────┘        │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### 18.2 输入缓冲

```csharp
// 问题：按键发生在两个 FixedUpdate 之间会丢失

// 解决：Update 收集输入，FixedUpdate 消费
private Queue<InputCommand> _inputBuffer = new();

void Update()
{
    // 收集输入
    if (Input.GetButtonDown("Jump"))
    {
        _inputBuffer.Enqueue(new InputCommand { Type = InputType.Jump });
    }
}

void FixedUpdate()
{
    // 消费输入
    while (_inputBuffer.TryDequeue(out var cmd))
    {
        _gameplayWorld.HandleInput(cmd);
    }
    
    _gameplayWorld.FixedTick(Time.fixedDeltaTime);
}
```

---

## 19. 完整项目实战

### 19.1 项目结构

```
Assets/
├── CYFramework/              # 框架（不要修改）
├── _Project/
│   ├── Scenes/
│   │   ├── Bootstrap.unity   # 启动场景
│   │   ├── MainMenu.unity    # 主菜单
│   │   └── Game.unity        # 游戏场景
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── GameManager.cs
│   │   │   └── Events.cs     # 事件定义
│   │   ├── Player/
│   │   │   ├── Player.cs
│   │   │   └── PlayerData.cs
│   │   ├── UI/
│   │   │   ├── MainMenuPanel.cs
│   │   │   ├── GameHudPanel.cs
│   │   │   └── SettingsPanel.cs
│   │   └── Save/
│   │       └── ProjectSave.cs
│   ├── Prefabs/
│   │   └── ...
│   └── Resources/
│       ├── UI/Panels/
│       ├── Audio/
│       └── Config/
└── ...
```

### 19.2 启动场景（Bootstrap）搭建

`Bootstrap.unity` 场景建议只放两类对象：

1) `CYBootstrap`（框架驱动器，负责初始化 ServiceLocator 并驱动生命周期）  
2) 你的 `GameEntryBase` 派生入口（负责注册/启动流程与业务系统）

> ⚠️ 重点：业务代码优先从 `GameEntryBase` 进入，而不是再造一个“全局 GameManager 上帝类”。

### 19.3 游戏入口（GameEntryBase）

```csharp
using CYFramework;
using CYFramework.Core;
using CYFramework.Core.Procedure;
using UnityEngine;

/// <summary>
/// 项目入口：挂到 Bootstrap 场景任意 GameObject 上即可
/// - 框架已由 CYBootstrap 初始化
/// - 这里负责：注册流程 / 启动首流程 / 统一订阅事件（可选）
/// </summary>
public sealed class ProjectGameEntry : GameEntryBase
{
    /// <summary>
    /// 自动注册流程：
    /// 运行时优先从 Resources/CYFramework/ProcedureRegistry 加载注册表；
    /// WebGL/微信不依赖运行期扫程序集（需要你在 Editor 生成注册表）。
    /// </summary>
    protected override bool AutoRegisterProcedures => true;

    /// <summary>
    /// 自动扫描 [OnEvent] 标记的方法并订阅
    /// </summary>
    protected override bool AutoSubscribeEvents => true;

    protected override void OnGameInit()
    {
        // 这里适合做：项目配置初始化、资源预热等（避免做重逻辑）
        CYLog.Info("[Project] OnGameInit");
    }

    protected override void OnGameStart()
    {
        // 启动首流程（按名称）
        CY.Procedure.Start("Menu");
    }
}
```

### 19.4 事件定义（struct + ref）

```csharp
/// <summary>
/// 游戏开始事件（示例）
/// </summary>
public struct GameStartEvent
{
    public int StageId;
}

/// <summary>
/// 游戏结束事件（示例）
/// </summary>
public struct GameOverEvent
{
    public bool IsVictory;
}
```

### 19.5 流程（Procedure）组织主流程

```csharp
using CYFramework;
using CYFramework.Core.Event;
using CYFramework.Core.Procedure;
using CYFramework.Core.UI;

[AutoRegisterProcedure("Menu", order: 0)]
public sealed class MenuProcedure : ProcedureBase
{
    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        // 打开主菜单
        CY.UI.Open<MainMenuPanel>();
    }

    protected override void OnLeave(ProcedureBase nextProcedure)
    {
        CY.UI.Close<MainMenuPanel>();
    }
}

[AutoRegisterProcedure("Battle", order: 1)]
public sealed class BattleProcedure : ProcedureBase
{
    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        // 打开 HUD
        CY.UI.Open<GameHudPanel>();

        // 进入战斗时发布事件（示例）
        var evt = new GameStartEvent { StageId = 1 };
        CY.Event.Post(ref evt);

        // 播放 BGM（音频资源建议放 Resources/Audio/BGM/...）
        CY.Audio.PlayBGM("bgm_game");
    }

    protected override void OnLeave(ProcedureBase nextProcedure)
    {
        CY.UI.Close<GameHudPanel>();
    }

    [OnEvent]
    private void OnGameOver(ref GameOverEvent evt)
    {
        // 示例：战斗结束回到菜单
        CY.Procedure.Change("Menu");
    }
}
```

> ⚠️ 新增/修改流程后，请在 Unity 菜单执行：`CYFramework/Generate Procedure Registry`，生成 `Assets/CYFramework/Resources/CYFramework/ProcedureRegistry.asset`。

### 19.6 UI 面板（UIPanel + [UIPrefab]）

```csharp
using CYFramework;
using CYFramework.Core.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单面板
/// - 预制体放在 Resources/UI/Panels/MainMenuPanel.prefab
/// - 路径写成相对 Resources 的路径：UI/Panels/MainMenuPanel
/// </summary>
[UIPrefab("UI/Panels/MainMenuPanel")]
public sealed class MainMenuPanel : UIPanel
{
    [SerializeField] private Button startButton;

    public override UILayer Layer => UILayer.Main;
    public override bool IsPoolable => true;

    protected override void OnBindUI()
    {
        // 只在绑定阶段注册一次，避免 Update 中重复分配
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }
    }

    protected override void OnUnbindUI()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
        }
    }

    private void OnStartClicked()
    {
        // 切换到战斗流程
        CY.Procedure.Change("Battle");
    }
}

[UIPrefab("UI/Panels/GameHudPanel")]
public sealed class GameHudPanel : UIPanel
{
    public override UILayer Layer => UILayer.Main;
    public override bool IsPoolable => true;
}
```

### 19.7 存档闭环（SaveDataBase + CY.SaveData/LoadData）

```csharp
using System;
using CYFramework;
using CYFramework.Core.Save;

/// <summary>
/// 存档数据必须继承 SaveDataBase（框架会在 Save/Load 时做版本与校验逻辑）
/// </summary>
[Serializable]
public sealed class PlayerSaveData : SaveDataBase
{
    public int Gold;
    public int Level;
}

public static class ProjectSave
{
    private const string Key = "player";

    public static PlayerSaveData Load()
    {
        return CY.LoadData<PlayerSaveData>(Key);
    }

    public static void Save(PlayerSaveData data)
    {
        CY.SaveData(Key, data);
    }
}
```

### 19.8 网络闭环（HTTP Get/Post 返回 HttpResponse）

> 注意：当前 `NetworkService` 示例 API 返回 `HttpResponse`（包含 `IsSuccess/StatusCode/Data/Error`）。如果你要强类型反序列化，可自行用 `JsonUtility` 解析 `Data`。

```csharp
using System.Threading.Tasks;
using CYFramework;
using UnityEngine;

public static class ProjectNetwork
{
    public static async Task FetchConfigAsync()
    {
        var resp = await CY.HttpGet("https://example.com/config.json");
        if (resp == null || !resp.IsSuccess)
        {
            CYLog.Warning($"[Net] FetchConfig 失败: {(resp == null ? "null" : resp.Error)}");
            return;
        }

        // 示例：把 JSON 文本交给业务解析
        CYLog.Info($"[Net] Config: {resp.Data}");
    }
}
```

### 19.9 资源加载与热更（按当前实现）

#### 19.9.1 资源加载（当前默认：Resources + 缓存）

框架当前默认 `IResourceLoader` 实现为 `Resources` + 缓存（`ResourceLoader`）。

推荐约定：

- 预制体：`Resources/Prefabs/...`
- UI 面板：`Resources/UI/Panels/...`（配合 `[UIPrefab("UI/Panels/xxx")]`）
- 音频：`Resources/Audio/BGM/...`、`Resources/Audio/SFX/...`

```csharp
using CYFramework;
using UnityEngine;

public static class ProjectResource
{
    /// <summary>
    /// 同步加载预制体并实例化
    /// </summary>
    public static GameObject SpawnEnemy(Transform parent = null)
    {
        var prefab = CY.Resource.Load<GameObject>("Prefabs/Enemy");
        if (prefab == null)
        {
            CYLog.Warning("[Res] Enemy prefab not found: Resources/Prefabs/Enemy");
            return null;
        }

        // 注意：Instantiate 本身会分配对象，这是“低频路径”可以接受；高频生成请走对象池或实体系统
        return Object.Instantiate(prefab, parent);
    }
}
```

#### 19.9.2 热更（能力边界说明）

- **WebGL/微信**：不支持动态程序集加载（不能依赖 HybridCLR），热更以“资源热更/配置热更”为主。
- **Native（PC/Android/iOS）**：可以结合项目需要扩展 Addressables/AB；框架在资源模块中保留了扩展点，但当前默认实现仍是 Resources。

---

## 20. 常见问题解答

### Q: 服务获取失败，报 NullReferenceException？

**原因**：在框架初始化完成前就调用了 `ServiceLocator.Get`

**解决**：
```csharp
// ❌ 错误：Awake 可能比框架启动更早
void Awake()
{
    var audio = ServiceLocator.Get<IAudioService>(); // 可能为空！
}

// ✅ 正确：在 Start 中获取
void Start()
{
    var audio = ServiceLocator.Get<IAudioService>(); // 安全
}
```

### Q: 事件订阅了但收不到？

**检查清单**：
1. 事件是否用 `struct` 定义？
2. 发布时是否用了 `ref`？
3. 订阅的处理方法参数是否有 `ref`？
4. 是否在销毁后还在发送？

### Q: UI 面板打不开？

**检查清单**：
1. 预制体是否在 `Resources/UI/Panels/` 下？
2. `[UIPrefab]` 特性路径是否正确？
3. 预制体根节点是否挂载了面板脚本？

### Q: 微信小游戏存档失败？

**解决**：添加宏定义 `CY_WECHAT`，并确认已启用微信平台的 `IStorageAdapter`（微信/WebGL 默认走 Storage 模式，而非文件系统模式）。

### Q: WebGL 没有声音？

**原因**：iOS Safari 需要用户交互才能播放音频

**解决**：确保第一次播放在点击事件中触发
