# CYFramework 2.2 框架审查报告（源码审查）

> 本报告基于静态源码阅读与文档对照，不包含运行时 Profiling / 真机联调结果；结论以代码实现为准。

**审查范围**
- `Assets/CYFramework/Runtime/**`（框架运行时代码）
- `Assets/CYFramework/Documentation/**`（使用/接口文档）
- `Assets/CYFramework/README.md`、`Assets/CYFramework/CYFramework.md`（对外说明与白皮书）

**环境信息**
- Unity：`2022.3.62f2c1`
- Git：`d0a264ba`
- 日期：`2025-12-15`

---

## 0. 结论摘要（TL;DR）

### 0.1 做得好的（建议保留的设计）
- **分层明确**：`Infrastructure / Platform / Core / UI(表现)` 目录划分清晰，读代码成本低。
- **统一入口**：`CY` 作为“服务聚合访问点”整体思路正确，且文件头部明确“不要无限堆快捷方法”。
- **生命周期调度集中**：`CYBootstrap` 统一驱动 `ITickable/IUpdateable/ILateUpdateable`，便于控制执行顺序与暂停逻辑。
- **EventBus 使用 struct + ref**：核心同步事件流可做到低 GC；并提供 `SubscribeAll/UnsubscribeAll` 的缓存扫描，易用性较好。
- **UIManager 迭代安全**：`UIManager` 在 `OnUpdate/OnLateUpdate` 使用 `_updateBuffer.AddRange(_openedPanels.Values)`，避免遍历中集合修改导致崩溃（这一点值得推广到其他模块）。
- **配置入口前置**：`CYConfigurator` 用 `DefaultExecutionOrder(-1100)` 早于 `CYBootstrap(-1000)`，解决“配置字段改了运行时无效”的常见坑。

### 0.2 高优先级问题（建议尽快修复）

#### P0（会导致功能错误/数据丢失/运行时崩溃）
1) **SaveService 校验和逻辑严重错误**  
`SaveDataBase.Checksum` 被标记为 `[NonSerialized]`，导致 `JsonUtility` 不会把校验和写入存档；加载时 `expectedChecksum` 永远为 `null`，会持续触发“校验失败”分支，并在存在备份时**错误回滚到旧备份**。  
相关文件：`Assets/CYFramework/Runtime/Core/Save/SaveService.cs`

2) **EntityManager 回收顺序导致实体表无法正确移除**  
`EntityManager.RecycleEntityInternal` 先调用 `entity.OnRecycle()`，而 `EntityBase.OnRecycle()` 会把 `Id` 重置为 `0`，导致 `_entities.Remove(entity.Id)` 实际执行的是 `Remove(0)`，原实体仍留在 `_entities`，造成**实体表膨胀、ID/实例不一致、后续遍历隐患**。  
相关文件：`Assets/CYFramework/Runtime/Core/Entity/EntityManager.cs`、`Assets/CYFramework/Runtime/Core/Entity/EntityManager.cs` 中的 `EntityBase`

3) **EntityManager Update/Tick 遍历 Dictionary.Values，极易在运行时崩溃**  
`Tick/OnUpdate/OnLateUpdate` 使用 `foreach (_entities.Values)`，而实体逻辑中常见操作是“生成/回收实体”，会修改 `_entities`，触发 `InvalidOperationException: Collection was modified`。  
相关文件：`Assets/CYFramework/Runtime/Core/Entity/EntityManager.cs`

#### P1（容易踩坑/文档与代码不一致/性能与可维护性风险）
4) **文档/API 参考与实际代码多处不一致**  
例如文档仍在使用 `CY.Entity.ShowEntity/HideEntity(entityId)`、`CY.Procedure.Current/CurrentName`、热更新方法名等；这会直接导致“照文档写代码无法编译”。  
相关文件：`Assets/CYFramework/Documentation/API_Reference.md`、`Assets/CYFramework/Documentation/Usage_Guide.md`、`Assets/CYFramework/Documentation/Lifecycle_Guide.md`、`Assets/CYFramework/README.md`

5) **CY 的“兜底创建服务”在 Timer/Procedure 上不完整**  
`CY.Timer` / `CY.Procedure` 在找不到服务时会 `new` 并 `RegisterInstance`，但 **不调用 `Initialize()`**；如果在 `ServiceLocator.InitializeAll()` 之后才触发该分支，会得到未初始化的 Manager（高概率 NRE）。  
相关文件：`Assets/CYFramework/Runtime/CY.cs`、`Assets/CYFramework/Runtime/Infrastructure/ServiceLocator.cs`

6) **UIManager 的“强类型 Open<T,TData>”注释与实现不一致（仍会装箱）**  
`Open<T, TData>(TData data) where TData : struct` 最终调用 `Open<T>(object data)`，值类型会装箱；建议要么移除该 overload，要么按“Typed UserData 接收器”真正做到无装箱。  
相关文件：`Assets/CYFramework/Runtime/Core/UI/UIManager.cs`

---

## 1. 架构与分层评估

### 1.1 目录结构与层次边界
- `Runtime/Infrastructure`：启动器、生命周期接口、ServiceLocator（基础设施层）。
- `Runtime/Platform`：平台适配接口与 Unity/WeChat 实现（Platform Adapter）。
- `Runtime/Core`：Event/Timer/UI/Entity/Resource/Save/Network 等核心服务（Core Services）。
- `Runtime/Debug`：开发期工具（Debug/Dev Tools）。
- `Runtime/Gameplay`：玩法抽象与实现（可选模块，注意与框架边界的耦合控制）。

整体符合 `CYFramework.md` 所描述的“分层/解耦/平台适配隔离”方向；当前主要问题不在宏观架构，而在**若干关键模块的实现细节与文档一致性**。

### 1.2 统一入口（CY）策略
`CY.cs` 的定位清晰：提供 Manager/Service 的聚合访问点，并明确“不要把入口做成上帝类”。  
但由于文档/外部约定与代码的实际 API 存在偏差（见第 5 节），建议明确两条路线之一：
- **路线 A：以代码为准，修正文档**（成本低、风险小）
- **路线 B：保留现状，同时提供兼容别名**（提高易用性，但需谨慎避免入口膨胀；推荐用 `partial` 在项目侧扩展）

---

## 2. 模块审查与问题清单（按模块）

> 本节“问题/建议”尽量只列出**会影响稳定性、平台兼容或长期维护成本**的点；纯风格/偏好项不展开。

### 2.1 Infrastructure：ServiceLocator / CYBootstrap / Lifecycle

**现状优点**
- `CYBootstrap` 集中初始化、集中调度，且对 WebGL/微信的 `AppDomain` 限制有编译期开关处理。
- `ServiceLocator` 支持 `Singleton/Scoped/Transient`、循环依赖检测（`_resolvingStack`）与 `InitOrder`。

**主要问题**
- `ServiceLocator.InitializeAll()` 的注释写“拓扑排序”，但实现 `BuildInitOrder()` 明确“不做依赖拓扑排序”；注释与实现不一致，会误导使用者（尤其是服务间依赖设计）。
- `ServiceLocator.RegisterInstance()` 在 `_initialized == true` 的情况下不会对 `IInitializable` 自动调用 `Initialize()`；而 `CY.cs` 中存在“兜底 new 并 RegisterInstance”的逻辑，容易产生“未初始化服务”。
- `ServiceLocator.ResolveInstance()` 对 `Transient` 也会触发 `OnServiceRegistered`；`CYBootstrap.RegisterLifecycle(object obj)` 会无差别把 `ITickable/IUpdateable/...` 加入列表，导致 **Transient 若实现这些接口可能被永久挂到生命周期里**（无法自动移除）。
- `ServiceLocator.ClearScoped()` 会 Dispose scoped 实例并置空，但不触发 `OnServiceUnregistered`，若未来引入“场景级服务 + 生命周期注册”，会导致生命周期列表里残留无效实例。

**建议（可落地）**
- 统一“服务创建/注册后是否自动 Initialize”的规则：  
  - 若框架允许运行中动态注册服务，建议在 `RegisterInstance` 内当 `_initialized==true` 时对 `IInitializable` 调 `Initialize()`（或提供显式 API）。
- 让 `OnServiceRegistered/OnServiceUnregistered` 携带更多信息（至少包含 `ServiceScope` 或 `ServiceType`），或对 `Transient` 默认不触发生命周期挂载事件。
- 修正/对齐 `ServiceLocator.InitializeAll` 的注释与 `CYFramework.md` 的“以 InitOrder 为准，不做拓扑排序”约定。

### 2.2 Event：EventBus

**现状优点**
- 同步事件 `Post(ref T)` 的基本路径较干净；事件订阅支持优先级插入。
- `SubscribeAll` 的反射扫描结果缓存，避免重复扫描。

**主要问题**
- `Unsubscribe<T>(handler)` 不会同步清理 `_targetSubscriptions`，会留下“目标对象 -> 已移除订阅”的悬挂引用，长期可能造成内存与逻辑噪音（`UnsubscribeAll` 时会再次处理这些订阅）。
- 延迟事件 `PostDelayed` 目前采用装箱 + 反射 `Invoke`，并且每次派发会产生 `object[]` 分配；虽然注释强调“低频使用”，但建议提供无反射路径（例如缓存委托）以降低误用成本。
- EventBus 作为 `ITickable` 在 `FixedUpdate` 驱动，“frames”语义更接近“逻辑帧”而非“渲染帧”；`PostNextFrame` 的命名可能产生误解。

**建议（可落地）**
- 在 `Unsubscribe<T>` 时同步移除 `_targetSubscriptions[target]` 中对应项（或改为只支持 `UnsubscribeAll`，并明确写入文档）。
- 若要保持 “NextFrame” 语义为渲染帧：将延迟派发逻辑迁移到 `IUpdateable`；否则把命名/注释改为“NextTick/NextLogicFrame”。

### 2.3 Timer：TimerManager

**主要问题**
- `Cancel/GetTimer/HasTimer` 使用 `List<Timer>.Find(t => ...)`，会产生闭包分配与线性扫描；如果 Timer 被业务大量用于冷却/倒计时，会出现不必要的 GC 与 O(n) 开销。
- `Timer.Update()` 在 `Duration <= 0` 时会出现除零（进度回调）或不明确行为；虽然默认不传 `onProgress` 时不触发，但属于隐蔽坑。
- 移除完成 Timer 的方式是先收集 `_toRemove` 再逐个 `_timers.Remove(timer)`，规模上来后会变成 O(n^2)。

**建议（可落地）**
- 用 `Dictionary<int, Timer>` 维护 id -> timer 映射，或至少把 `Find` 改为手写 for 循环避免闭包分配。
- 对 `Duration <= 0` 做显式处理：直接判定完成并把 progress 视为 1。
- 用倒序遍历删除/或“swap remove”减少移除成本。

### 2.4 Procedure：ProcedureManager / ProcedureRegistry

**主要问题**
- `StartEntry()` 在未配置入口流程时使用 `_procedureNames.Keys.First()`，字典 key 的枚举顺序不保证稳定；启动流程可能随运行环境变化。
- `ProcedureRegistryAsset` 使用字符串 `AssemblyQualifiedName` 运行时 `Type.GetType(...)` 创建实例：在 IL2CPP/裁剪（Managed Stripping）场景下可能被剔除，存在平台风险（尤其 WebGL/微信）。

**建议（可落地）**
- 未配置入口流程时，优先使用注册表的 `Order` 最小项，或在注册时维护一个按 order 排序的列表。
- 对 WebGL/微信：建议“生成注册表”的同时生成 `link.xml` 或生成一段带 `typeof(ProcedureXxx)` 的代码注册表来抗裁剪（保证类型被引用）。

### 2.5 Entity：EntityManager / EntityBase

**P0 关键问题**
- `RecycleEntityInternal` 的顺序问题（先 `OnRecycle` 再 `_entities.Remove(entity.Id)`）会导致实体无法从 `_entities` 移除。
- `Tick/OnUpdate/OnLateUpdate` 直接遍历 `_entities.Values`，在迭代中增删实体会崩溃。

**其他问题**
- `_maxPoolSize` 配置存在但没有在回收时生效，实体池可能无限增长。
- `_entityGroups` 实际含义更接近 “entityType -> instances”，名称容易误导为“分组/阵营”。

**建议（可落地）**
- 回收时先记录 `id = entity.Id`，先从 `_entities` 与列表中移除，再调用 `entity.OnRecycle()`（或让 `OnRecycle` 不改 Id）。
- 参考 `UIManager` 的做法，引入 `_updateBuffer`（List 复用）来遍历实体，或者做“延迟增删队列”在帧末统一 apply。
- `_maxPoolSize` 在 `pool.Enqueue` 前做上限判断，超出直接 `Destroy`（并确保从池根节点移除）。

### 2.6 UI：UIManager / UIPanel / MVVM

**现状优点**
- 面板栈、遮罩、对象池、层级容器整体完整度高。
- MVVM 同时提供 `ViewModel`（低频）与 `TypedViewModel + ObservableProperty<T>`（高频）两套方案，方向正确。

**主要问题**
- `UIManager.Open<T, TData>(TData data) where TData : struct` 仍会装箱（实现与注释不一致）。  
  注：Open/Close 属于低频操作，这个问题更多是“误导风险”而非性能瓶颈。

**建议（可落地）**
- 若要保留“强类型 UserData”，建议参照 Procedure 的 `IUserDataReceiver` 思路，为 `UIPanel` 提供类型化接收接口（如 `IUserDataReceiver<TData>`），并在 `Open<T,TData>` 中走无装箱路径；否则直接移除此 overload 并在文档中强调“UI 打开数据属于低频装箱可接受”。

### 2.7 Resource：ResourceLoader

**主要问题**
- `Unload`/`EvictIfNeeded` 内部直接调用 `Resources.UnloadUnusedAssets()`：这类调用可能造成明显帧尖刺，且从 API 使用者角度不易预期（“我只是 Unload 一个资源”）。
- `_loadingCallbacks` 以 `path` 作为 key，不区分 `T`，同一路径以不同类型请求时会出现回调拿到 `null` 的隐蔽问题。

**建议（可落地）**
- 将 `UnloadUnusedAssets` 的触发策略改为“由调用方显式触发/或在 Loading 场景集中触发”，默认只从缓存表移除。
- `_loadingCallbacks` key 改为 `(path, type)`，或明确文档约束“同一路径必须用同一类型加载”。

### 2.8 Save：SaveService

**P0 关键问题**
- `SaveDataBase.Checksum` 的 `[NonSerialized]` 会让校验机制失效，并导致“存在备份时回滚旧数据”的严重后果。

**其他问题**
- `SaveServiceConfig` 暴露的开关与 `SaveConfig` 内部字段不一致：例如没有 `EnableChecksum/EnableBackup/MaxBackupCount`，导致行为不可配置。
- `CreateBackup` 的日志调用 `CYLog.Warning(msg, ex.Message)` 第二参数会被当作 `tag`，异常信息丢失/混乱（属于易错细节）。

**建议（可落地）**
- 修复校验字段序列化：去掉 `[NonSerialized]`，改用 `[HideInInspector]` 或 `[SerializeField, HideInInspector]`；并在保存前确保 `Checksum` 置空再计算，避免“上一轮 checksum 参与本轮 checksum”的自引用问题。
- 对齐配置：要么在 `SaveServiceConfig` 增加相关开关，要么在代码/文档中明确“校验/备份为框架固定策略不可配置”。

### 2.9 Network：NetworkService

**关注点（建议在真机/平台联调验证）**
- NetworkService 是 `ITickable`（FixedUpdate 驱动）且会受到 `CYBootstrap` 的 `_isPaused` 影响；当 `Time.timeScale=0` 或应用进入后台时，心跳与重连逻辑会暂停。  
  这是否符合项目需求需要确认：  
  - 若“暂停=切后台”则合理；  
  - 若“暂停=游戏暂停菜单”但希望网络不断线，则需要改为使用 `unscaled time` 且不受 `_isPaused` 影响。

### 2.10 Pool：PoolManager / ObjectPool

**总体评价**
- PoolManager 的分组根节点组织与低内存回调比较完整；建议把实体池的 `_maxPoolSize` 也纳入同一套 PoolManager 策略（减少两套池逻辑的分裂）。

### 2.11 Scene：SceneLoader

**主要问题**
- `CancelLoading()` 并不能真正取消 `SceneManager.LoadSceneAsync`；当前实现会提前 `yield break`，但可能留下 `allowSceneActivation=false` 的异步操作停留在 0.9 的状态，造成内存与状态不可控风险。

**建议**
- 明确“不支持取消加载”，移除该 API 或改为“取消回调/不激活但仍让加载完成并立即卸载”之类的可控策略。

### 2.12 HotUpdate：HotUpdateService

**主要问题**
- `Documentation/API_Reference.md` 中热更新对外 API 名称与 `Runtime/Core/HotUpdate/HotUpdateService.cs` 不一致（文档列的是 `CheckForUpdateAsync/DownloadUpdateAsync/ApplyUpdateAsync`，代码实际为 `CheckUpdate/DownloadUpdate/ApplyUpdate`）。

---

## 3. 公共 API（CY 入口）与“够不够用”

### 3.1 当前 CY 暴露情况（以代码为准）
`CY.cs` 当前暴露：`Event/Timer/Procedure/Entity/UI/Data/Audio/Network/Save/Pool/Resource/Scene/FSM/Game` 以及 `Log/LogInfo/LogWarning/LogError`。

**缺口/不一致点**
- CY 未直接暴露 `IConfigLoader`、`IHotUpdateService` 等（可用 `CY.Get<T>()` 获取，但文档/直觉层面不统一）。
- 文档与团队约定中出现的 `CY.SaveData<T>() / CY.LoadData<T>() / CY.HasSave() / CY.DeleteSave()` 在代码里不存在（当前是 `CY.Save.Save/Load/Exists/Delete`）。
- 文档与示例大量使用的 `CY.Entity.ShowEntity/HideEntity(entityId)` 在代码里不存在或语义不同（当前是 `SpawnEntity/RecycleEntity/HideEntity(IEntity)`）。

### 3.2 建议的统一策略（推荐）
- **优先修正文档**：把“示例/教程/API_Reference”全部改为当前真实 API（见第 4 节）。
- **兼容层用 partial 放在项目侧**：如果确实需要 `ShowEntity/SaveData` 这类“团队口径 API”，推荐在 `Assets/_Game/Scripts/Core/` 下通过 `partial class CY` 做项目侧扩展，避免直接改框架核心造成冲突。

---

## 4. 文档与代码一致性清单（建议按优先级修）

> 原则：`Assets/CYFramework/Runtime/**` 为唯一信源；文档应跟随代码更新（Documentation as Truth）。

### 4.1 明显会导致“照文档写代码无法编译”的项（P1）
- `Assets/CYFramework/README.md`：示例中出现 `CY.Entity.ShowEntity<EnemyEntity>`，但代码无该 API。
- `Assets/CYFramework/Documentation/API_Reference.md`：
  - Entity：`ShowEntity/HideEntity(int)`、`HideAllEntities` 等与代码不符
  - Procedure：`Current/CurrentName` 与代码不符（实际为 `CurrentProcedure/CurrentProcedureName`）
  - HotUpdate：方法名与代码不符
- `Assets/CYFramework/Documentation/Usage_Guide.md`、`Assets/CYFramework/Documentation/Lifecycle_Guide.md`：多处沿用旧 API（`ShowEntity` 等）。

### 4.2 建议的修复方式
- 建议先在文档中新增一节“2.2 -> 2.2.x API 变更（Breaking Changes）”，标出：
  - `ShowEntity -> SpawnEntity`
  - `HideEntity(按ID) -> RecycleEntity/HideEntity(按实例)`
  - `Procedure.Current -> Procedure.CurrentProcedure` 等
- 再逐步把所有示例代码统一替换为“可直接编译”的真实 API。

---

## 5. 测试建议（补齐高风险模块的回归保护）

当前已有 EditMode 测试：`Assets/CYFramework/Tests/EditMode/ServiceLocatorTests.cs`（覆盖 ServiceLocator & EventBus 基本行为）。  
建议新增：
- SaveService：
  - “保存 -> 加载 -> 校验通过”的测试（覆盖 checksum 修复）
  - “存在备份时不应无条件回滚”的测试
- EntityManager：
  - 回收后 `_entities` 必须移除对应 id 的测试
  - OnUpdate 中 Spawn/Recycle 不应抛 `InvalidOperationException` 的测试（可用模拟实体/手动调用更新）

---

## 6. 结语

框架当前的架构方向是对的：入口统一、分层清晰、平台隔离意识也较强。  
真正影响落地与稳定性的点，集中在少数几个“关键路径实现细节”（Save/Entity/ServiceLocator）以及“文档与代码一致性”。建议先把 P0/P1 项修掉，再考虑更大规模的性能与易用性增强。

