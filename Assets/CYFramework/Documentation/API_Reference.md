# CYFramework 2.2 API 参考文档

> 本文档包含框架所有公开 API 的完整说明。

---

## 目录

- [1. 统一入口 (CY)](#1-统一入口)
- [2. 基础设施层 (Infrastructure)](#2-基础设施层)
- [3. 核心服务层 (Core)](#3-核心服务层)
- [4. UI 模块 (Modules/UI)](#4-ui-模块)
- [5. 玩法核心层 (Gameplay)](#5-玩法核心层)
- [6. 平台适配层 (Platform)](#6-平台适配层)
- [7. 调试工具 (Debug)](#7-调试工具)

---

## 1. 统一入口

### 1.1 CY 静态类

**命名空间**: `CYFramework`

类似 GameFramework 的 GameEntry，提供简洁的 API 入口。

#### CY.Event（事件系统）
| 方法 | 参数 | 说明 |
|------|------|------|
| `Subscribe<T>(handler, owner)` | 处理器, 拥有者 | 订阅事件 |
| `Unsubscribe<T>(handler)` | 处理器 | 取消订阅 |
| `Post<T>(ref T evt)` | 事件数据 | 发布事件（零 GC） |
| `UnsubscribeAll(owner)` | 拥有者 | 取消所有订阅 |
| `SubscribeAll(target)` | 目标对象 | 自动订阅 [OnEvent] 标记的方法 |

#### CY 日志快捷方法
| 方法 | 参数 | 说明 |
|------|------|------|
| `Log(string msg)` | 消息 | Debug 日志 |
| `LogInfo(string msg)` | 消息 | Info 日志 |
| `LogWarning(string msg)` | 消息 | Warning 日志 |
| `LogError(string msg)` | 消息 | Error 日志 |

#### CY.Timer（计时器系统）
| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `Delay(seconds, onComplete, useUnscaledTime)` | 秒数, 回调, 是否不受时间缩放 | `Timer` | 延迟执行 |
| `Loop(interval, onTick, useUnscaledTime)` | 间隔, 回调, 是否不受时间缩放 | `Timer` | 循环执行 |
| `NextFrame(onComplete)` | 回调 | `void` | 下一帧执行 |
| `CancelAll()` | 无 | `void` | 取消所有计时器 |

#### CY.Procedure（流程系统）
| 方法 | 参数 | 说明 |
|------|------|------|
| `AddProcedure<T>(name)` | 名称(可选) | 注册流程 |
| `AutoRegisterAll(assembly)` | 程序集(可选) | 自动注册 [AutoRegisterProcedure] 标记的流程 |
| `Start<T>()` | 无 | 启动流程系统 |
| `Start(name)` | 流程名称 | 按名称启动 |
| `ChangeProcedure<T>()` | 无 | 切换流程 |
| `ChangeProcedure<T>(userData)` | 用户数据 | 切换流程（带参数） |
| `Change(name, userData)` | 流程名称, 用户数据 | 按名称切换流程 |
| `Current` | - | 获取当前流程 |
| `CurrentName` | - | 获取当前流程名称 |

#### CY.Entity（实体系统）
| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `RegisterEntity(type, prefab, preload, parent)` | 类型名, 预制体, 预加载数, 父节点 | `void` | 注册实体类型 |
| `ShowEntity<T>(type, userData)` | 类型名, 用户数据 | `T` | 显示实体 |
| `ShowEntity(type, userData)` | 类型名, 用户数据 | `IEntity` | 显示实体 |
| `HideEntity(entityId)` | 实体 ID | `void` | 隐藏实体 |
| `HideEntity(entity)` | 实体对象 | `void` | 隐藏实体 |
| `HideAllEntities(type)` | 类型名 | `void` | 隐藏指定类型所有实体 |
| `HideAllEntities()` | 无 | `void` | 隐藏所有实体 |
| `GetEntity<T>(entityId)` | 实体 ID | `T` | 获取实体 |
| `GetEntityCount(type)` | 类型名 | `int` | 获取实体数量 |

#### CY.Data（数据表系统）
| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `CreateDataTable<T>(name)` | 表名(可选) | `DataTable<T>` | 创建数据表 |
| `GetDataTable<T>(name)` | 表名(可选) | `DataTable<T>` | 获取数据表 |
| `LoadFromCsv<T>(csvText, name, separator)` | CSV 文本, 表名(可选), 分隔符 | `DataTable<T>` | 从 CSV 加载 |
| `UnloadDataTable(name)` | 表名 | `void` | 卸载数据表 |

#### CY 服务定位器快捷方法
| 方法 | 说明 |
|------|------|
| `Get<T>()` | 获取服务（等同 ServiceLocator.Get） |
| `Register<T>(service)` | 注册服务 |

#### 特性（Attributes）
| 特性 | 用途 | 示例 |
|------|------|------|
| `[AutoRegisterProcedure(name, order)]` | 标记流程自动注册 | `[AutoRegisterProcedure("Menu", 0)]` |
| `[OnEvent(priority)]` | 标记方法自动订阅事件 | `[OnEvent] void OnGameOver(ref GameOverEvent e)` |
| `[EventPriority(priority)]` | 设置事件处理优先级 | `[EventPriority(-100)]` |

**使用示例**:
```csharp
// ========== 事件 ==========
// 手动订阅
CY.Event.Subscribe<GameStartEvent>(OnStart, this);
var startEvt = new GameStartEvent { StageId = 1 };
CY.Event.Post(ref startEvt);

// 自动订阅（推荐）- 在类中使用 [OnEvent] 标记方法
[OnEvent]
private void OnStart(ref GameStartEvent evt) { }

// ========== 流程 ==========
// 手动注册
CY.Procedure.AddProcedure<MenuProcedure>("Menu");
CY.Procedure.Start("Menu");
CY.Procedure.Change("Battle");

// 自动注册（推荐）- 在流程类上使用 [AutoRegisterProcedure]
[AutoRegisterProcedure("Menu", order: 0)]
public class MenuProcedure : ProcedureBase { }

// ========== 计时器 ==========
CY.Timer.Delay(2f, () => CY.LogInfo("延迟 2 秒"));
CY.Timer.Loop(1f, () => CY.LogInfo("每秒执行"));

// ========== 实体 ==========
CY.Entity.RegisterEntity("Enemy", enemyPrefab, 10);
var enemy = CY.Entity.ShowEntity<EnemyEntity>("Enemy");
CY.Entity.HideEntity(enemy);

// ========== 数据表 ==========
CY.Data.LoadFromCsv<MonsterRow>(csvText);
var monster = CY.Data.GetDataTable<MonsterRow>().GetRow(1001);
```

---

## 2. 基础设施层

### 2.1 ServiceLocator（服务定位器）

**命名空间**: `CYFramework.Infrastructure`

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `Register<TService, TImpl>()` | `ServiceScope scope = Singleton` | `void` | 注册服务（接口+实现类） |
| `Register<T>(Func<T> factory)` | `Func<T> factory, ServiceScope scope` | `void` | 工厂方法注册 |
| `RegisterInstance<T>(T instance)` | `T instance` | `void` | 注册已有实例 |
| `RegisterLazy<TService, TImpl>()` | `ServiceScope scope` | `void` | 懒加载注册 |
| `Get<T>()` | 无 | `T` | 获取服务（不存在则抛异常） |
| `TryGet<T>(out T service)` | `out T service` | `bool` | 安全获取服务 |
| `Get(Type type)` | `Type serviceType` | `object` | 按类型获取 |
| `IsRegistered<T>()` | 无 | `bool` | 检查是否已注册 |
| `InitializeAll()` | 无 | `void` | 初始化所有 IInitializable 服务 |
| `ClearScoped()` | 无 | `void` | 清理 Scoped 作用域服务 |
| `DisposeAll()` | 无 | `void` | 销毁所有服务 |
| `ClearAll()` | 无 | `void` | 清空注册表（测试用） |

**ServiceScope 枚举**:
| 值 | 说明 |
|----|------|
| `Singleton` | 全局单例（默认） |
| `Scoped` | 场景级别，切场景时清理 |
| `Transient` | 每次获取创建新实例 |

---

### 1.2 生命周期接口

**命名空间**: `CYFramework.Infrastructure`

#### IInitializable
```csharp
public interface IInitializable
{
    int InitOrder { get; }    // 初始化顺序（数字小的先执行）
    void Initialize();         // 初始化方法
}
```

#### ITickable
```csharp
public interface ITickable
{
    int TickOrder { get; }     // Tick 顺序
    void Tick(float dt);       // 固定帧更新（FixedUpdate 中调用）
}
```

#### IUpdateable
```csharp
public interface IUpdateable
{
    int UpdateOrder { get; }   // Update 顺序
    void OnUpdate(float dt);   // 每帧更新（Update 中调用）
}
```

#### ILateUpdateable
```csharp
public interface ILateUpdateable
{
    int LateUpdateOrder { get; }
    void OnLateUpdate(float dt);
}
```

#### IPausable
```csharp
public interface IPausable
{
    void OnPause();                        // 暂停时调用
    void OnResume(float pauseDuration);    // 恢复时调用，传入暂停时长
}
```

#### IDisposableEx
```csharp
public interface IDisposableEx : IDisposable
{
    int DisposeOrder { get; }  // 销毁顺序（数字大的先销毁）
}
```

---

### 1.3 CYLog（日志系统）

**命名空间**: `CYFramework.Infrastructure`

| 方法 | 参数 | 说明 |
|------|------|------|
| `Initialize(LogLevel level)` | 日志级别 | 初始化日志系统 |
| `SetLevel(LogLevel level)` | 日志级别 | 运行时修改级别 |
| `Verbose(string msg)` | 消息 | 详细日志 |
| `Debug(string msg)` | 消息 | 调试日志 |
| `Info(string msg)` | 消息 | 信息日志 |
| `Warning(string msg)` | 消息 | 警告日志 |
| `Error(string msg, Exception ex = null)` | 消息, 异常 | 错误日志 |
| `Fatal(string msg)` | 消息 | 致命错误 |

**LogLevel 枚举**:
```csharp
public enum LogLevel { Verbose = 0, Debug = 1, Info = 2, Warning = 3, Error = 4, Fatal = 5, Off = 6 }
```

---

### 1.4 CYBootstrap（启动器）

**命名空间**: `CYFramework.Infrastructure`

**Inspector 属性**:
| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `_logLevel` | `LogLevel` | `Debug` | 日志级别 |
| `_fixedTickRate` | `int` | `30` | 逻辑帧率 (Hz) |
| `_maxPauseTolerance` | `float` | `5f` | 切后台最大容忍时间 (秒) |

**静态属性**:
| 属性 | 类型 | 说明 |
|------|------|------|
| `Instance` | `CYBootstrap` | 单例实例 |

**公开方法**:
| 方法 | 说明 |
|------|------|
| `RegisterLifecycle(object obj)` | 注册生命周期对象 |
| `UnregisterLifecycle(object obj)` | 注销生命周期对象 |

---

## 3. 核心服务层

### 3.1 EventBus（事件总线）

**命名空间**: `CYFramework.Core.Event`

| 方法 | 参数 | 说明 |
|------|------|------|
| `Subscribe<T>(EventHandler<T> handler, object target, int priority = 0)` | 处理器, 目标对象, 优先级 | 订阅事件 |
| `Unsubscribe<T>(EventHandler<T> handler)` | 处理器 | 取消订阅 |
| `UnsubscribeAll(object target)` | 目标对象 | 取消对象的所有订阅 |
| `Post<T>(ref T evt)` | 事件数据 | 发布事件（必须用 ref） |
| `PostDelayed<T>(T evt, int frames = 1)` | 事件数据, 延迟帧数 | 延迟发布 |

**EventHandler 委托**:
```csharp
public delegate void EventHandler<T>(ref T evt) where T : struct;
```

---

### 3.2 FSM（有限状态机）

**命名空间**: `CYFramework.Core.FSM`

#### FSM<T> 类
| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `AddState(IState<T> state)` | 状态实例 | `FSM<T>` | 注册状态（链式调用） |
| `AddStates(params IState<T>[])` | 多个状态 | `FSM<T>` | 批量注册状态 |
| `Start(T initialState)` | 初始状态 | `void` | 启动状态机 |
| `ChangeState(T newState)` | 新状态 | `void` | 切换状态 |
| `Update(float deltaTime)` | 时间增量 | `void` | 更新（每帧调用） |
| `Stop()` | 无 | `void` | 停止状态机 |

| 属性 | 类型 | 说明 |
|------|------|------|
| `CurrentStateType` | `T` | 当前状态类型 |
| `IsRunning` | `bool` | 是否运行中 |

#### IState<T> 接口
```csharp
public interface IState<T> where T : Enum
{
    T StateType { get; }
    void OnEnter();
    void OnUpdate(float deltaTime);
    void OnExit();
}
```

#### StateBase<T> 基类
```csharp
public abstract class StateBase<T> : IState<T> where T : Enum
{
    public abstract T StateType { get; }
    protected FSM<T> FSM { get; }
    
    public virtual void OnEnter() { }
    public virtual void OnUpdate(float deltaTime) { }
    public virtual void OnExit() { }
    
    protected void ChangeState(T newState);  // 切换状态
}
```

**使用示例**:
```csharp
public enum EnemyState { Idle, Patrol, Chase, Attack }

public class IdleState : StateBase<EnemyState>
{
    public override EnemyState StateType => EnemyState.Idle;
    
    public override void OnEnter() => Debug.Log("进入待机");
    public override void OnUpdate(float dt) {
        if (DetectPlayer()) ChangeState(EnemyState.Chase);
    }
}

// 使用
var fsm = new FSM<EnemyState>();
fsm.AddState(new IdleState())
   .AddState(new PatrolState())
   .Start(EnemyState.Idle);
```

---

### 3.3 ProcedureManager（流程管理器）

**命名空间**: `CYFramework.Core.Procedure`

类似 GameFramework 的 Procedure 系统，管理游戏流程。

#### ProcedureManager 类
| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `AddProcedure<T>()` | 无 | `ProcedureManager` | 注册流程 |
| `AddProcedure(ProcedureBase)` | 流程实例 | `ProcedureManager` | 注册流程实例 |
| `Start<T>()` | 无 | `void` | 启动流程系统 |
| `ChangeProcedure<T>()` | 无 | `void` | 切换流程 |
| `ChangeProcedure<T>(userData)` | 用户数据 | `void` | 切换流程（带参数） |
| `GetProcedure<T>()` | 无 | `T` | 获取指定流程 |
| `Stop()` | 无 | `void` | 停止流程系统 |

| 属性 | 类型 | 说明 |
|------|------|------|
| `CurrentProcedure` | `ProcedureBase` | 当前流程 |
| `IsRunning` | `bool` | 是否运行中 |
| `UpdateOrder` | `int` | 更新优先级（默认 -50） |

#### ProcedureBase 基类
```csharp
public abstract class ProcedureBase
{
    protected ProcedureManager Owner { get; }
    
    protected internal virtual void OnEnter(ProcedureBase previous) { }
    protected internal virtual void OnUpdate(float deltaTime) { }
    protected internal virtual void OnLeave(ProcedureBase next) { }
    
    protected void ChangeProcedure<T>() where T : ProcedureBase;
    protected void ChangeProcedure<T>(object userData) where T : ProcedureBase;
}
```

#### ProcedureBase<TData> 泛型基类
```csharp
public abstract class ProcedureBase<TData> : ProcedureBase
{
    protected TData UserData { get; }  // 切换时传入的数据
}
```

**使用示例**:
```csharp
public class MenuProcedure : ProcedureBase
{
    protected internal override void OnEnter(ProcedureBase previous)
    {
        CYLog.Info("进入主菜单");
        // 显示菜单 UI
    }
    
    protected internal override void OnUpdate(float dt)
    {
        if (Input.GetKeyDown(KeyCode.Space))
            ChangeProcedure<GameProcedure>();
    }
    
    protected internal override void OnLeave(ProcedureBase next)
    {
        // 关闭菜单 UI
    }
}
```

---

### 3.4 TimerManager（计时器管理器）

**命名空间**: `CYFramework.Core.Timer`

#### TimerManager 类
| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `Delay(seconds, onComplete, useUnscaledTime)` | 秒数, 回调, 是否不受缩放 | `Timer` | 延迟执行 |
| `Loop(interval, onTick, useUnscaledTime)` | 间隔, 回调, 是否不受缩放 | `Timer` | 循环执行 |
| `NextFrame(onComplete)` | 回调 | `void` | 下一帧执行 |
| `Cancel(Timer)` | 计时器 | `void` | 取消计时器 |
| `CancelAll()` | 无 | `void` | 取消所有 |

| 属性 | 类型 | 说明 |
|------|------|------|
| `ActiveCount` | `int` | 活跃计时器数量 |
| `UpdateOrder` | `int` | 更新优先级（默认 -100） |

#### Timer 类
| 方法 | 说明 |
|------|------|
| `OnUpdate(Action<float>)` | 设置进度回调（0-1） |
| `Pause()` | 暂停 |
| `Resume()` | 恢复 |
| `Stop()` | 停止 |
| `Reset()` | 重置 |

| 属性 | 类型 | 说明 |
|------|------|------|
| `Id` | `int` | 计时器 ID |
| `Duration` | `float` | 总时长 |
| `Elapsed` | `float` | 已过时间 |
| `IsLoop` | `bool` | 是否循环 |
| `IsPaused` | `bool` | 是否暂停 |
| `IsCompleted` | `bool` | 是否完成 |
| `UseUnscaledTime` | `bool` | 是否不受时间缩放 |

**使用示例**:
```csharp
// 延迟 2 秒执行
CY.Timer.Delay(2f, () => Debug.Log("2秒后"));

// 带进度回调
CY.Timer.Delay(3f, OnComplete).OnUpdate(progress => {
    loadingBar.value = progress;  // 0 -> 1
});

// 循环执行
var timer = CY.Timer.Loop(1f, () => Debug.Log("每秒执行"));
timer.Stop();  // 停止

// 不受时间缩放（暂停时也继续）
CY.Timer.Delay(5f, OnTimeout, useUnscaledTime: true);
```

---

### 3.5 PoolManager（对象池）

**命名空间**: `CYFramework.Core.Pool`

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `GetOrCreatePool<T>(Func<T> factory, PoolConfig config = null)` | 工厂, 配置 | `ObjectPool<T>` | 获取/创建数据对象池 |
| `GetOrCreatePool(key, prefab, PoolConfig config = null)` | 键名, 预制体, 配置 | `GameObjectPool` | 获取/创建 GameObject 池 |
| `ShrinkAll()` | 无 | `void` | 收缩所有池（响应低内存） |

**IPoolable 接口**:
```csharp
public interface IPoolable
{
    void OnSpawn();    // 从池中取出时调用
    void OnDespawn();  // 归还池时调用
}
```

---

### 3.6 EntityManager（实体管理器）

**命名空间**: `CYFramework.Core.Entity`

管理游戏中的动态实体（敌人、子弹、特效等）。

#### EntityManager 类
| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `Initialize(Transform root)` | 根节点 | `void` | 初始化 |
| `RegisterEntity(type, prefab, preload, parent)` | 类型名, 预制体, 预加载数, 父节点 | `void` | 注册实体类型 |
| `ShowEntity<T>(type, userData)` | 类型名, 用户数据 | `T` | 显示实体 |
| `HideEntity(entityId)` | 实体 ID | `void` | 隐藏实体 |
| `HideEntity(entity)` | 实体对象 | `void` | 隐藏实体 |
| `HideAllEntities(type)` | 类型名 | `void` | 隐藏指定类型 |
| `HideAllEntities()` | 无 | `void` | 隐藏所有 |
| `GetEntity<T>(entityId)` | 实体 ID | `T` | 获取实体 |
| `GetEntities(type)` | 类型名 | `IReadOnlyList<IEntity>` | 获取所有指定类型 |
| `GetEntityCount(type)` | 类型名 | `int` | 获取数量 |
| `HasEntity(entityId)` | 实体 ID | `bool` | 是否存在 |

#### IEntity 接口
```csharp
public interface IEntity
{
    int Id { get; }
    string EntityType { get; }
    bool IsVisible { get; }
    GameObject GameObject { get; }
    
    void OnInit(int id, object userData);
    void OnShow(object userData);
    void OnHide();
    void OnUpdate(float deltaTime);
    void OnRecycle();
}
```

#### EntityBase 基类
```csharp
public abstract class EntityBase : MonoBehaviour, IEntity
{
    public int Id { get; }
    public abstract string EntityType { get; }
    public bool IsVisible { get; }
    protected object UserData { get; }
    
    // 子类重写
    protected virtual void OnEntityInit(object userData) { }
    protected virtual void OnEntityShow(object userData) { }
    protected virtual void OnEntityHide() { }
    protected virtual void OnEntityUpdate(float deltaTime) { }
    protected virtual void OnEntityRecycle() { }
}
```

**使用示例**:
```csharp
public class EnemyEntity : EntityBase
{
    public override string EntityType => "Enemy";
    
    private int _hp;
    
    protected override void OnEntityShow(object userData)
    {
        var data = (EnemyData)userData;
        _hp = data.MaxHp;
        transform.position = data.SpawnPos;
    }
    
    protected override void OnEntityUpdate(float dt)
    {
        // 移动逻辑
    }
    
    protected override void OnEntityHide()
    {
        // 死亡特效
    }
}

// 使用
CY.Entity.RegisterEntity("Enemy", enemyPrefab, 20);
var enemy = CY.Entity.ShowEntity<EnemyEntity>("Enemy", new EnemyData { MaxHp = 100 });
```

---

### 3.7 DataTableManager（数据表管理器）

**命名空间**: `CYFramework.Core.DataTable`

管理游戏配置数据（怪物、技能、关卡配置表）。

#### DataTableManager 类
| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `CreateDataTable<T>(name)` | 表名 | `DataTable<T>` | 创建数据表 |
| `GetDataTable<T>(name)` | 表名 | `DataTable<T>` | 获取数据表 |
| `HasDataTable(name)` | 表名 | `bool` | 是否存在 |
| `LoadFromCsv<T>(csvText, name, separator)` | CSV 文本, 表名, 分隔符 | `DataTable<T>` | 从 CSV 加载 |
| `LoadFromScriptableObject<T, TSO>(so, getter, name)` | SO, 行获取器, 表名 | `DataTable<T>` | 从 SO 加载 |
| `UnloadDataTable(name)` | 表名 | `void` | 卸载数据表 |
| `UnloadAllDataTables()` | 无 | `void` | 卸载所有 |

#### IDataRow 接口
```csharp
public interface IDataRow
{
    int Id { get; }
    void ParseRow(string[] values);
}
```

#### DataTable<T> 类
| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `AddRow(row)` | 数据行 | `void` | 添加行 |
| `GetRow(id)` | 行 ID | `T` | 获取行 |
| `GetRow(predicate)` | 条件 | `T` | 条件查询 |
| `GetAllRows()` | 无 | `IReadOnlyList<T>` | 获取所有行 |
| `GetRows(predicate)` | 条件 | `List<T>` | 条件查询多行 |
| `HasRow(id)` | 行 ID | `bool` | 是否存在 |
| `Count` | - | `int` | 行数 |

**使用示例**:
```csharp
// 定义数据行
public class MonsterRow : IDataRow
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    public int Hp { get; private set; }
    public float Speed { get; private set; }
    public int Damage { get; private set; }
    
    public void ParseRow(string[] values)
    {
        Id = int.Parse(values[0]);
        Name = values[1];
        Hp = int.Parse(values[2]);
        Speed = float.Parse(values[3]);
        Damage = int.Parse(values[4]);
    }
}

// 加载 CSV（格式: Id,Name,Hp,Speed,Damage）
string csv = Resources.Load<TextAsset>("Config/Monster").text;
CY.Data.LoadFromCsv<MonsterRow>(csv);

// 获取数据
var monster = CY.Data.GetDataTable<MonsterRow>().GetRow(1001);
Debug.Log($"{monster.Name}: HP={monster.Hp}");

// 条件查询
var table = CY.Data.GetDataTable<MonsterRow>();
var bosses = table.GetRows(m => m.Hp > 1000);
```

---

### 3.8 ConfigLoader（配置加载器）

**命名空间**: `CYFramework.Core.Config`

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `Load<T>(string path)` | 资源路径 | `T` | 同步加载 |
| `LoadAsync<T>(string path)` | 资源路径 | `Task<T>` | 异步加载 |
| `PreloadAsync(string[] paths)` | 路径数组 | `Task` | 批量预加载 |
| `Unload(string path)` | 资源路径 | `void` | 卸载配置 |

---

### 2.4 ResourceLoader（资源加载器）

**命名空间**: `CYFramework.Core.Resource`

> 注意：当前默认实现为 `Resources` + 缓存（`ResourceLoader`），Addressables/AssetBundle 属于扩展点（代码中有预留/待实现项）。

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `Load<T>(string path)` | 资源路径 | `T` | 同步加载 |
| `LoadAsync<T>(string path, Action<T> callback)` | 路径, 回调 | `void` | 异步加载（回调） |
| `LoadAsync<T>(string path)` | 路径 | `Task<T>` | 异步加载（Task） |
| `Unload(string path)` | 路径 | `void` | 卸载指定资源（仅对缓存条目生效） |
| `UnloadUnused()` | 无 | `void` | 卸载未使用资源（包装 `Resources.UnloadUnusedAssets`） |
| `LoadScene(string sceneName, LoadSceneMode mode, Action onComplete)` | 场景名, 模式, 回调 | `void` | 加载场景（同步触发回调） |
| `LoadSceneAsync(string sceneName, LoadSceneMode mode)` | 场景名, 模式 | `AsyncOperation` | 异步加载场景 |
| `Instantiate(string path, Transform parent = null)` | 路径, 父节点 | `GameObject` | 加载并实例化 |
| `InstantiateAsync(string path, Action<GameObject> callback, Transform parent = null)` | 路径, 回调, 父节点 | `void` | 异步加载并实例化 |
| `Preload<T>(string path)` | 路径 | `void` | 预加载（仅缓存） |
| `PreloadAsync(string[] paths, Action onComplete, Action<float> onProgress)` | 路径数组, 完成回调, 进度回调 | `void` | 批量预加载 |

---

### 2.5 NetworkService（网络服务）

**命名空间**: `CYFramework.Core.Network`

#### HTTP 方法
| 方法 | 参数 | 返回值 |
|------|------|--------|
| `Get(string url)` | URL | `Task<HttpResponse>` |
| `Post(string url, string body, string contentType = "application/json")` | URL, Body, ContentType | `Task<HttpResponse>` |

#### WebSocket 方法
| 方法 | 参数 | 返回值 |
|------|------|--------|
| `ConnectWebSocket(string url)` | WebSocket URL | `Task` |
| `SendWebSocket(string message)` | 消息内容 | `void` |
| `CloseWebSocket()` | 无 | `void` |

#### 事件
| 事件 | 参数 | 说明 |
|------|------|------|
| `OnStateChanged` | `NetworkState state` | 连接状态变化 |
| `OnMessage` | `string message` | 收到文本消息 |
| `OnBinaryMessage` | `byte[] data` | 收到二进制消息 |

---

### 2.6 SaveService（存档服务）

**命名空间**: `CYFramework.Core.Save`

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `Save<T>(string key, T data)` | 键名, 数据 | `bool` | 保存（`T : SaveDataBase`） |
| `Load<T>(string key)` | 键名 | `T` | 加载（不存在返回 `new T()`，`T : SaveDataBase, new()`） |
| `Exists(string key)` | 键名 | `bool` | 检查是否存在 |
| `Delete(string key)` | 键名 | `void` | 删除存档 |
| `SaveAll()` | 无 | `void` | 保存所有缓存（当前为简化实现） |
| `RegisterMigration(IMigration migration)` | 迁移器 | `void` | 注册版本迁移器链 |
| `SetCurrentVersion(int version)` | 版本号 | `void` | 设置当前存档版本 |

---

### 2.7 IAudioService（音频服务）

**命名空间**: `CYFramework.Core.Audio`

| 方法 | 参数 | 说明 |
|------|------|------|
| `PlayBGM(string name, float volume = 1f, bool loop = true)` | 音乐名, 音量, 循环 | 播放背景音乐 |
| `StopBGM(float fadeOut = 0.5f)` | 淡出时间 | 停止背景音乐 |
| `PauseBGM()` | 无 | 暂停 BGM |
| `ResumeBGM()` | 无 | 恢复 BGM |
| `PlaySFX(string name, float volume = 1f)` | 音效名, 音量 | 播放音效 |
| `SetMasterVolume(float volume)` | 音量 (0-1) | 设置主音量 |
| `SetBGMVolume(float volume)` | 音量 (0-1) | 设置 BGM 音量 |
| `SetSFXVolume(float volume)` | 音量 (0-1) | 设置音效音量 |
| `Mute(bool mute)` | 是否静音 | 静音开关 |

---

### 2.8 IHotUpdateService（热更新服务）

**命名空间**: `CYFramework.Core.HotUpdate`

| 方法 | 返回值 | 说明 |
|------|--------|------|
| `CheckForUpdateAsync()` | `Task<UpdateCheckResult>` | 检查更新 |
| `DownloadUpdateAsync(Action<DownloadProgress> onProgress)` | `Task` | 下载更新 |
| `ApplyUpdateAsync()` | `Task` | 应用更新 |

---

## 3. UI 模块

### 3.1 UIManager（UI 管理器）

**命名空间**: `CYFramework.Core.UI`

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `Open<T>(object data = null)` | 传递数据 | `T` | 打开面板 |
| `Close<T>()` | 无 | `void` | 关闭指定面板 |
| `Close(Type panelType)` | 面板类型 | `void` | 按类型关闭 |
| `Close(UIPanel panel)` | 面板实例 | `void` | 关闭面板实例 |
| `Back()` | 无 | `void` | 返回上一个面板 |
| `CloseAll()` | 无 | `void` | 关闭所有面板 |
| `CloseLayer(UILayer layer)` | 层级 | `void` | 关闭指定层级 |
| `Get<T>()` | 无 | `T` | 获取已打开的面板 |
| `IsOpened<T>()` | 无 | `bool` | 检查面板是否已打开 |
| `Preload<T>()` | 无 | `void` | 预加载面板 |
| `ShowToast(string msg, float duration = 2f)` | 消息, 时长 | `void` | 显示 Toast |
| `ShowConfirm(string title, string content, Action onConfirm, Action onCancel)` | 标题, 内容, 回调 | `void` | 确认对话框 |

**UILayer 枚举**:
| 值 | 数值 | 说明 |
|----|------|------|
| `Background` | 0 | 背景层 |
| `Main` | 100 | 主界面层 |
| `Popup` | 200 | 弹窗层 |
| `Tips` | 300 | 提示层 |
| `Guide` | 400 | 引导层 |
| `Loading` | 500 | 加载层 |
| `System` | 600 | 系统层 |

---

### 3.2 UIPanel（面板基类）

**命名空间**: `CYFramework.Core.UI`

**可重写属性**:
| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Layer` | `UILayer` | `Main` | UI 层级 |
| `IsStackable` | `bool` | `true` | 是否支持返回栈 |
| `IsPoolable` | `bool` | `true` | 是否可对象池复用 |
| `ShowMask` | `bool` | `false` | 是否显示遮罩 |
| `CloseOnMaskClick` | `bool` | `true` | 点击遮罩是否关闭 |
| `EnableAnimation` | `bool` | `true` | 是否启用动画 |

**生命周期方法（子类重写）**:
| 方法 | 说明 |
|------|------|
| `OnBindUI()` | 绑定 UI 事件（按钮点击等） |
| `OnUnbindUI()` | 解绑 UI 事件 |
| `OnShow(object data)` | 面板显示时调用【必须实现】 |
| `OnHide()` | 面板隐藏时调用 |

**公开方法**:
| 方法 | 说明 |
|------|------|
| `CloseSelf()` | 关闭自身 |
| `Back()` | 返回上一个面板 |
| `SetInteractable(bool)` | 设置可交互性 |
| `SetAlpha(float)` | 设置透明度 |

**特性**:
```csharp
[UIPrefab("UI/Panels/MyPanel")]  // 指定预制体路径
public class MyPanel : UIPanel { }
```

---

### 3.3 MVVM 支持

#### ViewModel 基类

**命名空间**: `CYFramework.Core.UI.MVVM`

| 方法 | 说明 |
|------|------|
| `SetProperty<T>(string name, T value)` | 设置属性并通知变更 |
| `GetProperty<T>(string name, T default)` | 获取属性值 |
| `Subscribe(string property, PropertyChangedHandler handler)` | 订阅属性变更 |
| `Unsubscribe(string property, PropertyChangedHandler handler)` | 取消订阅 |
| `SubscribeAll(PropertyChangedHandler handler)` | 订阅所有属性 |
| `ClearSubscriptions()` | 清除所有订阅 |
| `Initialize()` | 初始化（可重写） |
| `Dispose()` | 销毁（可重写） |

#### MVVMPanel<TViewModel> 基类

| 属性/方法 | 说明 |
|-----------|------|
| `ViewModel` | ViewModel 实例（自动创建） |
| `OnBindViewModel()` | 绑定 ViewModel（可重写） |
| `OnUnbindViewModel()` | 解绑 ViewModel（可重写） |
| `OnViewModelPropertyChanged(string, object, object)` | 属性变更回调（可重写） |

#### ObservableList<T>

| 方法 | 说明 |
|------|------|
| `Subscribe(CollectionChangedHandler<T>)` | 订阅集合变更 |
| `Unsubscribe(CollectionChangedHandler<T>)` | 取消订阅 |
| `AddRange(IEnumerable<T>)` | 批量添加 |
| `ReplaceAll(IEnumerable<T>)` | 替换所有 |
| `Sort(Comparison<T>)` | 排序 |

---

### 3.4 通用 UI 组件

#### UIToast（Toast 提示）

**命名空间**: `CYFramework.Core.UI.Components`

| 静态方法 | 参数 | 说明 |
|----------|------|------|
| `Show(string content, float duration = 2f)` | 内容, 时长 | 普通提示 |
| `ShowSuccess(string content)` | 内容 | 成功提示（绿色） |
| `ShowError(string content)` | 内容 | 错误提示（红色） |
| `ShowWarning(string content)` | 内容 | 警告提示（黄色） |

#### UIDialog（对话框）

**命名空间**: `CYFramework.Core.UI.Components`

| 静态方法 | 参数 | 说明 |
|----------|------|------|
| `Alert(string content, string title, Action onConfirm)` | 内容, 标题, 回调 | 提示框 |
| `Confirm(string content, Action onConfirm, Action onCancel, string title)` | 内容, 确认回调, 取消回调, 标题 | 确认框 |
| `Input(string content, Action<string> onConfirm, string default, string title)` | 内容, 输入回调, 默认值, 标题 | 输入框 |

#### UILoading（加载界面）

**命名空间**: `CYFramework.Core.UI.Components`

| 静态方法 | 参数 | 返回值 | 说明 |
|----------|------|--------|------|
| `Show(string tips, bool showProgress)` | 提示文字, 是否显示进度 | `UILoading` | 显示 Loading |
| `Hide(Action onComplete)` | 完成回调 | `void` | 隐藏 Loading |
| `Progress(float value)` | 进度 (0-1) | `void` | 设置进度 |
| `Tips(string text)` | 提示文字 | `void` | 更新提示 |
| `WithLoading(IEnumerator operation, string tips)` | 协程, 提示 | `IEnumerator` | 配合协程使用 |

#### UIListView（列表视图）

| 方法 | 参数 | 说明 |
|------|------|------|
| `SetData<T>(IList<T> data)` | 数据列表 | 设置数据源 |
| `BindObservableList<T>(ObservableList<T>)` | 可观察列表 | 绑定可观察列表 |
| `Refresh()` | 无 | 刷新列表 |
| `UpdateItem(int index, object data)` | 索引, 数据 | 更新指定项 |
| `InsertItem(int index, object data)` | 索引, 数据 | 插入项 |
| `RemoveItem(int index)` | 索引 | 移除项 |
| `Clear()` | 无 | 清空列表 |
| `GetItem(int index)` | 索引 | 获取项 |

**事件**:
| 事件 | 参数 | 说明 |
|------|------|------|
| `OnItemClicked` | `int index, object data` | 项点击事件 |

---

## 4. 玩法核心层

### 4.1 IGameplayWorld（玩法世界接口）

**命名空间**: `CYFramework.Gameplay.Abstraction`

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `FixedTick(float fixedDt)` | 固定时间步长 | `void` | 逻辑帧更新 |
| `HandleInput(in InputCommand cmd)` | 输入指令 | `void` | 处理输入 |
| `GetRenderSnapshot()` | 无 | `ref readonly RenderSnapshot` | 获取当前帧快照 |
| `GetPrevSnapshot()` | 无 | `ref readonly RenderSnapshot` | 获取上一帧快照 |
| `ResetDeltaTime()` | 无 | `void` | 重置时间（切后台用） |
| `Initialize()` | 无 | `void` | 初始化 |
| `Dispose()` | 无 | `void` | 销毁 |

### 4.2 InputCommand（输入指令）

```csharp
public struct InputCommand
{
    public InputType Type;      // 输入类型
    public Vector2 Direction;   // 方向
    public int SkillId;         // 技能 ID
    public float Timestamp;     // 时间戳
    public int CustomId;        // 自定义 ID
}

public enum InputType { None, Move, Jump, Attack, Skill, Interact, Custom }
```

### 4.3 RenderSnapshot（渲染快照）

```csharp
public struct RenderSnapshot
{
    public int Count;              // 有效数量
    public int[] IDs;              // 单位 ID
    public Vector3[] Positions;    // 位置
    public Quaternion[] Rotations; // 旋转
    public float[] HPs;            // 生命值
    public int[] StateIDs;         // 状态 ID
    public float Timestamp;        // 时间戳
    
    public static RenderSnapshot Create(int maxUnits);  // 创建快照
    public void Clear();                                 // 清空
    public void CopyFrom(in RenderSnapshot other);       // 复制
}
```

---

## 5. 平台适配层

### 5.1 平台宏定义

| 宏 | 说明 |
|----|------|
| `CY_WECHAT` | 微信小游戏平台 |
| `UNITY_WEBGL` | Unity WebGL 平台（Unity 内置宏） |
| `CY_PC` | PC 平台 |
| `CY_MOBILE` | 移动端 |
| `ENABLE_DOTS` | 启用 Hybrid DOTS |

> 提示：WebGL/微信均为单线程运行环境，框架应以平台宏（`CY_WECHAT` / `UNITY_WEBGL`）做能力分支；`CY_SINGLE_THREAD` 不是框架内置宏，如需可在项目侧自定义。

### 5.2 平台接口

| 接口 | PC/Mobile 实现 | 微信/WebGL 实现 |
|------|----------------|-----------------|
| `IFileSystem` | `UnityFileSystem` | 不支持 |
| `IStorageAdapter` | `UnityStorageAdapter` | `WeChatStorageAdapter` |
| `IAudioService` | `UnityAudioService` | `WeChatAudioService` |
| `IGameplayWorld` | `HybridGameplayWorld` | `OOPGameplayWorld` |

---

## 6. 调试工具

### 6.1 RuntimeProfiler

**快捷键**: `F1`

显示内容：FPS、帧时间、内存占用、DrawCall、对象池状态、网络延迟

### 6.2 CheatConsole

**快捷键**: `` ` `` (波浪键)

**内置命令**:
| 命令 | 说明 |
|------|------|
| `help` | 显示帮助 |
| `clear` | 清空控制台 |
| `fps` | 切换 FPS 显示 |
| `timescale <value>` | 时间缩放 |
| `gc` | 强制 GC |
| `log <level>` | 设置日志级别 |

**注册自定义命令**:
```csharp
console.RegisterCommand("god", "无敌模式", args => {
    player.isInvincible = true;
    return "已开启无敌";
});
```

---

## 附录：性能红线

| 指标 | 微信/WebGL | Mobile | PC |
|------|-----------|--------|----|
| 帧率 | 45-60 FPS | 60-90 FPS | 60-144 FPS |
| DrawCall | < 100 | < 300 | < 1000 |
| 内存 | < 200MB | < 400MB | < 800MB |
| 每帧 GC | 0 | 0 | < 1KB |
