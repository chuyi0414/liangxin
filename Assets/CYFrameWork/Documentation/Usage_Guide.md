# CYFramework 2.2 超详细使用指南

> 本指南假设你是零基础，会一步一步带你理解框架的原理和使用方法。

---

## 目录

1. [框架是什么？解决什么问题？](#1-框架是什么)
2. [框架的核心原理](#2-框架的核心原理)
3. [完整的生命周期流程](#3-完整的生命周期流程)
4. [第一步：让框架跑起来](#4-第一步让框架跑起来)
5. [CY 统一入口（推荐）](#5-cy-统一入口) **[NEW]**
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
| 保存数据 | `SaveService` | `save.Save("player", data)` |
| 管理 UI | `UIManager` | `ui.Open<ShopPanel>()` |
| 发请求 | `NetworkService` | `await network.GetAsync<T>(url)` |
| 对象池 | `PoolManager` | `pool.Spawn<Bullet>()` |
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

## 5. CY 统一入口（推荐）

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
        CY.Event.Fire(new GameStartEvent { StageId = 1 });
        
        // 日志
        CY.Log.Info("游戏启动");
        CY.Log.Warning("这是警告");
        
        // 计时器
        CY.Timer.Delay(2f, () => CY.Log.Info("2秒后执行"));
        CY.Timer.Loop(1f, () => CY.Log.Info("每秒执行一次"));
        
        // 流程切换
        CY.Procedure.Change<BattleProcedure>();
    }
    
    void OnDestroy()
    {
        CY.Event.UnsubscribeAll(this);  // 清理订阅
    }
    
    void OnGameStart(ref GameStartEvent evt)
    {
        CY.Log.Info($"关卡 {evt.StageId} 开始!");
    }
}
```

### 5.3 CY vs ServiceLocator 对比

| 功能 | ServiceLocator 写法 | CY 写法 |
|------|---------------------|---------|
| 事件订阅 | `ServiceLocator.Get<EventBus>().Subscribe(...)` | `CY.Event.Subscribe(...)` |
| 发布事件 | `ServiceLocator.Get<EventBus>().Post(ref evt)` | `CY.Event.Fire(evt)` |
| 日志 | `CYLog.Info("msg")` | `CY.Log.Info("msg")` |
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

## 6. 事件系统详解

### 6.1 为什么需要事件系统？

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

### 6.2 事件的完整使用流程

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

### 6.3 事件优先级

订阅时可以指定优先级，数字大的先执行：

```csharp
// 高优先级（先执行）- 比如音效要立即响应
_eventBus.Subscribe<PlayerDiedEvent>(OnDied, this, priority: 100);

// 普通优先级（默认 0）
_eventBus.Subscribe<PlayerDiedEvent>(OnDied, this);

// 低优先级（后执行）- 比如统计可以晚点
_eventBus.Subscribe<PlayerDiedEvent>(OnDied, this, priority: -100);
```

### 6.4 常见错误

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

## 7. UI 系统完整教程

### 7.1 UI 系统架构

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
│  │ 生命周期：                                           ││
│  │ OnBindUI()   → 绑定按钮点击等事件                     ││
│  │ OnShow()     → 面板显示，初始化数据                   ││
│  │ OnHide()     → 面板隐藏，清理状态                     ││
│  │ OnUnbindUI() → 解绑事件                              ││
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

### 7.2 UI 层级说明

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

### 7.3 创建你的第一个 UI 面板

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

### 7.4 面板之间传递数据

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

### 7.5 使用通用组件

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
        SaveManager.Instance.Delete();
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

### 7.6 MVVM 数据绑定

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

## 8. 存档系统详解

### 8.1 基本使用

```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Save;

// 获取存档服务
var save = ServiceLocator.Get<SaveService>();

// 定义存档数据
[System.Serializable]
public class PlayerData
{
    public string name = "玩家";
    public int level = 1;
    public int gold = 0;
    public List<int> unlockedItems = new List<int>();
}

// 保存数据
var data = new PlayerData { level = 10, gold = 5000 };
save.Save("player", data);

// 读取数据
var loaded = save.Load<PlayerData>("player");
if (loaded != null)
{
    CYLog.Info($"等级: {loaded.level}, 金币: {loaded.gold}");
}

// 读取（带默认值）
var settings = save.Load<SettingsData>("settings", new SettingsData());

// 检查存档是否存在
if (save.Exists("player"))
{
    // 有存档
}

// 删除存档
save.Delete("player");
```

### 8.2 完整的存档管理器示例

```csharp
public class GameSaveManager
{
    private static GameSaveManager _instance;
    public static GameSaveManager Instance => _instance ??= new GameSaveManager();
    
    private SaveService _saveService;
    private PlayerData _playerData;
    
    private const string PLAYER_KEY = "player_save";
    
    public PlayerData Data => _playerData;
    
    private GameSaveManager()
    {
        _saveService = ServiceLocator.Get<SaveService>();
    }
    
    /// <summary>
    /// 加载存档
    /// </summary>
    public bool Load()
    {
        if (_saveService.Exists(PLAYER_KEY))
        {
            _playerData = _saveService.Load<PlayerData>(PLAYER_KEY);
            CYLog.Info($"存档加载成功，等级: {_playerData.level}");
            return true;
        }
        
        // 没有存档，创建新的
        _playerData = new PlayerData();
        CYLog.Info("创建新存档");
        return false;
    }
    
    /// <summary>
    /// 保存存档
    /// </summary>
    public void Save()
    {
        _saveService.Save(PLAYER_KEY, _playerData);
        CYLog.Info("存档保存成功");
    }
    
    /// <summary>
    /// 删除存档（重新开始）
    /// </summary>
    public void Reset()
    {
        _saveService.Delete(PLAYER_KEY);
        _playerData = new PlayerData();
        CYLog.Info("存档已重置");
    }
}
```

---

## 9. 音频系统详解

### 9.1 基本使用

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

### 9.2 音频资源放置

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

## 10. 对象池详解

### 10.1 为什么需要对象池？

**问题**：频繁创建销毁对象会产生 GC（垃圾回收），导致游戏卡顿

**解决**：用完的对象不销毁，放回"池子"里，下次需要时取出来复用

### 10.2 使用 GameObject 池

```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Pool;

public class BulletSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _bulletPrefab;
    
    private PoolManager _pool;
    
    void Start()
    {
        _pool = ServiceLocator.Get<PoolManager>();
        
        // 创建子弹池，预热 20 个
        _pool.CreateGameObjectPool("Bullet", _bulletPrefab, 20);
    }
    
    public void Fire(Vector3 position, Vector3 direction)
    {
        // 从池中获取子弹
        var bullet = _pool.SpawnGameObject("Bullet", position, Quaternion.identity);
        
        // 设置子弹方向...
        bullet.GetComponent<Bullet>().SetDirection(direction);
    }
    
    public void RecycleBullet(GameObject bullet)
    {
        // 回收子弹到池中
        _pool.DespawnGameObject("Bullet", bullet);
    }
}
```

### 10.3 使用数据对象池

```csharp
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
var pool = ServiceLocator.Get<PoolManager>();

// 创建池
pool.CreatePool<DamageData>(() => new DamageData(), 50, 200);

// 获取
var dmg = pool.Spawn<DamageData>();
dmg.Damage = 100;
dmg.SourceId = 1;
dmg.TargetId = 2;

// 处理完后归还
pool.Despawn(dmg);
```

---

## 11. 网络通信详解

### 11.1 HTTP 请求

```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Network;

var network = ServiceLocator.Get<NetworkService>();

// GET 请求
var playerData = await network.GetAsync<PlayerData>("/api/player/123");

// POST 请求
var loginRequest = new LoginRequest { username = "test", password = "123" };
var loginResult = await network.PostAsync<LoginResponse>("/api/login", loginRequest);

// 带请求头
var headers = new Dictionary<string, string>
{
    { "Authorization", "Bearer your-token" }
};
var data = await network.GetAsync<MyData>("/api/data", headers);
```

### 11.2 WebSocket

```csharp
// 连接
await network.ConnectWebSocket("wss://game.server.com/ws");

// 监听消息
network.OnWebSocketMessage += (message) => {
    var data = JsonUtility.FromJson<ServerMessage>(message);
    HandleMessage(data);
};

// 发送消息
var cmd = new MoveCommand { x = 1, y = 2 };
network.SendWebSocketMessage(JsonUtility.ToJson(cmd));

// 断开
network.DisconnectWebSocket();
```

---

## 12. 玩法核心层详解

### 12.1 逻辑帧与渲染帧分离

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

### 12.2 输入缓冲

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

## 13. 完整项目实战

### 13.1 项目结构

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
│   │   │   ├── GameHUDPanel.cs
│   │   │   └── SettingsPanel.cs
│   │   └── Save/
│   │       └── SaveManager.cs
│   ├── Prefabs/
│   │   └── ...
│   └── Resources/
│       ├── UI/Panels/
│       ├── Audio/
│       └── Config/
└── ...
```

### 13.2 游戏管理器示例

```csharp
using UnityEngine;
using CYFramework.Infrastructure;
using CYFramework.Core.Event;
using CYFramework.Core.UI;
using CYFramework.Core.Audio;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    private EventBus _eventBus;
    private UIManager _uiManager;
    private IAudioService _audio;
    
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
        // 获取服务
        _eventBus = ServiceLocator.Get<EventBus>();
        _uiManager = ServiceLocator.Get<UIManager>();
        _audio = ServiceLocator.Get<IAudioService>();
        
        // 订阅事件
        _eventBus.Subscribe<GameStartEvent>(OnGameStart, this);
        _eventBus.Subscribe<GameOverEvent>(OnGameOver, this);
        
        // 加载存档
        SaveManager.Instance.Load();
        
        // 打开主菜单
        _uiManager.Open<MainMenuPanel>();
    }
    
    void OnGameStart(ref GameStartEvent evt)
    {
        _audio.PlayBGM("bgm_game");
        _uiManager.CloseAll();
        _uiManager.Open<GameHUDPanel>();
    }
    
    void OnGameOver(ref GameOverEvent evt)
    {
        SaveManager.Instance.Save();
        _uiManager.Open<GameOverPanel>();
    }
    
    void OnDestroy()
    {
        _eventBus?.UnsubscribeAll(this);
    }
}
```

---

## 14. 常见问题解答

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

**解决**：添加宏定义 `CY_WECHAT;CY_SINGLE_THREAD`

### Q: WebGL 没有声音？

**原因**：iOS Safari 需要用户交互才能播放音频

**解决**：确保第一次播放在点击事件中触发

