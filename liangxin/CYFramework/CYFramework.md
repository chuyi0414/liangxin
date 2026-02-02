# CYFramework 2.2 终极架构技术白皮书

## 概述

**版本**：2.2 (Enhanced Hybrid)

**适用平台**：微信小游戏 / WebGL / Android / iOS / PC

核心愿景：构建一套“可落地”的工业级底座。在微信端追求极致轻量，PC 端通过 “混合架构(Hybrid Architecture)——即用 OOP 写逻辑大脑、用 DOTS 做物理肌肉——来兼顾开发效率与极致性能。

## 设计哲学与核心原则

### 1.1 分层治之 (Layered & Decoupled)

基础设施：采用 Service Locator，实现通用模块热插拔。

玩法核心：采用抽象接口 (IGameplayWorld)，物理隔离逻辑与实现。

### 1.2 性能分级与混合策略(Tiered & Hybrid)

基线 (Baseline)：全平台默认。零 GC、对象池化、SOA 数据布局。

增强 (Enhanced)：**PC/Mobile Native**。采用 Hybrid DOTS 策略：复杂逻辑用 C# 写，大规模运算下放给 Job System + Burst。

> ⚠️ **平台限制**：微信小游戏/WebGL 运行在单线程 JS 环境*不支持 Job System 多线程*，只能使用 OOP Lite 实现。

### 1.3 平台原生亲和 (Platform Native)

针对微信小游戏，直接封装 `wx.getFileSystemManager` 等底层 API，拒绝中间层损耗。

### 1.4 数据与表现分离(Data-View Separation)

双缓冲快照：表现层（View/UI）禁止直接访问底层数据对象（Unit/Entity）。必须通过 Render Proxy 获取只读快照（Snapshot）。这既保证了 ECS 的线程安全，也统一了上层开发体验。

## 2. 总体架构全景

```
┌───────────────────────────────────────────────────────────────┐
│             Layer 5: Presentation (表现层)                    │
│     [UI View] [VFX] [Sound] (Unity GameObjects / Monos)      │
│     │禁止直接持有 Unit/Entity 引用，只消费 Snapshot │         │
└───────────────────────────────▲───────────────────────────────┘
                                │(只读数据)
┌───────────────────────────────┴───────────────────────────────┐
│             Layer 4: Data Bridge (数据桥接层)                 │
│     [RenderProxy] -> GetSnapshot() -> Struct[] / ArraySegment<T>     │
└───────────────────────────────▲───────────────────────────────┘
                                │
┌───────────────────────────────┴───────────────────────────────┐
│             Layer 3: Gameplay Engine (玩法核心层)             │
│  [Interface]: IGameplayWorld / ICommand / IQuery             │
│  [Impl A: OOP Lite]     [Impl B: Hybrid DOTS] (PC旗舰模式)    │
└───────────────────────────────┬───────────────────────────────┘
                                │
┌───────────────────────────────▼───────────────────────────────┐
│             Layer 2: Core Services (核心服务层)               │
│[Config] [EventBus] [Res] [Pool] [Network] [Save] [Log]       │
└───────────────────────────────┬───────────────────────────────┘
                                │
┌───────────────────────────────▼───────────────────────────────┐
│             Layer 1: Platform Adapter (平台适配层)            │
│[IFileSystem] [INetworkAdapter] -> (Unity / WeChat / Web)     │
└───────────────────────────────────────────────────────────────┘
```

## 2.1 当前实现状态（以代码为准）

> ⚠️ 本白皮书包含“架构愿规划”*能否使用、有哪些 API、是否支持某平台**，以 `Assets/CYFramework/Runtime` 的代码实现为准。

| 模块 | 入口/类型 | 当前状| 关键文件 |
|------|----------|----------|----------|
| 统一入口 | `CY` | 已实| `Assets/CYFramework/Runtime/CY.cs` |
| 生命周期/服务定位 | `ServiceLocator` | 已实现（`IInitializable.InitOrder` 排序，无依赖图拓扑排序） | `Assets/CYFramework/Runtime/Infrastructure/ServiceLocator.cs` |
| 事件系统 | `EventBus` | 已实现（struct 事件、优先级、延迟派发；需要显式解绑） | `Assets/CYFramework/Runtime/Core/Event/EventBus.cs` |
| 计时| `TimerManager` | 已实现（Delay/Loop/NextFrame| `Assets/CYFramework/Runtime/Core/Timer/TimerManager.cs` |
| 流程 | `ProcedureManager` | 已实现（支持流程注册表资产） | `Assets/CYFramework/Runtime/Core/Procedure/ProcedureManager.cs` |
| UI | `UIManager` | 已实现（MVVM/Typed MVVM| `Assets/CYFramework/Runtime/Core/UI` |
| 实体 | `EntityManager` | 已实现（含池化） | `Assets/CYFramework/Runtime/Core/Entity` |
| 存档 | `SaveService` | 已实现（版本迁移、AES、校验；WebGL/微信失败回退明文| `Assets/CYFramework/Runtime/Core/Save/SaveService.cs` |
| 网络 | `NetworkService` | 已实现（HTTP/WS、重心跳/熔断、适配器） | `Assets/CYFramework/Runtime/Core/Network/NetworkService.cs` |
| 资源 | `IResourceLoader`/`ResourceLoader` | ⚠️ 当前实现Resources + 缓存；Addressables/AB 为预留接配置| `Assets/CYFramework/Runtime/Core/Resource/ResourceLoader.cs` |
| 调试工具 | `RuntimeProfiler`/`CheatConsole` | 已实现（按配置开关） | `Assets/CYFramework/Runtime/Debug` |
| 流程注册表生| Editor 菜单 | 已实| `Assets/CYFramework/Editor/ProcedureRegistryGenerator.cs` |

## 详细模块设计

### 3.1 核心服务与工(Core Services & Tools)

#### 3.1.1 配置烘焙管线 (Config Baking Pipeline) [NEW]

解决“一套配置驱动两套实现”的问题。

**单一信源 (Source of Truth)**：策划仅维护 Excel ScriptableObject (SO)。

**烘焙流程 (Build Process)**。
- **OOP 目标**：直接拷打包原始 SO
- **DOTS 目标**：通过 Baker<T> 脚本，自动将 SO 数据“烘焙”为二进BlobAsset

**开发工作流优化：No-Baking Mode**

| 环境 | 读取方式 | 说明 |
|------|----------|------|
| **Editor** | 直读 SO 引用 | 无需 Bake，秒改秒|
| **Development Build** | 直读 SO | 调试方便 |
| **Release Build** | BlobAsset | CI/CD 自动烘焙 |

```csharp
public T LoadConfig<T>(string path) where T : ScriptableObject
{
#if UNITY_EDITOR
    // Editor: 直接拷贝 SO，无需烘焙
    return AssetDatabase.LoadAssetAtPath<T>(path);
#else
    // Runtime: 读烘焙后的二进制数据
    return BlobAssetStore.Load<T>(path);
#endif
}
```

**收益**：日常开发“改配置 -> 跑游戏”循环无等待，只有打包时才执Baking。

#### 3.1.2 基础设施

**ServiceLocator**：统一管理生命周期 (IInitializable, ITickable, IDisposable)。

- 支持三种作用域：`Singleton`（全局单例）、`Scoped`（场景级）、`Transient`（每次新建）

- 初始化顺序：基于 `IInitializable.InitOrder` 进行排序初始化（当前实现不做依赖图拓扑排序）

- 懒加载支持：通过 `Lazy<T>` 延迟实例化非关键服务

**EventBus**：零 GC 结构体事件流

- 事件优先级：支持 `Subscribe<T>(..., priority)`，以`[OnEvent(priority)]/[EventPriority(priority)]` 控制回调顺序

- 延迟派发：`PostDelayed(evt, frames)` 支持跨帧安全派发

- 解绑策略：事件系*不会“自动感知对象销毁*；请`OnDestroy/Dispose` 中调`Unsubscribe/UnsubscribeAll(this)` 主动清理（框架入口类`GameEntryBase` 已示范在生命周期钩子中清理）

#### 3.1.3 网络(Network Layer)

**协议支持**。

- **HTTP**：短连接请求，适用于登录、配置拉取、排行榜。

- **WebSocket**：长连接，适用于实时对战、聊天等

- **微信适配**：自动切`wx.request` / `wx.connectSocket`

**可靠性机**

- 自动重连：断线后指数退避重试（1s -> 2s -> 4s -> 8s，上0s。

- 心跳保活：每 15s 发送心跳包，超3 次判定断。

- 请求队列：网络恢复后自动重发未确认请。

- 熔断降级：连续失N 次后熔断，避免雪。

**序列**

- 默认 JSON（调试友好）

- 可MessagePack / Protobuf（生产环境高性能。

#### 3.1.4 存档系统 (Save System)

**存储适配**。

| 平台 | 实现          |
|------|---------------|
| PC/Mobile | PlayerPrefs + 本地文件加密 |
| 微信小游| wx.setStorageSync / wx.getStorageSync |
| 云存| 可选对接微信云托管 / 自建服务|

**版本迁移**。

- 存档携带 `version` 字段
- 注册 `IMigration` 迁移器链：`v1 -> v2 -> v3` 逐级升级
- 迁移失败时保留原存档备份

**安全**

- AES-128 加密本地存档
  - **WebGL 适配**：使用纯 C# 实现（如 `System.Security.Cryptography.Aes`），避免 Native 库依。
- 校验和防篡改
- 敏感数据（货币、道具）服务端权。

#### 3.1.5 热更(Hot Update)

**微信小游**

- 代码分包：首< 4MB，子包按需加载

- 资源 CDN：AB 包托管至 CDN，按版本号增量下。

- 版本检测：启动时对`version.json`，提示更。

**Native（PC/Mobile）**

- Addressables 远程目录：Catalog 热更 + 资源增量下载

- 可HybridCLR：C# 代码热更
  - **仅限 Native *，WebGL/微信不支持动态加载程序集

#### 3.1.6 对象(Object Pool)

- **预热策略**：场景加载时根据配置预实例化

- **峰值处*：超出池上限时临时创建，标记`Overflow`

- **内存收缩**：低内存警告时回`Overflow` 对象 + 50% 空闲对象

- **类型支持**：GameObject / 纯数Struct / UI 节点
- **渲染可见性**：取出前先隐藏 Renderer，取出后默认恢复到预制体初始启用状态，避免复用时旧位置/旧状态闪烁。
  - **实体池手动控制**：`EntityBase.AutoRestoreRenderers = false` 后，自行调用 `RestoreCachedRenderersToDefault()`。
  - **GameObject 池手动控制**：实现 `IPoolRendererControl` 并返回 `AutoRestoreRenderers = false`，自行决定启用时机。
- **回收位置**：归还池时自动移动到远离场景的隐藏坐标，避免复用闪烁与误碰撞。
- **隐藏同步**：回收/显示阶段会同步 Transform 与 Rigidbody(2D/3D) 位置，优先采用非隐藏坐标并清零速度，避免从隐藏坐标拉回导致画面闪动。
- **实体预显示**：实体生成支持“激活前”注入出生数据，确保位置/朝向在 `SetActive(true)` 之前完成。
  - **推荐**：`SpawnEntity<T, TData>(..., ref data)` + 实体实现 `IEntityPreShowData<TData>`。
  - **兼容**：`userData` 实现 `IEntityPreShowTransform` 时，`EntityBase` 会在激活前应用位置/旋转。
- **显示时序**：`SpawnEntity` 先执行 `OnInit`，`OnShow` 统一延迟到下一帧由 EntityManager 队列驱动，避免同帧峰值。
  - **分帧上限**：可通过 `EntityManagerConfig.MaxShowPerFrame` 控制每帧显示数量。
- **UI 元素池**：`CY.UI.GetOrCreateUIElementPool(key, prefab, config)`，用于非 UIPanel 的 UI 元素复用（如血条、飘字），池根挂在 `[ObjectPools]/UI`，取出后自动修正 `RectTransform.anchoredPosition3D.z = 0`。

#### 3.1.7 音频系统 (Audio System) [NEW]

**平台适配策略**。

| 平台 | BGM | SFX | 特殊处理 |
|------|-----|-----|----------|
| **PC/Mobile** | Unity AudioSource | Unity AudioSource | 无 |
| **微信小游* | `wx.createInnerAudioContext` | WebAudio API | 需解锁 |

**微信端特供处**

1. **自动解锁**：iOS 不允许自动播放音频，需用户交互触发
```csharp
// 监听首次触摸，播放静音片段解锁 AudioContext
public void TryUnlockAudio() {
    if (_audioUnlocked) return;
    PlaySilentClip();  // 播放 0.1s 静音
    _audioUnlocked = true;
}
```

2. **实例复用**：微信端禁止频繁创建 AudioContext
   - BGM：全局单例 `InnerAudioContext`，只切换 src
   - SFX：`AudioSourcePool` 预分8~16 个实例循环复。

3. **长短分离**。
   - **BGM**：`wx.createInnerAudioContext()`，流式加载，省内。
   - **SFX**：WebAudio API，快速触发，高频短音。

**接口设计**。
```csharp
public interface IAudioService {
    void PlayBGM(string name, float volume = 1f, bool loop = true);
    void StopBGM(float fadeOut = 0.5f);
    void PlaySFX(string name, float volume = 1f);
    void SetMasterVolume(float volume);
    void Mute(bool mute);
}
```

**生命周期挂起处理（微信审核红线）**

> ⚠️ **强制要求**：微信小游戏切后台（点胶接电话）时，必须静音且暂停逻辑，否*审核不通过**。

```csharp
// Bootstrap / GameManager 中注册
void OnEnable() {
    Application.focusChanged += OnFocusChanged;
}

void OnApplicationPause(bool isPaused) {
    if (isPaused) {
        // 切后台：强制静音 + 暂停
        AudioListener.pause = true;
        Time.timeScale = 0f;
        _pauseTimestamp = Time.realtimeSinceStartup;
        
        #if CY_WECHAT
        // 微信端额外处理
        WXAudioContext.PauseAll();
        #endif
    } else {
        // 切前台：恢复
        AudioListener.pause = false;
        Time.timeScale = 1f;
        
        // ⚠️ 关键：时间校准，防止逻辑"瞬移"
        float pauseDuration = Time.realtimeSinceStartup - _pauseTimestamp;
        if (pauseDuration > MAX_PAUSE_TOLERANCE) {
            // 超过阈值（>5 秒），重置逻辑时间，不追帧
            _gameplayWorld.ResetDeltaTime();
        }
    }
}
```

| 场景 | 处理 |
|------|------|
| **切后* | `AudioListener.pause = true` + `Time.timeScale = 0` |
| **切前< 5s** | 正常恢复 |
| **切前> 5s** | 重置 deltaTime，防止角色瞬技CD 归零 |

### 3.2 玩法核心(Gameplay Engine)

#### 3.2.1 抽象接口 (IGameplayWorld)

对外暴露统一 API，对内屏蔽实现差异。

```csharp
void FixedTick(float fixedDt);     // 固定逻辑(30/60Hz)
void HandleInput(InputData input); // 输入处理
RenderSnapshot GetRenderSnapshot(); // 获取渲染快照
```

**输入缓冲 (Input Buffering)**

> ⚠️ **陷阱**：`Input.GetKeyDown()` Update 帧重置，FixedUpdate 频率不同步。如果按键发生在两个 FixedTick 之间，逻辑层会**丢键**。

**方案**：Update 收集输入 压入队列 FixedTick 消费

```csharp
// View Layer (Update) - 收集输入
void Update() {
    if (Input.GetButtonDown("Jump")) {
        _inputBuffer.Enqueue(new InputCommand { 
            Type = InputType.Jump, 
            Timestamp = Time.time 
        });
    }
}

// Logic Layer (FixedTick) - 消费队列
void FixedTick(float dt) {
    while (_inputBuffer.TryDequeue(out var cmd)) {
        _logicSystem.ProcessCommand(cmd);
    }
    _logicSystem.Step(dt);
}
```

**Tick 策略：固定逻辑+ 渲染插*

| | 频率 | 职责 |
|-----|------|------|
| **Logic Layer** | FixedUpdate (30/60Hz) | 状态计算、物理、AI |
| **View Layer** | Update (变帧 | 插值渲染、动画混|

```csharp
// View 层插值示例：即使逻辑 30Hz，渲染也可 120Hz 丝滑
float alpha = (Time.time - _lastFixedTime) / Time.fixedDeltaTime;
renderPos = Vector3.Lerp(_snapshotPrev.Pos, _snapshotCurr.Pos, alpha);
```

> ⚠️ 插值会引入1 帧视觉延迟，对格音游可改用外推（Extrapolation。

#### 3.2.2 实现 A：OOP Lite (微信/低端机基

架构：SOA (Structure of Arrays) 风格。

数据：UnitData[] 数组存储核心数据。

逻辑：System 类通过简单的 for 循环遍历数组处理逻辑。

优势：极度轻量，Debug 方便，完全掌控内存分配。

#### 3.2.3 实现 B：Hybrid DOTS (PC/高端机增 [核心变更]

不再追求“全ECS”，而是采用混合模式降低开发难度。

大脑 (Brain - OOP)：复杂的技能判定、状态机、AI 决策树依然用 C# Class/Struct 编写。这部分代码可与“实A”复用。

肌肉 (Muscle - DOTS)：位置更新、物理碰撞、大规模 AOE 判定、视锥剔除等“计算密集型”任务，下放SystemBase IJobEntity 中并行执行。

**同步机制**。
- 每一帧，Brain 计算出的指令（如 MoveCommand）写`NativeQueue<T>`（单生产者模式）
- Job 系统读取队列执行，并将结果写`NativeArray<T>` Brain 下一帧决。
- 主线程在 `LateUpdate` 调用 `JobHandle.Complete()` 确保数据一。
- 双缓冲：读写分离避免竞争，Buffer A 供渲染读取，Buffer B Job 写入，帧末交。

### 3.3 数据桥接(Data Bridge) [NEW]

解决 ECS 数据难以UI 访问的问题。

#### 内存策略：三缓冲环形队列 (Triple Buffered Ring Queue)

**问题**：每`new RenderSnapshot()` 会产GC 压力。

**方案**：预分配 + 指针交换，零内存分配。

```csharp
// 三个预分配的快照容器
private RenderSnapshot[] _buffers = new RenderSnapshot[3];
private int _frontIdx = 0;  // 渲染端
private int _backIdx = 1;   // 逻辑端
private int _idleIdx = 2;   // 备用/过渡

// Snapshot 内部数组也是预分配固定长
public struct RenderSnapshot {
    public int Count;                    // 实际有效数量
    public int[] IDs;                    // 预分配MaxUnits
    public Vector3[] Positions;          // 预分配MaxUnits
    public Quaternion[] Rotations;
    public float[] HPs;
}

// 帧末只交换索引，零分配
public void SwapBuffers() {
    int temp = _frontIdx;
    _frontIdx = _backIdx;
    _backIdx = _idleIdx;
    _idleIdx = temp;
}
```

| 缓冲| 用| 访问|
|--------|------|--------|
| **Front** | 渲染读取 | View Layer (Update) |
| **Back** | 逻辑写入 | Logic Layer (FixedUpdate) |
| **Idle** | 过渡缓冲 | 用于平滑插值的上一|

#### Render Proxy 工作

1. 每一FixedUpdate 结束后，将数据写Back Buffer
2. SwapBuffers() 交换索引
3. View Layer Front Buffer 读取，结Idle Buffer 做插。

#### View Layer 消费

UI 根据 Snapshot 中的 ID 进行 Update。如ID 消失，则回收 UI 节点；如ID 新增，则Pool Spawn UI。

**收益**：彻底解+ GC。UI 随便写，不会因为访问了被销毁的 Entity 而导Crash。

## 4. CY 统一入口（以 `Runtime/CY.cs` 为准

### 4.1 设计原则

CY 直接暴露 Manager，无中间封装。

```csharp
// 直接访问 Manager 的全量 API
CY.Entity.ShowEntity("Enemy");    // 而不是 CY.Entity.Show()
CY.Entity.PauseEntity(id);
CY.UI.Open<ShopUI>();
CY.UI.ShowConfirm(...);
CY.Procedure.ChangeProcedure<T>();
```

### 4.2 核心服务一

| 入口 | 类型 | 说明 | 使用场景 |
|------|------|------|----------|
| `CY.Event` | EventBus | 事件系统 | 模块解耦通信 |
| `CY.Timer` | TimerManager | 计时| 技能冷却、定时刷|
| `CY.Procedure` | ProcedureManager | 流程管理 | 菜单→战斗→结算 |
| `CY.Entity` | EntityManager | 实体管理 | 敌人、子弹、特|
| `CY.UI` | UIManager | UI 面板 | 背包、商店、对话框 |
| `CY.Data` | DataTableManager | 数据| 配置表读|
| `CY.Audio` | IAudioService | 音频 | BGM、音|
| `CY.Save` | SaveService | 存档 | 进度保存 |
| `CY.Pool` | PoolManager | 对象| 复用 GameObject |
| `CY.Game` | GameEntryBase | 游戏入口 | 访问全局实例 |

> 实体位置设置建议：若实体带 `Rigidbody2D`，请避免只改 `transform.position`（会被物理回写）；推荐使用 `CY.Entity.SetEntityPosition2D(...)` 同步 Transform 与 Rigidbody2D，并在需要“本帧立刻做物理查询”时开启 `syncTransforms`。

### 4.3 扩展自定义系

CY partial class，游戏项目可扩展而不修改框架代码。

```csharp
// Assets/_Game/Scripts/Core/CY.Game.cs
namespace CYFramework
{
    public static partial class CY
    {
        private static QuestManager _quest;
        public static QuestManager Quest => _quest = Get<QuestManager>();
    }
}

// 使用
CY.Quest.AcceptQuest(1001);
```

## 5. 目录结构规范（以仓库实际为准

```
Assets/CYFramework/
├── Runtime/
  ├── CY.cs               # 统一入口（partial class，直接暴露 Manager
  ├── Infrastructure/     # 启动与服务定位、ServiceBase
  ├── Platform/           # 微信/PC 适配
  ├── Core/
    ├── Audio/          # 音频服务
    ├── Config/         # 配置定义与加载器
    ├── DataTable/      # 数据表管[NEW]
    ├── Entity/         # 实体管理 [NEW]
    ├── Event/          # 事件总线
    ├── FSM/            # 有限状态机
    ├── HotUpdate/      # 热更新管
    ├── Log/            # 日志系统
    ├── Network/        # 网络(HTTP/WS/适配
    ├── Pool/           # 对象
    ├── Procedure/      # 流程管理
    ├── Resource/       # 资源加载
    ├── Save/           # 存档系统
    ├── Timer/          # 计时器系
    └── UI/             # UI 框架 (MVVM)
        ├── UIManager.cs        # 面板管理
        ├── UIPanel.cs          # 面板基类
        ├── MVVM/               # ViewModel 数据绑定
        └── Components/         # 通用组件 (Toast/Dialog/Loading)
  ├── Gameplay/
    ├── Abstraction/    # IGameplayWorld, RenderSnapshot定义
    ├── Logic_Common/   # OOP与Hybrid共用的纯逻辑(状态机/AI)
    ├── Logic_OOP/      # 纯OOP驱动
    └── Logic_Hybrid/   # Hybrid DOTS驱动(Brain+Muscle)
  └── Debug/              # 运行时调试工
├── Editor/
  ├── Baking/             # 配置烘焙工具 (SO -> BlobAsset)
  ├── DebugTools/         # 编辑器调试面
  └── BuildPipeline/      # 构建流程扩展
└── Tests/                  # 单元测试与集成测
    ├── EditMode/
    └── PlayMode/
```

## 6. 关键工作
配置阶段：策划填Excel -> 导表工具生成 ScriptableObject -> 放入 Resources/Config。

开发阶段：

绝大多数业务逻辑（技能、流程）写在 Logic_Common 中（C#）。

Editor 模式下默认使Logic_OOP 运行，断点调试方便。

构建阶段 (CI/CD)。

构建微信版本：定义宏 CY_WECHAT。编译器剔除 DOTS 代码。打包系统将 SO 序列化进包体。

构建 PC 版本：定义宏 CY_PC + ENABLE_DOTS。

执行 Pre-Build Baking：将 SO 转换DOTS BlobAssets。

切换入口HybridGameplayWorld。

## 7. 性能红线 (Performance Budget)

| 指标 | 微信/WebGL | Mobile | PC |
|------|-----------|--------|----|
| **帧率范围** | 45~60 FPS | 60~90 FPS | 60~144 FPS |
| **Snapshot 封* | < 1.5ms | < 0.5ms | < 0.3ms |
| **DrawCall** | < 100 | < 300 | < 1000 |
| **Mono 堆内* | < 200MB | < 400MB | < 800MB |
| **每帧 GC Alloc** | 0 (Release) | 0 (Release) | < 1KB (Debug) |

**优化手段**。
- 仅拷贝视锥体（Frustum）内的单位数。
- UI 合批 + 动静分离
- 纹理图集 + Sprite Atlas
- LOD 分级 + 遮挡剔除

**平台差异化优**
| 平台 | Snapshot 策略 | 多线程|
|------|-----------------|----------|
| 微信/WebGL | C# for 循环 + 分帧处理 | 不支持|
| Mobile Native | Burst 编译 + Job 并行 | 支持 |
| PC | Burst + Job + SIMD | 支持 |

## 7.1 WebGL/微信小游戏平台限制清[NEW]

| 技| 支持情况 | 替代方案 |
|------|----------|----------|
| **Job System** | 不支持| C# for 循环 + 分帧处理 |
| **Burst Compiler** | ⚠️ 有限支持 | WebGL 下自动回退Mono，无 SIMD |
| **NativeArray/NativeQueue** | 不支持| 使用普通数`T[]` + 对象|
| **Span<T>/stackalloc** | ⚠️ .NET 4.x 不支持| 使用 `ArraySegment<T>` 或直接数组切|
| **System.IO 文件操作** | 不支持| `wx.getFileSystemManager` / IndexedDB |
| **System.Net.Sockets** | 不支持| 仅用 HTTP + WebSocket |
| **AppDomain** | 不支持| 使用 `Application.logMessageReceived`；流程系统推荐使Editor 生成的流程注册表替代运行时扫|
| **动态程序集加载** | 不支持| 不支持HybridCLR，只能资源热|
| **原生加密* | ⚠️ 部分不支持| C# AES 实现 JS 桥接 |
| **PlayerPrefs** | ⚠️ 大小受限 | 微信: `wx.setStorageSync` (上限 10MB) |
| **Addressables 本地* | 不支持| 必须使用远程 CDN 加载 |
| **多线程async 真并* | 不支持| 单线程协程模|

**框架应对策略**。
```csharp
// 示例：Snapshot 封送的平台分支
public void CopySnapshot()
{
#if UNITY_WEBGL || CY_WECHAT
    // WebGL: C# 分帧复制
    CopyBatch(_currentBatchIndex, BATCH_SIZE);
    _currentBatchIndex = (_currentBatchIndex + 1) % _totalBatches;
#else
    // Native: Burst + Job 并行
    var job = new SnapshotCopyJob { ... };
    job.Schedule(_count, 64).Complete();
#endif
}
```

## 7.2 流程注册表工作流（Procedure Registry

当你开启流程自动注册（例如 `AutoRegisterProcedures=true`）时，运行时会优先从 `Resources/CYFramework/ProcedureRegistry` 加载流程注册表完成注册，避免启动时扫描程序集。

推荐工作流：在你新增/修改流程后，Unity 菜单执行。

`CYFramework/Generate Procedure Registry`

该操作会生成/更新。
- `Assets/CYFramework/Resources/CYFramework/ProcedureRegistry.asset`
- `Assets/CYFramework/link.xml`（IL2CPP/Managed Stripping 裁剪保护，建议提交到版本库）

## 8. 错误处理与异常策[NEW]

### 8.1 全局异常捕获

```csharp
// 启动时注
Application.logMessageReceived += OnLogCallback;

// WebGL/微信不支AppDomain，需平台判断
#if !UNITY_WEBGL
AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
#endif
```

### 8.2 分级处理

| 级别 | 处理方式 |
|------|----------|
| **轻微** | 记录日志，继续运|
| **中等** | 弹窗提示，尝试恢复（如网络重连） |
| **严重** | 保存现场快照，强制重退|

### 8.3 Crash 上报

- 本地缓存崩溃日志，下次启动时上报
- 包含：设备信息、堆栈、最N 条日志、玩ID
- 微信端使`wx.reportMonitor` + 自建埋点

## 9. 调试与监[NEW]

### 9.1 运行Profiler 面板

内置轻量级调试面板（Development Build 可见）：
- **FPS / 帧时*：实时曲。
- **内存占用**：Mono / Native / 纹理
- **DrawCall / Batches**
- **对象池状*：各类型活跃/空闲数量
- **网络状*：延/ 包量 / 连接状。

### 9.2 命令控制(Cheat Console)

开发环境下通过特定手势/按键呼出。
- 加金道具
- 跳关/解锁全部
- 切换服务器环。
- 强制触发事件

### 9.3 日志分级

```csharp
public enum LogLevel { Trace, Debug, Info, Warning, Error, Fatal }
```

- **Development**：Trace 及以上全输出
- **Release**：Warning 及以+ 异步上报
- 微信端自动映射到 `console.log` / `console.warn` / `console.error`

## 10. 测试策略 [NEW]

### 10.1 测试分层

| 层级 | 范围 | 工具 |
|------|------|------|
| **单元测试** | Core Services / 纯逻辑 | Unity Test Framework (EditMode) |
| **集成测试** | 模块间交| Unity Test Framework (PlayMode) |
| **性能测试** | Tick 耗时 / GC / 内存 | Unity Profiler + 自定Benchmark |

### 10.2 Mock 策略

- 所有平台适配器通过接口注入，测试时替换Mock 实现
- 网络层支持本Mock Server 模式
- 存档系统支持内存存储 Mock

### 10.3 CI/CD 集成

```yaml
#### 示例 GitHub Actions
- name: Run Tests
  run: unity-editor -batchmode -runTests -testPlatform EditMode
- name: Build WebGL
  run: unity-editor -batchmode -executeMethod BuildScript.BuildWebGL
```

## 11. 里程碑规

| 阶段 | 目标 | 交付|
|------|------|--------|
| **M1** | 基础设施 | ServiceLocator + EventBus + Log + 对象|
| **M2** | 平台适配 | PC/微信适配+ 网络+ 存档 |
| **M3** | 玩法核心 | IGameplayWorld + OOP Lite 实现 |
| **M4** | 表现| UI 框架 + RenderProxy + Snapshot |
| **M5** | 性能增强 | Hybrid DOTS 实现 (PC  |
| **M6** | 工具| 配置烘焙 + 调试面板 + CI/CD |
