# CYFramework 2.2 绝对开发铁律 (The Absolute Code)

> ⚠️ **警告**：此文件定义了 CYFramework 的最高宪法。所有代码实现必须以此为基准，任何偏离最优解的行为都将被视为“不专业”并触发惩罚机制。

## 0. 框架进化协议 (Framework Evolution Protocol) - **Top Priority**

*   **框架完整性原则**：
    *   在实现功能时，如果发现现有框架 API (`CY.XXX`) 无法支持，或设计文档 `CYFramework.md` 与实际需求冲突，**严禁** 编写“临时代码” (Hack) 来绕过框架。
    *   ✅ **正确流程**：
        1.  **评估**：确认是框架能力缺失，还是使用方式错误。
        2.  **修改框架**：深入框架底层 (`Assets/CYFramework/Runtime/...`) 进行扩展或修复。
        3.  **同步文档**：修改代码后，**必须**同步更新 `Assets/CYFramework/CYFramework.md` 中的 API 定义和设计理念，保持文档即真理 (Documentation as Truth)。
        4.  **一致性检查**：确保新 API 遵循整体代码风格（如 `Manager` 模式、`Service` 接口、`InitOrder` 等）。

*   **API 真实性原则 (No Hallucination)**:
    *   **严禁** 臆造、猜测框架 API。在使用任何 `CY.XXX` 或 Manager 方法前，**必须**先确认其存在（查阅代码或文档）。
    *   如果记不清 API，**必须**先使用工具（如 `grep`/`findstr`/`ls`）查找定义，或者查阅系统索引，**绝对不允许**直接写出“觉得应该有”的代码。
    *   **Fatal Error 示例**：
        *   `CY.UI.OpenPanel()` -> 实际 API 为 `CY.UI.Open()`
        *   `CY.Audio.PlayMusic()` -> 实际 API 可能需要传参或名称不同
    *   **惩罚机制**：凡是编写了不存在的 API 导致编译错误的，一律视为严重违规，必须立即开启自我检讨模式。

## 1. 核心架构地图 (Architecture Map)

所有新文件必须严格放置在指定目录，严禁随意存放。

| 模块类型 | 必选路径规范 (`Assets/CYFramework/Runtime/...`) | 命名规范 |
| :--- | :--- | :--- |
| **核心服务** | `Core/{ModuleName}/{Name}Manager.cs` | `XxxManager` (实现了 `IService`) |
| **实体逻辑** | `Core/Entity/{Name}Entity.cs` | `XxxEntity` (继承自 `EntityBase`) |
| **UI 面板** | `Core/UI/Panels/{Name}Panel.cs` | `XxxPanel` (继承自 `UIPanel`) |
| **数据表** | `Core/DataTable/Rows/{Name}Row.cs` | `XxxRow` (继承自 `DataRowBase`) |
| **工具类** | `Utility/{Name}Utility.cs` | `XxxUtility` (static class) |

*   **业务层代码**（非框架核心）应位于 `Assets/_Game/Scripts/...`，但必须遵循相同的目录结构。

## 2. 框架全域索引 (Framework Full Index)

> **使用指南**：在需要修改框架底层或查找 API 定义时，从下方目录树中定位。

### 2.1 基础设施 (Infrastructure)
*   **入口**: `Assets/CYFramework/Runtime/CY.cs` (所有 Manager 的静态访问点)
*   **核心基类**: `Assets/CYFramework/Runtime/Infrastructure/` (ServiceBase, IInitializable, IService, ServiceLocator)
*   **平台适配**: `Assets/CYFramework/Runtime/Platform/` (WeChat, WebGL, Standalone)
*   **调试工具**: `Assets/CYFramework/Runtime/Debug/` (RuntimeProfiler, CheatConsole)

### 2.2 核心服务模块 (Core Services)
*   **实体系统**: `Assets/CYFramework/Runtime/Core/Entity/` (EntityManager, EntityBase, IEntity)
*   **UI 系统**: `Assets/CYFramework/Runtime/Core/UI/` (UIManager, UIPanel, MVVM/BindableProperty)
*   **事件系统**: `Assets/CYFramework/Runtime/Core/Event/` (EventBus, GameEvent)
*   **数据表**: `Assets/CYFramework/Runtime/Core/DataTable/` (DataTableManager, DataRowBase)
*   **音频系统**: `Assets/CYFramework/Runtime/Core/Audio/` (AudioManager, IAudioService)
*   **配置系统**: `Assets/CYFramework/Runtime/Core/Config/` (ConfigManager, BlobAsset)
*   **有限状态机**: `Assets/CYFramework/Runtime/Core/FSM/` (FSMManager, FSMState)
*   **游戏入口**: `Assets/CYFramework/Runtime/Core/GameEntry/` (GameEntryBase)
*   **热更新**: `Assets/CYFramework/Runtime/Core/HotUpdate/` (HotUpdateManager)
*   **日志系统**: `Assets/CYFramework/Runtime/Core/Log/` (LogManager)
*   **网络系统**: `Assets/CYFramework/Runtime/Core/Network/` (NetworkService, Http/WebSocket)
*   **对象池**: `Assets/CYFramework/Runtime/Core/Pool/` (PoolManager)
*   **流程管理**: `Assets/CYFramework/Runtime/Core/Procedure/` (ProcedureManager, ProcedureBase)
*   **资源加载**: `Assets/CYFramework/Runtime/Core/Resource/` (ResourceLoader, AssetBundle)
*   **存档系统**: `Assets/CYFramework/Runtime/Core/Save/` (SaveService)
*   **场景管理**: `Assets/CYFramework/Runtime/Core/Scene/` (SceneManager)
*   **计时器**: `Assets/CYFramework/Runtime/Core/Timer/` (TimerManager)

### 2.3 玩法逻辑 (Gameplay)
*   **逻辑抽象**: `Assets/CYFramework/Runtime/Gameplay/`
    *   `/Abstraction/`: IGameplayWorld, RenderSnapshot
    *   `/Logic_Common/`: 纯 C# 逻辑 (AI, FSM)
    *   `/Logic_Hybrid/`: DOTS 混合实现 (SystemBase, Job)

## 3. 性能红线与最优解 (Performance Optimizations)

始终假设目标平台为只会跑由 JS 驱动的单线程环境（微信小游戏）。

### 3.1 组件获取 (Component Access) - **Fatal Level**
*   ❌ **禁止**：`GetComponent<T>()` (在 `Update`/`FixedUpdate` 中)。
*   ❌ **禁止**：`transform.Find()` / `GameObject.Find()` (在任何运行时逻辑中)。
*   ✅ **最优解**：
    1.  **Self-Caching**: `EntityBase` 子类必须在 `OnEntityInit` 中缓存自身组件。
    2.  **External-Exposure**: 若其他脚本需高频访问（如 `Collider`），必须用 `public T Component { get; private set; }` 暴露，**严禁**让外部脚本调用你的 `GetComponent`。
    3.  **Global-Caching**: 全局唯一对象，必须在 Manager 初始化时缓存，并提供 `public static/instance` 访问点。

### 3.2 内存分配 (Memory Allocation) - **Critical Level**
*   ❌ **禁止**：`new List<T>()` / `new Dictionary<T,K>()` (在热点路径)。
*   ❌ **禁止**：`params object[]` (导致数组分配)。
*   ❌ **禁止**：`Action/Func` 闭包捕获局部变量 (导致隐式 class 分配)。
*   ✅ **最优解**：
    1.  **Pool**: 使用 `CY.Pool` 复用集合或对象。
    2.  **Clear() over New**: 永远优先调用 `Clear()` 而不是创建新容器。
    3.  **Struct Iterators**: 自定义数据结构应提供 `struct` 枚举器以避免装箱。

### 3.3 字符串操作 (String Operations) - **High Level**
*   ❌ **禁止**：`string + string` (产生大量垃圾)。
*   ❌ **禁止**：`Debug.Log($"HP: {hp}")` (在 Release 模式下即使不打印也会分配字符串)。
*   ✅ **最优解**：
    1.  **ZString**: 如果项目集成了 `ZString/StringBuilder`，必须使用。
    2.  **Conditional**: Log 必须包裹在 `CY.Log` 中（框架层已处理宏剔除）。

## 4. 实体交互规范 (Entity Interaction)

### 4.1 移动与物理 (Physics & Locomotion)
*   **问题**：直接修改 `transform.position` 会导致物理引擎由“瞬移”产生巨大的错误的力。
*   **最优解**：
    *   刚体移动：必须使用 `Rigidbody2D.MovePosition` 或修改 `velocity`。
    *   距离检测：**严禁**使用 `Vector3.Distance` 检测碰撞体。必须使用 `Collider2D.ClosestPoint(targetPos)` 计算边缘距离。
    *   防抖：当 `dist < 0.01f` 时，强制 `velocity = zero`，防止物理引擎死锁抖动。

## 5. 框架入口规范 (API Best Practices)

*   **CY.Entity**: 实体生成/回收。
    *   `SpawnEntity<T>`: 自动从池中取。
    *   `RecycleEntity`: **必须**手动调用回收，严禁 `Destroy`。
*   **CY.Event**: 消息总线。
    *   必须使用 struct 作为事件参数（零 GC）。
    *   必须在 `Dispose/OnEntityHide` 中 `Unsubscribe`。
*   **CY.Data**: 配置读取。
    *   严禁在循环中调用 `GetRow`。必须在 `Init` 阶段缓存 `DataRow` 引用。

## 6. 自我修正协议 (Self-Correction Protocol)

*   在编写代码前，启动**静态分析模式**：
    *   *“这个功能所属模块位置在哪？”* -> 查阅 **2. 框架全域索引**。
    *   *“这个变量需要缓存吗？”* -> 是 -> 加到 `Init`。
*   如果用户指出了性能问题或者框架缺失：
    *   承认错误。
    *   利用索引快速定位底层文件。
    *   扩展框架底层并同步文档。

---
*Compliance is Mandatory. Incompetence is Punishable.*
