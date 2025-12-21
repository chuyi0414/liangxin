# CYFramework 生命周期指南

## 总览

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Unity 启动                                    │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      CYBootstrap (框架驱动器)                            │
│  ─────────────────────────────────────────────────────────────────────  │
│  职责：                                                                 │
│  • 初始化 ServiceLocator                                               │
│  • 驱动所有注册的生命周期对象                                            │
│  • 转发 Update/FixedUpdate/LateUpdate                                  │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      GameEntryBase (游戏入口)                            │
│  ─────────────────────────────────────────────────────────────────────  │
│  职责：                                                                 │
│  • 初始化游戏子系统                                                     │
│  • 注册游戏流程                                                         │
│  • 启动第一个流程                                                       │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    ▼               ▼               ▼
            ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
            │ Procedure   │ │   Entity    │ │     UI      │
            │   流程系统   │ │   实体系统   │ │   UI系统    │
            └─────────────┘ └─────────────┘ └─────────────┘
```

---

## 1. 框架层生命周期

### CYBootstrap（最底层驱动）

```csharp
// 自动创建，无需手动操作
// 执行顺序：-1000（最先执行）

Awake()     → 初始化 ServiceLocator，注册平台适配器
Start()     → 无
FixedUpdate → 驱动所有 ITickable.Tick()
Update      → 驱动所有 IUpdateable.OnUpdate()
LateUpdate  → 驱动所有 ILateUpdateable.OnLateUpdate()
OnDestroy   → 调用所有 IDisposableEx.Dispose()
```

### 核心服务（自动管理）

| 服务 | 生命周期 | 说明 |
|------|---------|------|
| `EventBus` | 全局单例 | 无需 Update |
| `TimerManager` | `IUpdateable` | 框架自动驱动 |
| `ProcedureManager` | `IUpdateable` | 框架自动驱动 |
| `EntityManager` | `IUpdateable` | 框架自动驱动 |
| `UIManager` | `IInitializable` | 无需 Update |

---

## 2. 游戏层生命周期

### GameEntryBase（游戏入口）

配置选项：
| 属性 | 默认值 | 说明 |
|------|--------|------|
| `AutoRegisterProcedures` | `false` | 自动注册 [AutoRegisterProcedure] 标记的流程（运行时优先从流程注册表加载） |
| `AutoSubscribeEvents` | `true` | 自动扫描 [OnEvent] 标记的方法 |

```csharp
public class LiangXinGame : GameEntryBase
{
    // 开启自动注册流程
    protected override bool AutoRegisterProcedures => true;
    
    // 1. 初始化子系统
    protected override void OnGameInit()
    {
        // 加载配置、创建管理器
    }
    
    // 2. 注册流程（AutoRegisterProcedures=false 时需要实现）
    protected override void RegisterProcedures()
    {
        CY.Procedure.AddProcedure<MenuProcedure>("Menu");
    }
    
    // 3. 启动
    protected override void OnGameStart()
    {
        CY.Procedure.Start("Menu");  // 按名称启动
    }
    
    // 4. 事件处理（[OnEvent] 自动订阅）
    [OnEvent]
    private void OnGameOver(ref GameOverEvent evt) { }
    
    // 5. 关闭（可选）
    protected override void OnGameShutdown() { }
}
```

当你新增/修改流程后，推荐在 Unity 菜单执行：

`CYFramework/Generate Procedure Registry`

该操作会生成：`Assets/CYFramework/Resources/CYFramework/ProcedureRegistry.asset`。
同时会生成/更新：`Assets/CYFramework/link.xml`（IL2CPP/Managed Stripping 裁剪保护，建议提交到版本库）。
运行时 `ProcedureManager` 会优先从 `Resources/CYFramework/ProcedureRegistry` 加载注册表完成注册，避免启动时扫描程序集。
在 `WebGL/微信` 平台（不支持无参自动扫程序集）也可以正常工作。

### 流程生命周期（ProcedureBase）

使用 `[AutoRegisterProcedure]` 自动注册：

```csharp
[AutoRegisterProcedure("Battle", order: 2)]  // 名称 + 执行顺序
public class BattleProcedure : ProcedureBase
{
    // 进入流程时
    protected override void OnEnter(ProcedureBase prev) { }
    
    // 每帧更新（自动被 ProcedureManager 驱动）
    protected override void OnUpdate(float deltaTime) { }
    
    // 离开流程时
    protected override void OnLeave(ProcedureBase next) { }
    
    // 切换流程
    void SomeMethod()
    {
        ChangeProcedure<VictoryProcedure>();  // 按类型
    }
}
```

---

## 3. Entity 实体生命周期

### EntityBase（实体基类）

```csharp
public class EnemyEntity : EntityBase
{
    // EntityType 由 EntityManager 在 Spawn 时注入，无需 override
    
    // 1. 初始化（首次创建时）
    protected override void OnEntityInit(object userData) { }
    
    // 2. 显示（从池中取出时）
    protected override void OnEntityShow(object userData)
    {
        // 初始化状态、位置等
    }
    
    // 3. 固定帧更新 - FixedUpdate（物理/AI 逻辑）
    protected override void OnEntityFixedUpdate(float deltaTime)
    {
        // 物理计算、AI 决策
    }
    
    // 4. 每帧更新 - Update（常规逻辑）
    protected override void OnEntityUpdate(float deltaTime)
    {
        // 移动、攻击等逻辑
    }
    
    // 5. 延迟更新 - LateUpdate（相机跟随等）
    protected override void OnEntityLateUpdate(float deltaTime)
    {
        // 在所有 Update 后执行
    }
    
    // 6. 隐藏（回收到池中）
    protected override void OnEntityHide()
    {
        // 清理状态
    }
    
    // 7. 回收（对象池复用前）
    protected override void OnEntityRecycle() { }
}
```

### 使用流程

```
CY.Entity.RegisterEntity("Enemy", prefab)  →  预创建对象池
            ↓
CY.Entity.SpawnEntity("Enemy", data)  →  OnEntityInit → OnEntityShow
            ↓
        [每帧自动]  →  OnEntityUpdate
            ↓
CY.Entity.RecycleEntity(entity)  →  OnEntityHide → OnEntityRecycle
            ↓
        [回到对象池等待复用]
```

---

## 4. UI 生命周期

### UIPanel（面板基类）

```csharp
public class BattlePanel : UIPanel
{
    // 1. 绑定 UI（打开时自动调用）
    protected override void OnBindUI()
    {
        // 绑定按钮事件等
    }
    
    // 2. 打开（Open 时）
    protected override void OnOpen(object userData)
    {
        // 刷新数据
    }
    
    // 3. 每帧更新 - Update（动画、计时器等）
    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        // UI 动画、倒计时等
    }
    
    // 4. 延迟更新 - LateUpdate（位置跟随等）
    protected override void OnLateUpdate(float elapseSeconds, float realElapseSeconds)
    {
        // 跟随目标位置等
    }
    
    // 5. 关闭（Close 时）
    protected override void OnClose(bool isShutdown, object userData)
    {
        // 停止动画等
    }
    
    // 6. 解绑 UI（关闭时自动调用）
    protected override void OnUnbindUI()
    {
        // 解绑事件
    }
}
```

### 使用流程

```
CY.UI.Open<BattlePanel>()  →  OnBindUI → OnOpen
            ↓
        [每帧自动]  →  OnUpdate → OnLateUpdate
            ↓
CY.UI.Close<BattlePanel>()  →  OnClose → OnUnbindUI
            ↓
        [缓存到对象池]
            ↓
CY.UI.Open<BattlePanel>()  →  OnBindUI → OnOpen（复用）
```

---

## 5. 自定义子系统

### 方式一：继承 ServiceBase（推荐）

```csharp
// 只重写需要的方法！
public class WaveManager : ServiceBase
{
    public override void Initialize()
    {
        // 初始化
    }
    
    public override void OnUpdate(float deltaTime)
    {
        // 每帧更新
    }
    
    // 其他方法不需要就不用写
}

// 注册
var waveManager = new WaveManager();
CYBootstrap.Instance.RegisterLifecycle(waveManager);
```

### 方式二：按需实现接口

```csharp
// 只实现你需要的接口
public class SimpleManager : IUpdateable
{
    public int UpdateOrder => 0;
    
    public void OnUpdate(float deltaTime)
    {
        // 每帧更新
    }
}
```

---

## 6. 生命周期时序图

```
Unity Start
    │
    ├─→ CYBootstrap.Awake()         ← 框架初始化
    │       └─→ ServiceLocator 初始化
    │
    ├─→ GameEntryBase.Start()       ← 游戏初始化
    │       ├─→ OnGameInit()        ← 初始化子系统
    │       ├─→ RegisterProcedures() ← 注册流程
    │       └─→ OnGameStart()       ← 启动第一个流程
    │
    └─→ 进入主循环
            │
            ├─→ FixedUpdate (固定帧率)
            │       └─→ ITickable.Tick()      ← 物理/AI
            │
            ├─→ Update (变帧率)
            │       ├─→ TimerManager.OnUpdate()
            │       ├─→ ProcedureManager.OnUpdate()
            │       │       └─→ 当前流程.OnUpdate()
            │       ├─→ EntityManager.OnUpdate()
            │       │       └─→ 所有实体.OnEntityUpdate()
            │       └─→ 自定义 IUpdateable.OnUpdate()
            │
            └─→ LateUpdate
                    └─→ ILateUpdateable.OnLateUpdate() ← 相机等
```

---

## 7. 快速参考表

| 你要做什么 | 用什么 | 生命周期方法 |
|-----------|--------|-------------|
| 游戏入口 | `GameEntryBase` | `OnGameInit`, `RegisterProcedures`, `OnGameStart` |
| 游戏流程 | `ProcedureBase` | `OnEnter`, `OnUpdate`, `OnLeave` |
| 游戏实体 | `EntityBase` | `OnEntityShow`, `OnEntityFixedUpdate`, `OnEntityUpdate`, `OnEntityLateUpdate`, `OnEntityHide` |
| UI 面板 | `UIPanel` | `OnBindUI`, `OnOpen`, `OnUpdate`, `OnLateUpdate`, `OnClose`, `OnUnbindUI` |
| 自定义系统 | `ServiceBase` | `Initialize`, `OnUpdate`, `Dispose` |

**记住**：你只需要重写你需要的方法，其他的框架会处理！
