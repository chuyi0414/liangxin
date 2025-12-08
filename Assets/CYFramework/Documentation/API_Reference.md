# CYFramework 2.2 API 参考文档

> 本文档包含框架所有公开 API 的完整说明。

---

## 目录

- [1. 基础设施层 (Infrastructure)](#1-基础设施层)
- [2. 核心服务层 (Core)](#2-核心服务层)
- [3. UI 模块 (Modules/UI)](#3-ui-模块)
- [4. 玩法核心层 (Gameplay)](#4-玩法核心层)
- [5. 平台适配层 (Platform)](#5-平台适配层)
- [6. 调试工具 (Debug)](#6-调试工具)

---

## 1. 基础设施层

### 1.1 ServiceLocator（服务定位器）

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

## 2. 核心服务层

### 2.1 EventBus（事件总线）

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

### 2.2 PoolManager（对象池）

**命名空间**: `CYFramework.Core.Pool`

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `CreatePool<T>(Func<T> factory, int prewarm, int max)` | 工厂, 预热数, 最大数 | `void` | 创建数据池 |
| `CreateGameObjectPool(string key, GameObject prefab, int prewarm)` | 键名, 预制体, 预热数 | `void` | 创建 GameObject 池 |
| `Spawn<T>()` | 无 | `T` | 从数据池获取 |
| `SpawnGameObject(string key, Vector3 pos, Quaternion rot)` | 键名, 位置, 旋转 | `GameObject` | 从 GO 池获取 |
| `Despawn<T>(T obj)` | 对象 | `void` | 归还数据池 |
| `DespawnGameObject(string key, GameObject go)` | 键名, 对象 | `void` | 归还 GO 池 |
| `Clear<T>()` | 无 | `void` | 清空指定数据池 |
| `ClearAll()` | 无 | `void` | 清空所有池 |

**IPoolable 接口**:
```csharp
public interface IPoolable
{
    void OnSpawn();    // 从池中取出时调用
    void OnDespawn();  // 归还池时调用
}
```

---

### 2.3 ConfigLoader（配置加载器）

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

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `Load<T>(string path)` | 资源路径 | `T` | 同步加载 |
| `LoadAsync<T>(string path, Action<float> onProgress = null)` | 路径, 进度回调 | `Task<T>` | 异步加载 |
| `LoadSceneAsync(string name, LoadSceneMode mode, Action<float> onProgress = null)` | 场景名, 模式, 进度 | `Task` | 加载场景 |
| `Release(string path)` | 资源路径 | `void` | 释放资源 |
| `UnloadUnusedAssets()` | 无 | `void` | 卸载未使用资源 |

---

### 2.5 NetworkService（网络服务）

**命名空间**: `CYFramework.Core.Network`

#### HTTP 方法
| 方法 | 参数 | 返回值 |
|------|------|--------|
| `GetAsync<T>(string url, Dictionary<string,string> headers = null)` | URL, 请求头 | `Task<T>` |
| `PostAsync<T>(string url, object body, Dictionary<string,string> headers = null)` | URL, 请求体, 请求头 | `Task<T>` |
| `PutAsync<T>(string url, object body, Dictionary<string,string> headers = null)` | URL, 请求体, 请求头 | `Task<T>` |
| `DeleteAsync<T>(string url, Dictionary<string,string> headers = null)` | URL, 请求头 | `Task<T>` |

#### WebSocket 方法
| 方法 | 参数 | 返回值 |
|------|------|--------|
| `ConnectWebSocket(string url)` | WebSocket URL | `Task` |
| `DisconnectWebSocket()` | 无 | `void` |
| `SendWebSocketMessage(string message)` | 消息内容 | `void` |

#### 事件
| 事件 | 参数 | 说明 |
|------|------|------|
| `OnWebSocketMessage` | `string message` | 收到消息 |
| `OnWebSocketConnected` | 无 | 连接成功 |
| `OnWebSocketDisconnected` | 无 | 断开连接 |
| `OnNetworkStatusChanged` | `NetworkStatus status` | 网络状态变化 |

---

### 2.6 SaveService（存档服务）

**命名空间**: `CYFramework.Core.Save`

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `Save<T>(string key, T data)` | 键名, 数据 | `void` | 同步保存 |
| `SaveAsync<T>(string key, T data)` | 键名, 数据 | `Task` | 异步保存 |
| `Load<T>(string key, T defaultValue = default)` | 键名, 默认值 | `T` | 同步加载 |
| `LoadAsync<T>(string key)` | 键名 | `Task<T>` | 异步加载 |
| `Exists(string key)` | 键名 | `bool` | 检查是否存在 |
| `Delete(string key)` | 键名 | `void` | 删除存档 |
| `DeleteAll()` | 无 | `void` | 删除所有存档 |
| `RegisterMigration<T>(int from, int to, Func<T,T> migrator)` | 版本范围, 迁移函数 | `void` | 注册版本迁移 |

---

### 2.7 IAudioService（音频服务）

**命名空间**: `CYFramework.Modules.Audio`

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

**命名空间**: `CYFramework.Modules.UI`

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

**命名空间**: `CYFramework.Modules.UI`

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

**命名空间**: `CYFramework.Modules.UI.MVVM`

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

**命名空间**: `CYFramework.Modules.UI.Components`

| 静态方法 | 参数 | 说明 |
|----------|------|------|
| `Show(string content, float duration = 2f)` | 内容, 时长 | 普通提示 |
| `ShowSuccess(string content)` | 内容 | 成功提示（绿色） |
| `ShowError(string content)` | 内容 | 错误提示（红色） |
| `ShowWarning(string content)` | 内容 | 警告提示（黄色） |

#### UIDialog（对话框）

**命名空间**: `CYFramework.Modules.UI.Components`

| 静态方法 | 参数 | 说明 |
|----------|------|------|
| `Alert(string content, string title, Action onConfirm)` | 内容, 标题, 回调 | 提示框 |
| `Confirm(string content, Action onConfirm, Action onCancel, string title)` | 内容, 确认回调, 取消回调, 标题 | 确认框 |
| `Input(string content, Action<string> onConfirm, string default, string title)` | 内容, 输入回调, 默认值, 标题 | 输入框 |

#### UILoading（加载界面）

**命名空间**: `CYFramework.Modules.UI.Components`

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
| `CY_PC` | PC 平台 |
| `CY_MOBILE` | 移动端 |
| `CY_SINGLE_THREAD` | 单线程模式 |
| `ENABLE_DOTS` | 启用 Hybrid DOTS |

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
