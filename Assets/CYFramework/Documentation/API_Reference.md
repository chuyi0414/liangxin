# CYFramework API 参考文档

## 目录

1. [框架入口](#1-框架入口)
2. [核心模块](#2-核心模块)
   - [LogModule 日志模块](#21-logmodule-日志模块)
   - [EventModule 事件模块](#22-eventmodule-事件模块)
   - [TimerModule 定时器模块](#23-timermodule-定时器模块)
   - [ObjectPoolModule 对象池模块](#24-objectpoolmodule-对象池模块)
   - [ResourceModule 资源模块](#25-resourcemodule-资源模块)
   - [StorageModule 存储模块](#26-storagemodule-存储模块)
   - [SoundModule 声音模块](#27-soundmodule-声音模块)
   - [UIModule UI模块](#28-uimodule-ui模块)
   - [ProcedureModule 流程模块](#29-proceduremodule-流程模块)
   - [SchedulerModule 分帧调度模块](#210-schedulermodule-分帧调度模块)
3. [玩法世界](#3-玩法世界)
   - [IGameplayWorld 接口](#31-igameplayworld-接口)
   - [GameplayWorldOOP 实现](#32-gameplayworldoop-实现)
   - [GameplayWorldDots 实现](#33-gameplayworlddots-实现)
4. [数据结构](#4-数据结构)
5. [平台服务](#5-平台服务)

---

## 1. 框架入口

### CYFW 快捷访问（推荐）

全局静态类，简化模块访问。

```csharp
// 直接使用，无需获取实例
CYFW.Timer.Delay(1f, () => Debug.Log("1秒后"));
CYFW.Sound.PlaySFX("Audio/Click");
CYFW.UI.OpenPanel<SettingsPanel>();
CYFW.Storage.SetInt("Score", 100);
CYFW.Event.Publish(new GameStartEvent());

// 检查框架是否就绪
if (CYFW.IsReady)
{
    // 框架已初始化
}
```

**完整列表**：

| 快捷方式 | 模块 |
|----------|------|
| `CYFW.Log` | 日志模块 |
| `CYFW.Event` | 事件模块 |
| `CYFW.Timer` | 定时器模块 |
| `CYFW.Pool` | 对象池模块 |
| `CYFW.Resource` | 资源模块 |
| `CYFW.Storage` | 存储模块 |
| `CYFW.Sound` | 声音模块 |
| `CYFW.UI` | UI模块 |
| `CYFW.Procedure` | 流程模块 |
| `CYFW.Scheduler` | 分帧调度模块 |
| `CYFW.World` | 玩法世界 |
| `CYFW.IsReady` | 框架是否就绪 |

---

### CYFrameworkEntry

框架的主入口，挂载在 GameObject 上驱动整个框架。

**Inspector 配置**：

| 分类 | 配置项 | 说明 |
|------|--------|------|
| 日志 | Log Level | 日志级别（Debug/Info/Warning/Error/Off） |
| 日志 | Log Show Timestamp | 是否显示时间戳 |
| 日志 | Log Show Module | 是否显示模块名 |
| 对象池 | Pool Default Capacity | 每种类型默认最大容量 |
| 对象池 | Pool Auto Expand | 是否自动扩容 |
| 资源 | Resource Loader Type | 加载方式（Resources/AssetBundle/Addressables） |
| 资源 | Resources Path Prefix | Resources 路径前缀 |
| 资源 | Asset Bundle Path | AssetBundle 存放路径 |
| 存储 | Storage Prefix | 存储键名前缀 |
| 存储 | Storage Auto Save | 是否自动保存 |
| 声音 | BGM/SFX Volume | 音量（0-1） |
| 声音 | Mute BGM/SFX | 是否静音 |
| UI | UI Panel Path Prefix | 面板预制体路径前缀 |
| UI | UI Click Sound Path | UI 点击音效路径 |
| 调度器 | Max Time Per Frame | 每帧最大执行时间(ms) |
| 玩法世界 | Use Dots Implementation | 是否使用 DOTS 实现 |
| 玩法世界 | Debug Mode / Time Scale 等 | 玩法世界参数 |

```csharp
// 获取框架实例（也可以用 CYFW.Instance）
var fw = CYFrameworkEntry.Instance;

// 检查是否初始化
if (fw.IsInitialized)
{
    // 框架已就绪
}

// 获取模块
var log = fw.Log;           // 日志模块
var evt = fw.Event;         // 事件模块
var timer = fw.Timer;       // 定时器模块
var pool = fw.Pool;         // 对象池模块
var res = fw.Resource;      // 资源模块
var storage = fw.Storage;   // 存储模块
var sound = fw.Sound;       // 声音模块
var ui = fw.UI;             // UI模块
var proc = fw.Procedure;    // 流程模块
var sched = fw.Scheduler;   // 分帧调度模块

// 获取玩法世界
var world = fw.GameplayWorld;
```

---

## 2. 核心模块

### 2.1 LogModule 日志模块

提供分级日志输出，支持条件编译。

```csharp
var log = CYFrameworkEntry.Instance.Log;

// 调试日志（仅 Editor 和 Development Build）
log.D("Game", "调试信息");

// 信息日志
log.I("Game", "普通信息");

// 警告日志
log.W("Game", "警告信息");

// 错误日志
log.E("Game", "错误信息");
log.E("Game", "异常信息", exception);

// 设置日志等级（低于此等级的日志不输出）
log.CurrentLogLevel = LogLevel.Warning;

// 开关日志
log.IsEnabled = false;

// 显示/隐藏时间戳
log.ShowTimestamp = true;
```

**静态工具类**（无需获取模块实例）：

```csharp
Log.D("Tag", "调试");
Log.I("Tag", "信息");
Log.W("Tag", "警告");
Log.E("Tag", "错误");
```

---

### 2.2 EventModule 事件模块

强类型事件系统，无装箱开销。

```csharp
var evt = CYFrameworkEntry.Instance.Event;

// 定义事件（必须是 struct）
public struct PlayerDiedEvent
{
    public int PlayerId;
    public Vector3 Position;
}

// 订阅事件
void OnPlayerDied(PlayerDiedEvent e)
{
    Debug.Log($"玩家 {e.PlayerId} 死亡");
}
evt.Subscribe<PlayerDiedEvent>(OnPlayerDied);

// 发布事件
evt.Publish(new PlayerDiedEvent 
{ 
    PlayerId = 1, 
    Position = Vector3.zero 
});

// 取消订阅
evt.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);

// 检查是否有监听者
bool hasListeners = evt.HasListeners<PlayerDiedEvent>();

// 清除指定类型的所有监听
evt.Clear<PlayerDiedEvent>();

// 清除所有监听
evt.Clear();
```

**内置事件**：

```csharp
// 游戏开始事件
public struct GameStartEvent { public int LevelId; public float StartTime; }

// 游戏结束事件
public struct GameEndEvent { public bool IsWin; public int Score; }

// 伤害事件
public struct DamageEvent { public int AttackerId; public int TargetId; public int Damage; public bool IsCritical; }
```

---

### 2.3 TimerModule 定时器模块

支持延时调用和循环调用。

```csharp
var timer = CYFrameworkEntry.Instance.Timer;

// 延时调用（3秒后执行一次）
int id1 = timer.Delay(3f, () => Debug.Log("3秒到了"));

// 循环调用（每1秒执行一次）
int id2 = timer.Loop(1f, () => Debug.Log("每秒执行"));

// 使用真实时间（不受 Time.timeScale 影响）
int id3 = timer.Delay(5f, () => Debug.Log("5秒真实时间"), useUnscaledTime: true);

// 取消定时器
timer.Cancel(id1);

// 暂停/恢复定时器
timer.Pause(id2);
timer.Resume(id2);

// 取消所有定时器
timer.CancelAll();

// 获取活跃定时器数量
int count = timer.ActiveTimerCount;
```

---

### 2.4 ObjectPoolModule 对象池模块

泛型对象池，减少 GC。

```csharp
var pool = CYFrameworkEntry.Instance.Pool;

// 定义可池化的类（需要无参构造函数）
public class Bullet
{
    public Vector3 Position;
    public float Speed;
    
    public void Reset()
    {
        Position = Vector3.zero;
        Speed = 0f;
    }
}

// 从池中获取对象
Bullet bullet = pool.Get<Bullet>();

// 使用对象
bullet.Position = new Vector3(0, 1, 0);
bullet.Speed = 10f;

// 归还到池中
bullet.Reset();  // 建议归还前重置状态
pool.Return(bullet);

// 预热对象池（提前创建对象）
pool.Prewarm<Bullet>(50);

// 清空指定类型的池
pool.Clear<Bullet>();

// 清空所有池
pool.ClearAll();

// 获取池统计信息
string stats = pool.GetStats();

// 获取缓存的池类型数量
int cachedCount = pool.CachedCount;
```

**高级用法**（带回调的对象池）：

```csharp
var bulletPool = pool.GetPool<Bullet>(
    maxCapacity: 100,
    onGet: b => b.gameObject.SetActive(true),
    onReturn: b => b.gameObject.SetActive(false)
);
```

---

### 2.5 ResourceModule 资源模块

统一的资源加载接口，支持切换加载方式。

```csharp
var res = CYFW.Resource;

// 同步加载
Texture2D tex = res.Load<Texture2D>("Textures/Icon");
AudioClip clip = res.Load<AudioClip>("Audio/BGM/Main");

// 同步加载并实例化
GameObject obj = res.LoadAndInstantiate("Prefabs/Enemy", parent: transform);

// 异步加载
res.LoadAsync<GameObject>("Prefabs/Boss", prefab =>
{
    if (prefab != null)
    {
        Instantiate(prefab);
    }
});

// 异步加载并实例化
res.LoadAndInstantiateAsync("Prefabs/Effect", obj =>
{
    obj.transform.position = Vector3.zero;
}, parent: transform);

// 检查是否已缓存
bool cached = res.IsCached("Textures/Icon");

// 从缓存移除
res.Unload("Textures/Icon");

// 清空所有缓存
res.UnloadAll();

// 获取缓存数量
int count = res.CachedCount;
```

---

### 2.6 StorageModule 存储模块

本地数据持久化。

```csharp
var storage = CYFrameworkEntry.Instance.Storage;

// 基础类型存取
storage.SetInt("HighScore", 1000);
int score = storage.GetInt("HighScore", defaultValue: 0);

storage.SetFloat("Volume", 0.8f);
float vol = storage.GetFloat("Volume", defaultValue: 1f);

storage.SetString("PlayerName", "张三");
string name = storage.GetString("PlayerName", defaultValue: "");

// 检查键是否存在
bool exists = storage.HasKey("HighScore");

// 删除键
storage.DeleteKey("HighScore");

// 删除所有数据
storage.DeleteAll();

// 立即保存到磁盘
storage.Save();

// 对象存取（JSON 序列化）
[Serializable]
public class PlayerData
{
    public int Level;
    public int Gold;
    public List<int> Items;
}

var data = new PlayerData { Level = 10, Gold = 500, Items = new List<int> { 1, 2, 3 } };
storage.SetObject("PlayerData", data);

PlayerData loaded = storage.GetObject<PlayerData>("PlayerData");
```

---

### 2.7 SoundModule 声音模块

BGM 和音效管理。

```csharp
var sound = CYFrameworkEntry.Instance.Sound;

// 播放 BGM（支持淡入淡出）
sound.PlayBGM("Audio/BGM/Battle", fadeIn: 1f);

// 停止 BGM
sound.StopBGM(fadeOut: 0.5f);

// 暂停/恢复 BGM
sound.PauseBGM();
sound.ResumeBGM();

// 播放音效
sound.PlaySFX("Audio/SFX/Click");
sound.PlaySFX("Audio/SFX/Explosion", volume: 0.8f);

// 在指定位置播放 3D 音效
sound.PlaySFXAtPosition("Audio/SFX/Footstep", position);

// 设置音量
sound.SetBGMVolume(0.5f);
sound.SetSFXVolume(0.8f);

// 静音控制
sound.MuteBGM = true;
sound.MuteSFX = false;

// 获取当前音量
float bgmVol = sound.BGMVolume;
float sfxVol = sound.SFXVolume;
```

---

### 2.8 UIModule UI模块

管理 UI 面板的生命周期。

```csharp
var ui = CYFrameworkEntry.Instance.UI;

// 打开面板
ui.OpenPanel<SettingsPanel>();

// 带参数打开面板
ui.OpenPanel<ShopPanel>(new ShopData { Category = "weapons" });

// 关闭面板
ui.ClosePanel<SettingsPanel>();

// 隐藏面板（不销毁，下次打开更快）
ui.HidePanel<SettingsPanel>();

// 获取面板实例
var panel = ui.GetPanel<SettingsPanel>();
if (panel != null)
{
    panel.RefreshData();
}

// 检查面板是否打开
bool isOpen = ui.IsPanelOpen<SettingsPanel>();

// 返回上一个面板
ui.GoBack();

// 关闭所有面板
ui.CloseAllPanels();

// 获取当前顶部面板
UIPanel top = ui.GetTopPanel();

// 设置面板预制体路径前缀（默认 "UI/Panels/"）
ui.SetPanelPathPrefix("UI/Windows/");
```

**自定义面板**：

```csharp
using CYFramework.Runtime.Core.UI;

public class SettingsPanel : UIPanel
{
    // 面板层级（可选，默认 Main）
    public override UILayer Layer => UILayer.Popup;
    
    // 是否缓存（可选，默认 true）
    public override bool IsCached => true;

    public Slider volumeSlider;

    // 面板加载时（只调用一次）
    public override void OnLoad()
    {
        base.OnLoad();
        // 初始化组件引用
    }

    // 面板打开时
    public override void OnOpen(object param = null)
    {
        base.OnOpen(param);
        // 读取数据
        volumeSlider.value = CYFrameworkEntry.Instance.Storage.GetFloat("Volume", 1f);
    }

    // 面板显示时（每次显示都调用）
    public override void OnShow()
    {
        base.OnShow();
    }

    // 面板隐藏时
    public override void OnHide()
    {
        base.OnHide();
    }

    // 面板关闭时
    public override void OnClose()
    {
        base.OnClose();
        // 保存数据
        CYFrameworkEntry.Instance.Storage.SetFloat("Volume", volumeSlider.value);
    }

    // 面板销毁时
    public override void OnUnload()
    {
        base.OnUnload();
    }

    // 关闭按钮
    public void OnCloseClick()
    {
        PlayClickSound();  // 播放点击音效
        CloseSelf();       // 关闭自己
    }
}
```

**UI 层级**：

| 层级 | 值 | 说明 |
|------|-----|------|
| Background | 0 | 背景层 |
| Main | 100 | 主界面 |
| Popup | 200 | 弹窗 |
| Toast | 300 | 提示 |
| Loading | 400 | 加载 |
| System | 500 | 系统（最顶层） |

---

### 2.9 ProcedureModule 流程模块

基于状态机的游戏流程管理。

```csharp
var proc = CYFrameworkEntry.Instance.Procedure;

// 定义流程
public class ProcedureLoading : ProcedureBase
{
    private float _progress;

    public override void OnEnter(ProcedureModule owner)
    {
        base.OnEnter(owner);
        _progress = 0f;
        // 开始加载资源...
    }

    public override void OnUpdate(ProcedureModule owner, float deltaTime)
    {
        _progress += deltaTime * 0.2f;
        if (_progress >= 1f)
        {
            ChangeProcedure<ProcedureMainMenu>();
        }
    }

    public override void OnExit(ProcedureModule owner)
    {
        base.OnExit(owner);
        // 清理加载界面...
    }
}

// 注册流程
proc.RegisterProcedure(new ProcedureLoading());
proc.RegisterProcedure(new ProcedureMainMenu());
proc.RegisterProcedure(new ProcedureGame());

// 启动初始流程
proc.StartProcedure<ProcedureLoading>();

// 手动切换流程
proc.ChangeProcedure<ProcedureGame>();

// 获取当前流程
ProcedureBase current = proc.CurrentProcedure;
Type currentType = proc.CurrentProcedureType;

// 检查是否在指定流程
bool isInGame = proc.IsInProcedure<ProcedureGame>();

// 流程间传递数据
proc.SetData("LevelId", 5);
int levelId = proc.GetData<int>("LevelId");
proc.RemoveData("LevelId");
proc.ClearData();
```

---

### 2.9 SchedulerModule 分帧调度模块

将大任务拆分到多帧执行。

```csharp
var sched = CYFrameworkEntry.Instance.Scheduler;

// 设置每帧最大执行时间（毫秒）
sched.SetMaxFrameTime(2f);

// 调度分帧任务
var task = sched.Schedule(() => LoadChunks(), priority: 0, onComplete: () =>
{
    Debug.Log("加载完成");
});

// 分帧迭代器示例
IEnumerator<float> LoadChunks()
{
    for (int i = 0; i < 100; i++)
    {
        // 加载一个区块...
        LoadChunk(i);
        
        // 返回进度（0~1）
        yield return (float)i / 100f;
    }
}

// 批量处理（自动分帧）
List<Enemy> enemies = GetAllEnemies();
sched.ScheduleBatch(enemies, enemy =>
{
    enemy.UpdateAI();
}, itemsPerFrame: 10, onComplete: () =>
{
    Debug.Log("所有敌人 AI 更新完成");
});

// 检查任务状态
if (task.State == ScheduledTaskState.Running)
{
    Debug.Log($"进度: {task.Progress:P0}");
}

// 取消任务
sched.Cancel(task.Id);

// 取消所有任务
sched.CancelAll();

// 待执行任务数量
int pending = sched.PendingCount;
```

---

## 3. 玩法世界

### 3.1 IGameplayWorld 接口

玩法世界的统一接口。

```csharp
public interface IGameplayWorld
{
    bool IsInitialized { get; }
    bool IsRunning { get; }
    bool IsBattleEnded { get; }
    
    void Initialize(GameplayConfig config);
    void Tick(float deltaTime);
    void Shutdown();
    void Pause();
    void Resume();
    void Reset();
    
    void HandleInput(PlayerInput input);
    void ExecuteCommand(GameplayCommand command);
    BattleResult GetBattleResult();
}
```

### 3.2 GameplayWorldOOP 实现

OOP 版本的玩法世界（全平台可用）。

```csharp
var fw = CYFrameworkEntry.Instance;
var world = fw.GameplayWorld as GameplayWorldOOP;

// 生成实体
int playerId = world.SpawnEntity(new EntitySpawnInfo
{
    Type = EntityType.Player,
    ConfigId = 1,
    CampId = 1,
    Position = Vector3.zero,
    Rotation = 0f
});

int enemyId = world.SpawnEntity(new EntitySpawnInfo
{
    Type = EntityType.Enemy,
    ConfigId = 100,
    CampId = 2,
    Position = new Vector3(5, 0, 5),
    Rotation = 180f,
    MaxLifeTime = 30f  // 30秒后自动销毁
});

// 获取实体数据
if (world.TryGetEntity(playerId, out EntityData data))
{
    Debug.Log($"玩家位置: {data.Position}, 血量: {data.Hp}/{data.MaxHp}");
}

// 获取实体快照（用于 UI）
EntitySnapshot snapshot = world.GetEntitySnapshot(playerId);

// 移动实体
world.MoveEntityTo(playerId, new Vector3(10, 0, 10));

// 造成伤害
world.DamageEntity(enemyId, 50);

// 治疗实体
world.HealEntity(playerId, 20);

// 销毁实体
world.DestroyEntity(enemyId);

// 处理输入
world.HandleInput(new PlayerInput
{
    Type = InputType.Move,
    PlayerId = playerId,
    TargetPosition = new Vector3(5, 0, 5)
});

world.HandleInput(new PlayerInput
{
    Type = InputType.Attack,
    PlayerId = playerId,
    TargetEntityId = enemyId
});

// 执行命令
world.ExecuteCommand(new GameplayCommand
{
    Type = CommandType.DamageEntity,
    EntityId = enemyId,
    IntParam = 100
});

// 结束战斗
world.EndBattle(BattleResultType.Victory, score: 1000);

// 获取战斗结果
BattleResult result = world.GetBattleResult();
Debug.Log($"结果: {result.ResultType}, 用时: {result.Duration}s, 击杀: {result.KillCount}");

// 获取子系统
var aiSystem = world.GetSystem<AISystem>();
aiSystem.AddAI(enemyId);

var combatSystem = world.GetSystem<CombatSystem>();
combatSystem.RequestAttack(playerId, enemyId, skillId: 1);

var buffSystem = world.GetSystem<BuffSystem>();
buffSystem.AddBuff(playerId, new BuffAddInfo
{
    ConfigId = 1,
    EffectType = BuffEffectType.AttackUp,
    EffectValue = 10,
    Duration = 10f
});

// 控制玩法世界
world.Pause();
world.Resume();
world.Reset();

// 时间控制
world.SetTimeScale(2f);  // 2倍速
float gameTime = world.GetGameTime();

// 获取所有实体
var entities = world.GetAllEntities();
foreach (var entity in entities)
{
    if (entity.State == EntityState.Active)
    {
        // 处理活跃实体...
    }
}
```

### 3.3 GameplayWorldDots 实现

DOTS/ECS 版本的玩法世界（仅 PC/iOS/Android）。

```csharp
// 启用 DOTS 实现
var config = new GameplayConfig
{
    UseDotsImplementation = true,
    TimeScale = 1f,
    MaxEntityCount = 10000
};

// 工厂会自动选择实现
var world = GameplayWorldFactory.Create(config);

// API 与 OOP 版本相同
world.HandleInput(input);
world.ExecuteCommand(command);
var result = world.GetBattleResult();
```

---

## 4. 数据结构

### EntityData 实体数据

```csharp
public struct EntityData
{
    public int Id;              // 唯一 ID
    public EntityType Type;     // 类型（Player/Enemy/Npc/Projectile/Item/Effect）
    public EntityState State;   // 状态（Invalid/Active/Dead/PendingDestroy）
    public int ConfigId;        // 配置表 ID
    public int CampId;          // 阵营 ID
    public Vector3 Position;    // 位置
    public float Rotation;      // 朝向（Y 轴欧拉角）
    public float MoveSpeed;     // 移动速度
    public Vector3 TargetPosition;  // 目标位置
    public bool IsMoving;       // 是否移动中
    public int Hp;              // 当前血量
    public int MaxHp;           // 最大血量
    public int Attack;          // 攻击力
    public int Defense;         // 防御力
    public float CreateTime;    // 创建时间
    public float LifeTime;      // 存活时间
    public float MaxLifeTime;   // 最大存活时间（<=0 表示无限）
}
```

### EntitySpawnInfo 生成信息

```csharp
public struct EntitySpawnInfo
{
    public EntityType Type;
    public int ConfigId;
    public int CampId;
    public Vector3 Position;
    public float Rotation;
    public float MaxLifeTime;
}
```

### PlayerInput 玩家输入

```csharp
public struct PlayerInput
{
    public InputType Type;      // Move/Attack/UseSkill/UseItem/Interact
    public int PlayerId;        // 玩家实体 ID
    public Vector3 TargetPosition;   // 目标位置
    public int TargetEntityId;       // 目标实体 ID
    public int SkillId;              // 技能 ID
    public int ItemId;               // 道具 ID
    public float Timestamp;          // 时间戳
}
```

### GameplayCommand 游戏命令

```csharp
public struct GameplayCommand
{
    public CommandType Type;    // SpawnEntity/DestroyEntity/DamageEntity/MoveEntity/EndBattle
    public int EntityId;
    public int IntParam;
    public float FloatParam;
    public Vector3 VectorParam;
}
```

### BattleResult 战斗结果

```csharp
public struct BattleResult
{
    public BattleResultType ResultType;  // None/Victory/Defeat/Draw/Timeout
    public float Duration;       // 战斗时长
    public int Score;            // 得分
    public int KillCount;        // 击杀数
    public int DeathCount;       // 死亡数
}
```

### BuffAddInfo Buff 添加信息

```csharp
public struct BuffAddInfo
{
    public int ConfigId;
    public int CasterId;
    public BuffEffectType EffectType;  // AttackUp/DefenseUp/SpeedUp/HealOverTime/DamageOverTime/Stun
    public int EffectValue;
    public float Duration;
    public float TickInterval;
    public int MaxStack;
}
```

---

## 5. 平台服务

### ILocalStorage 本地存储

```csharp
public interface ILocalStorage
{
    bool HasKey(string key);
    string GetString(string key, string defaultValue = "");
    void SetString(string key, string value);
    int GetInt(string key, int defaultValue = 0);
    void SetInt(string key, int value);
    float GetFloat(string key, float defaultValue = 0f);
    void SetFloat(string key, float value);
    void DeleteKey(string key);
    void DeleteAll();
    void Save();
}

// 扩展方法
storage.GetObject<T>(key, defaultValue);
storage.SetObject<T>(key, value);
```

### ITimeService 时间服务

```csharp
public interface ITimeService
{
    float DeltaTime { get; }
    float UnscaledDeltaTime { get; }
    float Time { get; }
    float UnscaledTime { get; }
    float TimeScale { get; set; }
    long Timestamp { get; }
}
```

### INetworkClient 网络客户端

```csharp
public interface INetworkClient
{
    void Get(string url, Action<bool, string> callback);
    void Post(string url, string body, Action<bool, string> callback);
    void Download(string url, string savePath, Action<bool> callback, Action<float> progress = null);
}
```

### IDeviceService 设备服务

```csharp
public interface IDeviceService
{
    string DeviceId { get; }
    string DeviceModel { get; }
    string SystemVersion { get; }
    int ScreenWidth { get; }
    int ScreenHeight { get; }
    float ScreenDpi { get; }
    int BatteryLevel { get; }
    bool IsCharging { get; }
    void Vibrate(long milliseconds);
    void CopyToClipboard(string text);
    string GetFromClipboard();
}
```

---

## 快速开始

### 1. 场景设置

1. 创建空 GameObject，命名为 `CYFramework`
2. 挂载 `CYFrameworkEntry` 组件
3. 运行游戏，框架自动初始化

### 2. 基础使用

```csharp
using UnityEngine;
using CYFramework.Runtime.Core;
using CYFramework.Runtime.Gameplay.Abstraction;
using CYFramework.Runtime.Gameplay.OOP;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        var fw = CYFrameworkEntry.Instance;
        
        // 使用日志
        fw.Log.I("Game", "游戏启动");
        
        // 订阅事件
        fw.Event.Subscribe<GameEndEvent>(OnGameEnd);
        
        // 设置定时器
        fw.Timer.Delay(3f, () => StartGame());
        
        // 播放 BGM
        fw.Sound.PlayBGM("Audio/BGM/Title");
    }
    
    void StartGame()
    {
        var world = CYFrameworkEntry.Instance.GameplayWorld as GameplayWorldOOP;
        
        // 生成玩家
        int playerId = world.SpawnEntity(new EntitySpawnInfo
        {
            Type = EntityType.Player,
            ConfigId = 1,
            CampId = 1,
            Position = Vector3.zero
        });
        
        CYFrameworkEntry.Instance.Log.I("Game", $"玩家已生成: {playerId}");
    }
    
    void OnGameEnd(GameEndEvent evt)
    {
        Debug.Log(evt.IsWin ? "胜利！" : "失败！");
    }
}
```

---

*文档版本：1.0*  
*最后更新：2025年*
