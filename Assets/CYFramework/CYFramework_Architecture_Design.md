# CYFramework 多平台轻量高性能架构设计文档

> 版本：v1.0（核心架构设计）  
> 目标：围绕“多平台可用（含微信小游戏）、轻量化、以及在支持平台上可实现极致性能”这几个核心诉求，选择并设计一套合适的 Unity 游戏框架架构；在具体实现时，可根据项目需求自由选型或自研具体方案。

---

## 1. 设计目标与约束

### 1.1 设计目标

- **多平台统一**  
  - 支持：PC（Windows）、Mobile（iOS/Android）、WebGL、微信小游戏。  
  - 尽量复用同一套框架与业务代码，只在必要处做平台差异化。

- **轻量化与低侵入**  
  - 框架自身模块数量适中，引入的概念尽量简洁。  
  - 不强依赖庞大的第三方库，避免复杂的反射、依赖注入容器。  
  - 未启用的模块和子系统应可以方便裁剪，不增加运行时与包体负担。

- **高性能 & 易优化**  
  - 在所有平台上：基础实现避免不必要的 GC 和性能陷阱。  
  - 在支持 DOTS/多线程的平台上：可以挂载一套高性能玩法实现，充分利用 Job System + Burst。  
  - 结构上预留“瓶颈替换点”，让特定玩法逻辑可以按平台替换实现。

- **清晰的分层与职责边界**  
  - 将平台相关、框架核心、玩法抽象、玩法实现、编辑器工具等层次清晰拆分。  
  - 便于多人协作和长期维护。

### 1.2 主要约束

- **微信小游戏 / WebGL 约束**  
  - 多线程和 Burst 支持受限，不能指望完整 DOTS 技术栈在此平台发挥优势。  
  - 必须保证在单线程环境下，玩法核心也能以“数据批处理+低 GC”的方式高效运行。

- **Unity 引擎约束**  
  - 保持与 Unity 2022 LTS+ 版本兼容，避免使用过于实验性的 API。  
  - 框架不强制依赖 SRP/URP/HDRP 任意一种渲染管线。

---

## 2. 整体分层架构

架构分为四个主要层次 + 一个横切的编辑器工具层：

1. **Platform 层（平台抽象层）**  
   - 封装与平台强相关的能力（时间、文件、本地存储、网络、振动等）。
2. **Runtime Core 层（运行时核心层）**  
   - 提供模块管理、事件总线、对象池、时间与调度、日志与统计等基础能力。  
   - 与具体玩法无关，是所有项目共享的“框架内核”。
3. **Gameplay Abstraction 层（玩法抽象层）**  
   - 定义玩法核心的统一接口（如 `IGameplayWorld`），对上层屏蔽实现细节。  
   - UI、流程、网络等只依赖该接口，不直接面向 OOP 或 ECS 实现。
4. **Gameplay Implementation 层（玩法实现层）**  
   - 针对不同平台提供不同的玩法核心实现，例如：  
     - `GameplayWorldOOP`：单线程、全平台可用的轻量实现。  
     - `GameplayWorldDots`：仅在 PC/原生平台启用的 DOTS/ECS 高性能实现。
5. **Editor & Tooling 层（编辑器与工具层）**  
   - 提供调试面板、配置编辑器、性能分析辅助工具等，仅在编辑器中工作。

### 2.1 高层结构示意（逻辑）

```text
           +---------------------------+
           |      Game (业务逻辑)      |
           | UI / 流程 / 网络 / 剧情   |
           +---------------------------+
                        |
                        v
           +---------------------------+
           |  Gameplay Abstraction     |
           |  IGameplayWorld 接口等     |
           +---------------------------+
                        |
        +--------------+ +---------------------------+
        | OOP Impl    | | DOTS/ECS Impl (可选，仅PC) |
        | (全平台)     | | (多线程 + Burst)           |
        +--------------+ +---------------------------+
                        ^
                        |
           +---------------------------+
           |     Runtime Core 层       |
           |  模块管理/事件/对象池等   |
           +---------------------------+
                        ^
                        |
           +---------------------------+
           |     Platform 抽象层       |
           |   时间/存储/网络封装等    |
           +---------------------------+
```

### 2.2 目录结构

建议的工程目录结构如下：

```text
Assets/CYFramework/
├── Runtime/
│   ├── Platform/           # 平台抽象层（ITimeService、ILocalStorage 等）
│   ├── Core/               # 框架内核（模块管理、事件、对象池、定时器、日志）
│   └── Gameplay/
│       ├── Abstraction/    # 玩法抽象层（IGameplayWorld 等接口）
│       ├── OOP/            # OOP/DOD Lite 实现（全平台可用）
│       └── DOTS/           # DOTS/ECS 实现（仅 PC/原生，可选）
├── Editor/                 # 编辑器工具、调试面板
├── Resources/              # 框架自带资源（可选）
└── Samples/                # 示例场景与代码（可选）
```

### 2.3 命名规范

- **命名空间**
  - 运行时：`CYFramework.Runtime.Platform`、`CYFramework.Runtime.Core`、`CYFramework.Runtime.Gameplay`
  - 编辑器：`CYFramework.Editor`

- **接口命名**：以 `I` 开头，例如 `IModule`、`IGameplayWorld`、`ITimeService`。

- **模块/类命名**
  - 模块类：`XxxModule`，例如 `EventModule`、`TimerModule`。
  - 实现类：`XxxImpl` 或直接按功能命名，例如 `GameplayWorldOOP`、`UnityTimeService`。

- **文件命名**：与类名一致，一个文件一个主类。

---

## 3. Platform 层设计（平台抽象）

### 3.1 目标

- 隔离 Unity/微信/原生平台的差异，让上层代码只依赖统一接口。  
- 方便在不同平台裁剪能力，例如：微信小游戏中关掉不支持的功能（文件系统、多线程等）。

### 3.2 典型接口示例（设计层面）

> 以下为概念接口，实际实现时为 C# interface，并通过具体实现类适配 Unity / WeChat API。

- **时间服务 `ITimeService`**  
  - `float RealtimeSinceStartup { get; }`  
  - `float DeltaTime { get; }`  
  - `float UnscaledDeltaTime { get; }`

- **本地存储 `ILocalStorage`**  
  - `bool HasKey(string key)`  
  - `string GetString(string key, string defaultValue)`  
  - `void SetString(string key, string value)`  
  - `void Save()`

- **网络访问 `INetworkClient`（可选）**  
  - Http 请求、WebSocket 抽象接口。  
  - 微信小游戏中可根据限制裁剪功能。

- **设备能力（震动、剪贴板、通知等）**  
  通过 `IDeviceService` 统一封装，避免在业务层直接调用平台 SDK。

### 3.3 多平台映射

- **PC / 移动端原生**：直接使用 Unity 提供的 API 实现上述接口。  
- **WebGL / 微信小游戏**：通过 WebGL 接口或微信 JSBridge 实现能支持的子集，部分 API 空实现或降级处理。

---

## 4. Runtime Core 层设计（框架内核）

Runtime Core 层是 CYFramework 的“心脏”，不关心具体玩法，主要模块如下：

### 4.1 模块管理器（Module Manager）

- 统一管理所有框架模块的生命周期：
  - `Initialize()`：框架启动时按顺序调用。  
  - `Update(float deltaTime)`：每帧调用，仅对需要帧刷新的模块。  
  - `Shutdown()`：框架退出或场景切换时调用。

- **设计要点**：
  - 使用 `IModule` 接口（或类似命名），所有模块实现该接口或继承一个轻量基类。  
  - 内部使用 `List<IModule>`（或数组）维护模块更新顺序，避免字典查找。  
  - 只在启动时注册模块，运行中不频繁增删模块，降低复杂度和开销。  
  - 上层若频繁使用某模块，应在外部缓存引用，减少 `GetModule<T>()` 调用。

### 4.2 事件总线（Event Bus）

- 提供模块与业务之间的解耦通讯机制：
  - 支持基于结构体的强类型事件（例如 `struct DamageEvent`）。  
  - 订阅/取消订阅接口，支持多监听者。

- **性能要求**：
  - 事件分发尽量无装箱，无反射。  
  - 事件参数复用，对高频事件使用对象池，降低 GC 产生。  
  - 避免闭包导致的隐藏分配。

### 4.3 对象池（Object Pool）

- 提供通用对象池，用于复用：
  - 事件参数对象  
  - 消息体  
  - 临时列表、字典  
  - 各类运行时数据结构（如弹道对象、Buff 实例等）

- **策略**：
  - 限制最大容量，防止无限膨胀。  
  - 支持按需预热（例如在加载关卡时预创建一批常用对象）。

### 4.4 时间与调度器（Timer & Scheduler）

- 提供：
  - 延时调用、循环调用（Timer）。  
  - 分帧任务调度：将大任务拆成多帧执行，减小单帧耗时波动。

### 4.5 日志与统计（Log & Metrics）

- 封装 Unity 日志，增加：
  - 日志等级控制。  
  - 可选写入文件（在不受限的平台）。

- 基本性能统计：
  - 模块级别的更新耗时采样（开发模式）。  
  - 可通过调试窗口查看当前对象池使用、事件队列长度等关键信息。

---

## 5. Gameplay Abstraction 层设计（玩法抽象）

### 5.1 目标

- 为“玩法核心运行时”定义统一接口，对上层屏蔽实现细节。  
- UI、流程、网络等模块只依赖该接口，从而可以在不同平台切换实现而无需改动业务逻辑。

### 5.2 核心接口示例：`IGameplayWorld`

> 以下示例是按"战斗/单位管理"类型的玩法场景设计的，仅作参考。  
> 实际项目中应根据具体玩法类型（如卡牌、解谜、模拟经营等）定制接口方法。

- **初始化与销毁**  
  - `void Initialize(GameplayConfig config);`  
  - `void Shutdown();`

- **帧更新**  
  - `void Tick(float deltaTime);`  
  - Tick 内部负责调用各子系统（移动、AI、战斗、Buff 等）的更新逻辑。

- **实体/单位管理**  
  - `int SpawnUnit(UnitSpawnInfo info);` 返回实体 ID。  
  - `void DespawnUnit(int unitId);`

- **输入与命令处理**  
  - `void HandleInput(PlayerInput input);`  
  - `void ExecuteCommand(GameplayCommand cmd);`

- **结果查询与状态获取**  
  - `BattleResult GetBattleResult();`  
  - `UnitSnapshot GetUnitSnapshot(int unitId);`

> UI 和流程通过 `IGameplayWorld`：
> - 向下发起：生成单位、下技能命令、暂停、快进等。  
> - 向上查询：战斗结果、单位状态，用于显示血条、结算界面等。

---

## 6. Gameplay Implementation 层设计（玩法实现）

玩法实现层提供一个或多个 `IGameplayWorld` 的具体实现，根据平台选择合适版本。

### 6.1 实现 A：OOP / DOD Lite 单线程实现（全平台基线）

#### 6.1.1 设计目标

- 在 **所有平台**（含 WebGL/微信小游戏）可用，是默认实现。  
- 尽可能借鉴数据导向思想：
  - 关键数据集中存储，方便批量更新和缓存友好。  
  - 控制 GC 和虚调用，保证单线程下也有良好性能。

#### 6.1.2 数据表示

- 使用 `struct` 和简单类表示单位数据，例如：
  - `UnitData`：位置、朝向、速度、血量、阵营、状态标记等。  
  - 所有单位存放在 `List<UnitData>` 或数组中，根据 ID 映射索引。

- 用附加结构表示：
  - 技能冷却表、Buff 列表等，可使用字典或压缩结构（按需优化）。

#### 6.1.3 逻辑更新

- 每帧在 `Tick` 中：
  - 循环遍历单位数组，按系统顺序执行：
    - 输入/命令处理  
    - AI 决策  
    - 移动与物理  
    - 战斗伤害计算  
    - Buff 更新

- 所有更新逻辑写成纯 C# 循环：
  - 避免 LINQ、反射、多层虚函数。  
  - 将大逻辑拆成多个小函数，保持可读性。

### 6.2 实现 B：DOTS/ECS 高性能实现（PC/原生可选）

#### 6.2.1 设计目标

- 仅在支持 DOTS 的平台启用（PC/原生）。  
- 充分使用：
  - Unity.Entities（ECS）  
  - Job System  
  - Burst

#### 6.2.2 与抽象层的关系

- `GameplayWorldDots` 同样实现 `IGameplayWorld`：
  - 内部维护一个或多个 `World` 与 `SystemGroup`。  
  - 在 `Tick` 中驱动 ECS 世界更新。  
  - 对外行为与 `GameplayWorldOOP` 尽量保持一致，保证 UI 与流程无需感知差异。

#### 6.2.3 平台隔离

- DOTS 实现代码放至单独 asmdef：
  - 仅在 PC / 原生平台的 Assembly Definition 中勾选。  
  - WebGL/微信小游戏的构建中不包含该 asmdef 与相关包。

- 使用条件编译或构建配置：
  - 在启动时根据运行平台选择使用 `GameplayWorldOOP` 或 `GameplayWorldDots`。

---

## 7. 多平台切换策略

### 7.1 构建与条件编译

- 使用 Unity 的 **Assembly Definition + Scripting Define Symbols** 完成不同平台功能裁剪：
  - `CYFramework.Core`：始终勾选，所有平台共享。  
  - `CYFramework.Gameplay.OOP`：始终勾选，作为基线实现。  
  - `CYFramework.Gameplay.DOTS`：仅在 PC/原生平台勾选。

- 在代码层面：

```csharp
public static class GameplayWorldFactory
{
    public static IGameplayWorld Create(GameplayConfig config)
    {
        IGameplayWorld world;

    #if UNITY_WEBGL || WECHAT_MINIGAME
        // 小游戏/浏览器等受限平台，使用 OOP 实现
        world = new GameplayWorldOOP();
    #else
        // 支持 DOTS 的平台，可以按配置选择 OOP 或 DOTS 实现
        if (config.UseDotsImplementation)
            world = new GameplayWorldDots();
        else
            world = new GameplayWorldOOP();
    #endif

        world.Initialize(config);
        return world;
    }
}
```

> 说明：上面代码为设计示意，实际版本会加入错误处理与空实现保护。

### 7.2 数据与配置共享

- 所有玩法实现共用一套配置数据结构：
  - 角色表、技能表、关卡表、AI 配置等。  
  - 通过 ScriptableObject 或表格（CSV/JSON）加载至统一数据结构。  
  - OOP 实现与 DOTS 实现分别将这些配置映射到各自的数据布局中。

- 这样可以保证：
  - 调整配置只需要改一处，所有平台行为保持一致（逻辑差异仅来自性能实现）。

---

## 8. 框架初始化与运行流程

### 8.1 初始化流程（伪逻辑）

1. Unity 场景中挂载 `CYFrameworkEntry` 组件。  
2. `CYFrameworkEntry.Awake`：
   - 初始化 Platform 层服务（时间、存储、日志输出等）。  
   - 初始化 Runtime Core 层模块管理器、事件总线、对象池等。  
   - 读取全局配置（如是否启用 DOTS 实现）。
3. 创建 `IGameplayWorld` 实例（通过工厂函数）。  
4. 进入第一个游戏流程（如主菜单/登录/战斗准备等）。

### 8.2 每帧运行流程

1. `CYFrameworkEntry.Update`：
   - 计算 deltaTime，并传递给模块管理器。  
   - 按顺序更新 Runtime Core 中需要刷新的模块（计时器、事件队列、下载队列等）。
2. 调用 `IGameplayWorld.Tick(deltaTime)` 更新玩法核心世界。  
3. 根据玩法状态更新 UI、音频等模块。

### 8.3 退出与清理

- 在应用退出或场景切换时：
  - 顺序调用模块的 `Shutdown()`。  
  - 调用 `IGameplayWorld.Shutdown()`，清理所有实体与内部资源。  
  - 释放对象池与缓存数据（视项目需求决定重用或清空）。

---

## 9. 性能与轻量化原则（汇总）

1. **模块管理**：
   - 只在启动阶段注册模块，运行时不做结构性修改。  
   - 千万不要在 Update 中新增/删除模块列表中的项。

2. **事件系统**：
   - 使用结构体事件 + 强类型委托。  
   - 事件参数可池化，减少 GC。  
   - 杜绝在高频路径中使用反射或通用 `object` 事件参数。

3. **玩法实现（OOP 基线）**：
   - 单位数据集中存储，使用 for 循环批量更新。  
   - 控制继承层级，减少虚调用链深度。  
   - 谨慎使用 LINQ、匿名函数和闭包。

4. **玩法实现（DOTS 增强）**：
   - 仅在支持平台使用，多线程 + Burst 用于极致优化。  
   - 不影响低端平台代码路径。

5. **资源与内存管理**：
   - 对频繁创建/销毁的对象使用对象池。  
   - 定期检查和释放长时间未使用的资源引用。

6. **调试开关**：
   - 所有调试与统计逻辑必须可通过配置开关关闭。  
   - 发布版本默认关闭调试开关，以保证最小额外开销。

---

## 10. 后续扩展方向

- **热更新支持**：
  - 在该架构基础上引入脚本热更（如 Lua、ILRuntime、HybridCLR），将部分 Gameplay 层逻辑外移。  
  - 需要设计稳定的接口层，保证热更逻辑与核心 runtime 解耦。

- **工具链与可视化**：
  - 开发可视化的战斗调试工具：查看单位状态、事件流、性能曲线。  
  - 对配置表和关卡编辑器做编辑器扩展支持。

- **多进程/多实例扩展**（长远规划）：
  - 在 PC 平台上尝试将某些重逻辑拆分到独立进程或服务器上处理，通过网络协议与客户端交互。

---

> 本文档为 CYFramework 的总体架构设计说明，后续可在此基础上：
> - 添加各个模块的详细接口说明（Core、Event、Pool、Timer、Log 等）。  
> - 添加具体玩法项目的补充设计（例如战斗系统、关卡系统、任务系统等）。
