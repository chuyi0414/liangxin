# CYFramework 2.2 API 参考文档

## 目录

- [1. 快速入门](#1-快速入门)
- [2. 基础设施层](#2-基础设施层)
  - [2.1 ServiceLocator](#21-servicelocator)
  - [2.2 生命周期接口](#22-生命周期接口)
  - [2.3 CYBootstrap](#23-cybootstrap)
- [3. 核心服务层](#3-核心服务层)
  - [3.1 EventBus](#31-eventbus)
  - [3.2 CYLog](#32-cylog)
  - [3.3 ObjectPool](#33-objectpool)
  - [3.4 ConfigLoader](#34-configloader)
  - [3.5 ResourceLoader](#35-resourceloader)
  - [3.6 NetworkService](#36-networkservice)
  - [3.7 SaveService](#37-saveservice)
  - [3.8 AudioService](#38-audioservice)
  - [3.9 HotUpdateService](#39-hotupdateservice)
- [4. 玩法核心层](#4-玩法核心层)
  - [4.1 IGameplayWorld](#41-igameplayworld)
  - [4.2 InputBuffer](#42-inputbuffer)
  - [4.3 RenderProxy](#43-renderproxy)
  - [4.4 状态机与AI](#44-状态机与ai)
- [5. 调试工具](#5-调试工具)
- [6. 平台适配](#6-平台适配)

---

## 1. 快速入门

### 1.1 安装

1. 将 `CYFramework` 文件夹放入 `Assets/` 目录
2. 在场景中创建空 GameObject，命名为 `[CYFramework]`
3. 添加 `CYBootstrap` 组件
4. 运行即可

### 1.2 第一个示例

```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Event;
using UnityEngine;

public class MyGameManager : MonoBehaviour
{
    void Start()
    {
        // 获取服务
        var eventBus = ServiceLocator.Get<EventBus>();
        
        // 订阅事件
        eventBus.Subscribe<GameStartEvent>(OnGameStart, this);
        
        // 发布事件
        var evt = new GameStartEvent { Level = 1 };
        eventBus.Post(ref evt);
    }
    
    void OnGameStart(GameStartEvent e)
    {
        CYLog.Info($"游戏开始，关卡: {e.Level}");
    }
}

public struct GameStartEvent
{
    public int Level;
}
```

### 1.3 平台宏定义

在 `Player Settings > Scripting Define Symbols` 中添加：

| 宏 | 说明 |
|-----|------|
| `CY_WECHAT` | 微信小游戏平台 |
| `CY_PC` | PC 平台（启用高级特性） |
| `ENABLE_DOTS` | 启用 Hybrid DOTS 模式 |

---

## 2. 基础设施层

### 2.1 ServiceLocator

服务定位器，用于依赖注入和服务管理。

#### 注册服务

```csharp
// 方式 1: 接口 + 实现类（推荐）
ServiceLocator.Register<IMyService, MyServiceImpl>();

// 方式 2: 工厂方法
ServiceLocator.Register<MyService>(() => new MyService("config"));

// 方式 3: 直接注册实例
var instance = new MyService();
ServiceLocator.RegisterInstance<IMyService>(instance);
```

#### 服务生命周期

```csharp
// Singleton（默认）：全局单例
ServiceLocator.Register<IMyService, MyService>(ServiceScope.Singleton);

// Scoped：场景级别，切场景时清理
ServiceLocator.Register<IMyService, MyService>(ServiceScope.Scoped);

// Transient：每次获取都创建新实例
ServiceLocator.Register<IMyService, MyService>(ServiceScope.Transient);
```

#### 获取服务

```csharp
// 直接获取（服务不存在会抛异常）
var service = ServiceLocator.Get<IMyService>();

// 安全获取
if (ServiceLocator.TryGet<IMyService>(out var service))
{
    service.DoSomething();
}
```

#### API 列表

| 方法 | 说明 |
|------|------|
| `Register<TInterface, TImpl>()` | 注册服务 |
| `Register<T>(Func<T> factory)` | 工厂注册 |
| `RegisterInstance<T>(T instance)` | 注册实例 |
| `Get<T>()` | 获取服务 |
| `TryGet<T>(out T service)` | 安全获取 |
| `InitializeAll()` | 初始化所有 IInitializable |
| `DisposeAll()` | 销毁所有 IDisposableEx |
| `ClearScoped()` | 清理 Scoped 服务 |
| `ClearAll()` | 清理所有服务 |

---

### 2.2 生命周期接口

实现这些接口的服务会被 CYBootstrap 自动调度。

```csharp
// 初始化接口
public interface IInitializable
{
    int InitOrder { get; }  // 初始化顺序（小的先执行）
    void Initialize();
}

// 固定帧更新（逻辑帧，30/60Hz）
public interface ITickable
{
    int TickOrder { get; }
    void Tick(float dt);
}

// 每帧更新
public interface IUpdateable
{
    int UpdateOrder { get; }
    void OnUpdate(float dt);
}

// 暂停/恢复
public interface IPausable
{
    void OnPause();
    void OnResume(float pauseDuration);
}

// 销毁接口
public interface IDisposableEx
{
    int DisposeOrder { get; }
    void Dispose();
}
```

#### 示例

```csharp
public class MySystem : IInitializable, ITickable, IDisposableEx
{
    public int InitOrder => 10;
    public int TickOrder => 10;
    public int DisposeOrder => 10;
    
    public void Initialize()
    {
        CYLog.Info("MySystem 初始化");
    }
    
    public void Tick(float dt)
    {
        // 逻辑更新
    }
    
    public void Dispose()
    {
        CYLog.Info("MySystem 销毁");
    }
}
```

---

### 2.3 CYBootstrap

框架启动器，挂载到场景 GameObject 上。

#### Inspector 配置

| 属性 | 说明 | 默认值 |
|------|------|--------|
| Log Level | 日志级别 | Debug |
| Fixed Tick Rate | 逻辑帧率 | 30 |
| Max Pause Tolerance | 切后台最大容忍时间 | 5s |

---

## 3. 核心服务层

### 3.1 EventBus

零 GC 事件系统。

#### 定义事件

```csharp
// 使用 struct 避免 GC
public struct PlayerDiedEvent
{
    public int PlayerId;
    public Vector3 Position;
    public string Killer;
}
```

#### 订阅事件

```csharp
var eventBus = ServiceLocator.Get<EventBus>();

// 基本订阅
eventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied, this);

// 带优先级（数字大的先执行）
eventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied, this, priority: 100);
```

#### 发布事件

```csharp
// ⚠️ 必须使用 ref 传递，避免装箱
var evt = new PlayerDiedEvent { PlayerId = 1 };
eventBus.Post(ref evt);
```

#### 取消订阅

```csharp
eventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);

// 或取消某对象的所有订阅
eventBus.UnsubscribeAll(this);
```

#### API 列表

| 方法 | 说明 |
|------|------|
| `Subscribe<T>(Action<T>, object, int)` | 订阅事件 |
| `Unsubscribe<T>(Action<T>)` | 取消订阅 |
| `UnsubscribeAll(object)` | 取消对象所有订阅 |
| `Post<T>(ref T)` | 发布事件 |
| `Clear()` | 清空所有订阅 |

---

### 3.2 CYLog

分级日志系统。

#### 日志级别

```csharp
public enum LogLevel
{
    Verbose = 0,  // 详细调试
    Debug = 1,    // 调试信息
    Info = 2,     // 一般信息
    Warning = 3,  // 警告
    Error = 4,    // 错误
    Fatal = 5,    // 致命错误
    Off = 6       // 关闭日志
}
```

#### 使用方法

```csharp
CYLog.Verbose("详细信息");
CYLog.Debug("调试信息");
CYLog.Info("一般信息");
CYLog.Warning("警告");
CYLog.Error("错误");
CYLog.Fatal("致命错误");

// 带条件的日志（避免字符串拼接开销）
CYLog.DebugIf(isDebugMode, () => $"复杂计算结果: {Calculate()}");
```

#### 配置

```csharp
// 初始化时设置级别
CYLog.Initialize(LogLevel.Info);

// 运行时修改
CYLog.SetLevel(LogLevel.Warning);
```

---

### 3.3 ObjectPool

对象池管理器，支持数据对象和 GameObject。

#### 数据对象池

```csharp
var poolManager = ServiceLocator.Get<PoolManager>();

// 注册池（预热 10 个）
poolManager.RegisterDataPool<Bullet>(
    createFunc: () => new Bullet(),
    prewarm: 10,
    maxSize: 100
);

// 获取对象
var bullet = poolManager.Spawn<Bullet>();

// 归还对象
poolManager.Despawn(bullet);
```

#### GameObject 池

```csharp
// 注册 Prefab 池
poolManager.RegisterPrefabPool(
    bulletPrefab,
    prewarm: 20,
    maxSize: 200
);

// 生成
var go = poolManager.SpawnPrefab(bulletPrefab, position, rotation);

// 回收
poolManager.DespawnPrefab(go);
```

#### 自动回收接口

```csharp
public class Bullet : IPoolable
{
    public void OnSpawn()
    {
        // 从池中取出时调用
        isActive = true;
    }
    
    public void OnDespawn()
    {
        // 归还池时调用
        isActive = false;
        velocity = Vector3.zero;
    }
}
```

---

### 3.4 ConfigLoader

配置加载器。

#### 加载配置

```csharp
var configLoader = ServiceLocator.Get<IConfigLoader>();

// 同步加载
var weaponConfig = configLoader.Load<WeaponConfig>("Config/Weapons");

// 异步加载
var config = await configLoader.LoadAsync<EnemyConfig>("Config/Enemies");

// 批量预加载
await configLoader.PreloadAsync(new[] {
    "Config/Weapons",
    "Config/Enemies",
    "Config/Skills"
});
```

#### 配置定义

```csharp
[CreateAssetMenu(fileName = "WeaponConfig", menuName = "CYFramework/WeaponConfig")]
public class WeaponConfig : ScriptableObject
{
    public string weaponName;
    public int damage;
    public float attackSpeed;
}
```

---

### 3.5 ResourceLoader

资源加载器。

#### 同步加载

```csharp
var loader = ServiceLocator.Get<IResourceLoader>();

// 加载 Prefab
var prefab = loader.Load<GameObject>("Prefabs/Player");

// 加载纹理
var texture = loader.Load<Texture2D>("Textures/UI/Button");
```

#### 异步加载

```csharp
// 单个资源
var prefab = await loader.LoadAsync<GameObject>("Prefabs/Enemy");

// 带进度回调
loader.LoadAsync<GameObject>("Prefabs/Boss", progress => {
    loadingBar.value = progress;
});
```

#### 场景加载

```csharp
// 异步加载场景
await loader.LoadSceneAsync("Level1", LoadSceneMode.Single);

// 带进度
await loader.LoadSceneAsync("Level2", LoadSceneMode.Additive, 
    progress => Debug.Log($"加载进度: {progress:P0}"));
```

#### 资源释放

```csharp
// 释放单个资源
loader.Release("Prefabs/Enemy");

// 释放未使用资源
loader.UnloadUnusedAssets();
```

---

### 3.6 NetworkService

网络服务，支持 HTTP 和 WebSocket。

#### HTTP 请求

```csharp
var network = ServiceLocator.Get<NetworkService>();

// GET 请求
var response = await network.GetAsync<PlayerData>("/api/player/123");

// POST 请求
var loginData = new LoginRequest { username = "test", password = "123" };
var result = await network.PostAsync<LoginResponse>("/api/login", loginData);

// 带请求头
var headers = new Dictionary<string, string> {
    { "Authorization", "Bearer xxx" }
};
var data = await network.GetAsync<MyData>("/api/data", headers);
```

#### WebSocket

```csharp
// 连接
await network.ConnectWebSocket("wss://game.server.com/ws");

// 发送消息
network.SendWebSocketMessage(JsonUtility.ToJson(new MoveCommand { x = 1, y = 2 }));

// 注册消息处理
network.OnWebSocketMessage += (message) => {
    var data = JsonUtility.FromJson<ServerMessage>(message);
    ProcessMessage(data);
};

// 断开
network.DisconnectWebSocket();
```

#### 网络状态

```csharp
// 检查连接
bool isConnected = network.IsWebSocketConnected;

// 网络状态变化事件
network.OnNetworkStatusChanged += (status) => {
    if (status == NetworkStatus.Disconnected) {
        ShowReconnectDialog();
    }
};
```

---

### 3.7 SaveService

存档服务，支持加密和版本迁移。

#### 保存数据

```csharp
var saveService = ServiceLocator.Get<SaveService>();

// 定义存档数据
[Serializable]
public class PlayerSaveData
{
    public int level;
    public int gold;
    public List<int> unlockedSkills;
}

// 保存
var data = new PlayerSaveData { level = 10, gold = 5000 };
await saveService.SaveAsync("player", data);

// 快速保存（同步，适合小数据）
saveService.Save("settings", settingsData);
```

#### 加载数据

```csharp
// 异步加载
var data = await saveService.LoadAsync<PlayerSaveData>("player");

// 同步加载
var settings = saveService.Load<SettingsData>("settings");

// 带默认值
var data = saveService.Load<PlayerSaveData>("player", new PlayerSaveData());
```

#### 检查与删除

```csharp
// 检查存档是否存在
if (saveService.Exists("player"))
{
    // 加载
}

// 删除存档
saveService.Delete("player");

// 删除所有存档
saveService.DeleteAll();
```

#### 版本迁移

```csharp
// 注册迁移器
saveService.RegisterMigration<PlayerSaveData>(1, 2, oldData => {
    // v1 -> v2: 添加新字段
    return new PlayerSaveDataV2 {
        level = oldData.level,
        gold = oldData.gold,
        gems = 0  // 新字段默认值
    };
});
```

---

### 3.8 AudioService

音频服务。

#### 播放音乐

```csharp
var audio = ServiceLocator.Get<IAudioService>();

// 播放 BGM
audio.PlayBGM("bgm_battle", volume: 0.8f, loop: true);

// 停止 BGM（带淡出）
audio.StopBGM(fadeOut: 1.0f);

// 暂停/恢复
audio.PauseBGM();
audio.ResumeBGM();
```

#### 播放音效

```csharp
// 播放音效
audio.PlaySFX("sfx_explosion");

// 调整音量
audio.PlaySFX("sfx_coin", volume: 0.5f);
```

#### 音量控制

```csharp
// 主音量
audio.SetMasterVolume(0.8f);

// 分类音量
audio.SetBGMVolume(0.6f);
audio.SetSFXVolume(1.0f);

// 静音
audio.Mute(true);
```

---

### 3.9 HotUpdateService

热更新服务。

#### 检查更新

```csharp
var hotUpdate = ServiceLocator.Get<IHotUpdateService>();

// 检查更新
var result = await hotUpdate.CheckForUpdateAsync();

if (result.HasUpdate)
{
    Debug.Log($"发现新版本: {result.LatestVersion}");
    Debug.Log($"需要下载: {result.TotalDownloadSize / 1024}KB");
}
```

#### 执行更新

```csharp
// 开始更新（带进度回调）
await hotUpdate.DownloadUpdateAsync(progress => {
    progressBar.value = progress.Progress;
    progressText.text = $"{progress.DownloadedFiles}/{progress.TotalFiles}";
});

// 应用更新
await hotUpdate.ApplyUpdateAsync();
```

---

## 4. 玩法核心层

### 4.1 IGameplayWorld

玩法世界抽象接口。

```csharp
public interface IGameplayWorld
{
    // 固定逻辑帧（30/60Hz）
    void FixedTick(float fixedDt);
    
    // 处理输入
    void HandleInput(InputCommand cmd);
    
    // 获取渲染快照
    ref readonly RenderSnapshot GetRenderSnapshot();
    
    // 获取上一帧快照（用于插值）
    ref readonly RenderSnapshot GetPrevSnapshot();
    
    // 重置时间（切后台恢复时调用）
    void ResetDeltaTime();
}
```

#### 使用示例

```csharp
public class GameManager : MonoBehaviour
{
    private IGameplayWorld _world;
    
    void Start()
    {
        // 根据平台选择实现
#if CY_WECHAT || UNITY_WEBGL
        _world = new OOPGameplayWorld();
#else
        _world = new HybridGameplayWorld();
#endif
        
        if (_world is IInitializable init)
            init.Initialize();
    }
    
    void FixedUpdate()
    {
        _world.FixedTick(Time.fixedDeltaTime);
    }
    
    void Update()
    {
        // 收集输入
        if (Input.GetButtonDown("Jump"))
        {
            _world.HandleInput(new InputCommand {
                Type = InputType.Jump,
                Timestamp = Time.time
            });
        }
    }
}
```

---

### 4.2 InputBuffer

输入缓冲，解决 Update/FixedUpdate 频率不同步问题。

```csharp
public class InputBuffer
{
    // 入队输入
    public void Enqueue(InputCommand cmd);
    
    // 尝试出队
    public bool TryDequeue(out InputCommand cmd);
    
    // 清空缓冲
    public void Clear();
    
    // 缓冲数量
    public int Count { get; }
}
```

#### 使用示例

```csharp
private InputBuffer _inputBuffer = new InputBuffer();

void Update()
{
    // 收集输入（在渲染帧）
    if (Input.GetButtonDown("Attack"))
    {
        _inputBuffer.Enqueue(new InputCommand {
            Type = InputType.Attack,
            Timestamp = Time.time
        });
    }
}

void FixedUpdate()
{
    // 消费输入（在逻辑帧）
    while (_inputBuffer.TryDequeue(out var cmd))
    {
        ProcessCommand(cmd);
    }
}
```

---

### 4.3 RenderProxy

渲染代理，提供快照插值。

```csharp
var renderProxy = new RenderProxy(gameplayWorld);

// 每帧更新
void Update()
{
    // 获取插值后的快照
    var snapshot = renderProxy.GetInterpolatedSnapshot();
    
    // 更新渲染对象
    for (int i = 0; i < snapshot.Count; i++)
    {
        var renderer = GetRenderer(snapshot.IDs[i]);
        renderer.transform.position = snapshot.Positions[i];
        renderer.transform.rotation = snapshot.Rotations[i];
    }
}
```

---

### 4.4 状态机与AI

#### 简单状态机

```csharp
public enum EnemyState { Idle, Patrol, Chase, Attack, Dead }

var fsm = new SimpleFSM<EnemyState>(EnemyState.Idle);

// 注册状态
fsm.RegisterState(EnemyState.Idle,
    onEnter: () => animator.Play("Idle"),
    onUpdate: dt => {
        if (CanSeePlayer()) fsm.ChangeState(EnemyState.Chase);
    }
);

fsm.RegisterState(EnemyState.Chase,
    onEnter: () => animator.Play("Run"),
    onUpdate: dt => {
        MoveTowardsPlayer(dt);
        if (InAttackRange()) fsm.ChangeState(EnemyState.Attack);
    }
);

// 每帧更新
void Update()
{
    fsm.Update(Time.deltaTime);
}
```

#### AI 控制器

```csharp
var ai = new SimpleAIController();

// 添加行为（优先级驱动）
ai.AddBehavior(new AttackBehavior());   // 近距离攻击
ai.AddBehavior(new ChaseBehavior());    // 追击玩家
ai.AddBehavior(new PatrolBehavior());   // 巡逻

// 更新
void Update()
{
    var context = new AIContext {
        SelfPosition = transform.position,
        TargetPosition = player.position,
        HP = currentHP
    };
    
    ai.Update(ref context, Time.deltaTime);
}
```

---

## 5. 调试工具

### 5.1 RuntimeProfiler

运行时性能面板。

**快捷键**: `F1`

显示信息：
- FPS / 帧时间
- 内存占用
- DrawCall
- 对象池状态
- 网络延迟

### 5.2 CheatConsole

命令控制台。

**快捷键**: `` ` `` (波浪键)

#### 内置命令

| 命令 | 说明 |
|------|------|
| `help` | 显示所有命令 |
| `clear` | 清空控制台 |
| `fps` | 切换 FPS 显示 |
| `timescale <value>` | 设置时间缩放 |
| `gc` | 强制 GC |
| `log <level>` | 设置日志级别 |
| `quit` | 退出游戏 |

#### 注册自定义命令

```csharp
var console = FindObjectOfType<CheatConsole>();

console.RegisterCommand("god", "无敌模式", args => {
    player.isInvincible = true;
    return "已开启无敌模式";
});

console.RegisterCommand("gold", "设置金币 <amount>", args => {
    if (args.Length > 0 && int.TryParse(args[0], out int amount))
    {
        player.gold = amount;
        return $"金币已设置为 {amount}";
    }
    return "用法: gold <amount>";
});
```

---

## 6. 平台适配

### 6.1 平台检测

```csharp
#if CY_WECHAT
    // 微信小游戏专用代码
#elif UNITY_WEBGL
    // WebGL 专用代码
#elif UNITY_ANDROID
    // Android 专用代码
#elif UNITY_IOS
    // iOS 专用代码
#else
    // PC 专用代码
#endif
```

### 6.2 平台限制

| 技术 | PC | Mobile | WebGL/微信 |
|------|:--:|:------:|:----------:|
| Job System | ✅ | ✅ | ❌ |
| Burst | ✅ | ✅ | ⚠️ |
| System.IO | ✅ | ✅ | ❌ |
| 多线程 | ✅ | ✅ | ❌ |
| WebSocket | ✅ | ✅ | ✅ |

### 6.3 平台适配器

框架自动根据平台选择适配器：

| 服务 | PC/Mobile | 微信/WebGL |
|------|-----------|------------|
| 文件存储 | `UnityFileSystem` | `WeChatStorageAdapter` |
| 音频 | `UnityAudioService` | `WeChatAudioService` |
| 玩法世界 | `HybridGameplayWorld` | `OOPGameplayWorld` |

---

## 附录

### A. 性能红线

| 指标 | 微信/WebGL | Mobile | PC |
|------|-----------|--------|----|
| 帧率 | 45-60 FPS | 60-90 FPS | 60-144 FPS |
| DrawCall | < 100 | < 300 | < 1000 |
| 内存 | < 200MB | < 400MB | < 800MB |
| 每帧 GC | 0 (Release) | 0 (Release) | < 1KB |

### B. 目录结构

```
Assets/CYFramework/
├── Runtime/
│   ├── Infrastructure/     # Bootstrap, ServiceLocator
│   ├── Platform/           # 平台适配器
│   ├── Core/               # 核心服务
│   ├── Gameplay/           # 玩法核心
│   ├── Modules/            # 功能模块
│   └── Debug/              # 调试工具
├── Editor/                 # 编辑器工具
├── Plugins/WebGL/          # JS 桥接
└── Tests/                  # 测试
```

### C. 常见问题

**Q: 微信小游戏存档失败？**
A: 确保添加了 `CY_WECHAT` 宏定义，框架会自动切换到 `wx.setStorageSync`。

**Q: WebGL 没有声音？**
A: iOS Safari 需要用户交互才能播放音频，框架已自动处理，确保首次播放在点击事件中。

**Q: Job System 报错？**
A: WebGL 不支持多线程，检查是否添加了平台宏，框架会自动降级到 OOP 实现。
