# CYFramework 使用指南

## 目录

1. [简介](#1-简介)
2. [安装与配置](#2-安装与配置)
3. [快速开始](#3-快速开始)
4. [核心概念](#4-核心概念)
5. [常用场景](#5-常用场景)
6. [性能优化](#6-性能优化)
7. [多平台适配](#7-多平台适配)
8. [常见问题](#8-常见问题)

---

## 1. 简介

CYFramework 是一个轻量级、高性能的 Unity 游戏框架，专为多平台开发设计。

### 特性

- **轻量级**：核心代码精简，无冗余依赖
- **高性能**：避免反射、减少 GC、支持 DOTS
- **多平台**：支持 PC、移动端、WebGL、微信小游戏
- **模块化**：按需使用，易于扩展
- **数据驱动**：逻辑与表现分离

### 架构层级

```
┌─────────────────────────────────────────┐
│              Application                 │  ← 你的游戏代码
├─────────────────────────────────────────┤
│           Gameplay Layer                 │  ← 玩法世界（OOP/DOTS）
├─────────────────────────────────────────┤
│            Core Layer                    │  ← 核心模块
├─────────────────────────────────────────┤
│           Platform Layer                 │  ← 平台抽象
└─────────────────────────────────────────┘
```

---

## 2. 安装与配置

### 2.1 导入框架

将 `CYFramework` 文件夹复制到 `Assets/` 目录下。

### 2.2 目录结构

```
Assets/CYFramework/
├── Runtime/
│   ├── Core/           # 核心模块
│   ├── Platform/       # 平台抽象
│   └── Gameplay/       # 玩法系统
│       ├── Abstraction/
│       ├── OOP/
│       └── DOTS/
├── Editor/             # 编辑器工具
├── Samples/            # 示例代码
├── Documentation/      # 文档
└── Resources/          # 资源目录
```

### 2.3 Assembly Definition

框架使用 Assembly Definition 进行代码隔离：

| asmdef | 说明 | 平台 |
|--------|------|------|
| CYFramework.Core | 核心代码 | 全平台 |
| CYFramework.Gameplay.DOTS | DOTS 实现 | PC/移动端 |
| CYFramework.Editor | 编辑器工具 | Editor |
| CYFramework.Samples | 示例代码 | 全平台 |

### 2.4 启用 DOTS（可选）

1. 打开 Package Manager
2. 搜索并安装 `Entities` 包
3. 在 Inspector 中勾选 `Use Dots Implementation`

### 2.5 框架配置（Inspector）

在 `CYFrameworkEntry` 组件上可以配置所有模块参数：

| 分类 | 配置项 | 说明 | 默认值 |
|------|--------|------|--------|
| **日志** | Log Level | 日志级别（Debug/Info/Warning/Error） | Debug |
| | Log Show Timestamp | 显示时间戳 | ✓ |
| | Log Show Module | 显示模块名 | ✓ |
| **对象池** | Pool Default Capacity | 默认最大容量 | 100 |
| | Pool Auto Expand | 自动扩容 | ✓ |
| **资源** | Resource Loader Type | 加载方式（Resources/AssetBundle/Addressables） | Resources |
| | Resources Path Prefix | 路径前缀 | 空 |
| | Asset Bundle Path | AB 存放路径 | Bundles |
| **存储** | Storage Prefix | 键名前缀 | CYGame_ |
| | Storage Auto Save | 自动保存 | ✗ |
| **声音** | BGM Volume | 背景音乐音量 | 1.0 |
| | SFX Volume | 音效音量 | 1.0 |
| | Mute BGM | 静音背景音乐 | ✗ |
| | Mute SFX | 静音音效 | ✗ |
| **UI** | UI Panel Path Prefix | 面板预制体路径前缀 | UI/Panels/ |
| | UI Click Sound Path | 点击音效路径 | 空 |
| **调度器** | Max Time Per Frame | 每帧最大执行时间(ms) | 5 |
| **玩法世界** | Use Dots Implementation | 使用 DOTS 实现 | ✗ |
| | Debug Mode | 调试模式 | ✗ |
| | Time Scale | 时间缩放 | 1.0 |
| | Max Entity Count | 最大实体数 | 1000 |

---

## 3. 快速开始

### 3.1 创建框架入口

1. 新建场景或打开现有场景
2. 创建空 GameObject，命名为 `CYFramework`
3. 添加 `CYFrameworkEntry` 组件
4. **在 Inspector 中配置各项参数**
5. 运行游戏

```
Hierarchy:
└── CYFramework (CYFrameworkEntry)
```

### 3.2 编写游戏代码

下面是一个完整的游戏示例，每一行都有详细注释：

```csharp
using UnityEngine;
using CYFramework.Runtime.Core;           // 框架核心命名空间
using CYFramework.Runtime.Gameplay.Abstraction;  // 玩法抽象（IGameplayWorld、EntitySpawnInfo 等）

/// <summary>
/// 游戏主控制器示例
/// 演示如何使用 CYFramework 的各个模块
/// </summary>
public class MyGame : MonoBehaviour
{
    // ========================================
    // 成员变量
    // ========================================
    
    /// <summary>
    /// 玩家实体的唯一 ID
    /// SpawnEntity 返回的 ID，后续操作玩家都需要这个 ID
    /// </summary>
    private int _playerId;
    
    /// <summary>
    /// 缓存玩法世界引用，避免每帧获取
    /// 使用接口类型，框架会自动选择 OOP 或 DOTS 实现
    /// </summary>
    private IGameplayWorld _world;

    // ========================================
    // Unity 生命周期
    // ========================================

    void Start()
    {
        // --------------------------------------------------
        // 第一步：检查框架是否就绪
        // --------------------------------------------------
        // CYFW.IsReady 会检查：
        // 1. CYFrameworkEntry 是否存在
        // 2. 框架是否已完成初始化
        // 如果场景中没有挂载 CYFrameworkEntry 组件，这里会返回 false
        if (!CYFW.IsReady)
        {
            Debug.LogError("框架未初始化！请在场景中添加 CYFrameworkEntry 组件");
            return;
        }

        // --------------------------------------------------
        // 第二步：获取并缓存玩法世界
        // --------------------------------------------------
        // CYFW.World 返回的是 IGameplayWorld 接口
        // 框架会根据平台自动选择实现：
        //   - WebGL/微信小游戏 → GameplayWorldOOP
        //   - PC/iOS/Android → GameplayWorldDOTS（如果 DOTS 可用）
        // 用户代码只需使用接口，无需关心具体实现
        _world = CYFW.World;
        
        if (_world == null)
        {
            Debug.LogError("玩法世界未创建！请检查 CYFrameworkEntry 是否启用了玩法世界");
            return;
        }

        // --------------------------------------------------
        // 第三步：生成玩家实体
        // --------------------------------------------------
        // SpawnEntity 需要一个 EntitySpawnInfo 结构体，包含：
        // - Type: 实体类型（Player/Enemy/Npc/Projectile/Item/Effect）
        // - ConfigId: 配置表 ID，用于读取数值配置
        // - CampId: 阵营 ID，用于判断敌我关系
        // - Position: 初始位置
        // - Rotation: 初始朝向（Y 轴欧拉角）
        // - MaxLifeTime: 最大存活时间，<=0 表示永久存在
        _playerId = _world.SpawnEntity(new EntitySpawnInfo
        {
            Type = EntityType.Player,    // 玩家类型
            ConfigId = 1,                // 配置表 ID = 1
            CampId = 1,                  // 阵营 1（玩家阵营）
            Position = Vector3.zero,     // 出生在原点
            Rotation = 0f                // 朝向 Z 轴正方向
        });
        
        // 输出日志，确认玩家生成成功
        // Log.I 是框架提供的日志工具，第一个参数是标签，第二个是消息
        Log.I("Game", $"玩家已生成，ID = {_playerId}");

        // --------------------------------------------------
        // 第四步：播放背景音乐
        // --------------------------------------------------
        // PlayBGM 参数：
        // 1. 音频路径（相对于 Resources 或配置的加载路径）
        // 2. loop: 是否循环（默认 true）
        // 3. fadeDuration: 淡入时间（默认 0.5 秒）
        CYFW.Sound.PlayBGM("Audio/BGM/Game", loop: true, fadeDuration: 1f);

        // --------------------------------------------------
        // 第五步：订阅游戏事件
        // --------------------------------------------------
        // 订阅伤害事件，当任何实体受到伤害时触发
        CYFW.Event.Subscribe<DamageEvent>(OnDamage);
    }

    void Update()
    {
        // 如果玩法世界不存在或玩家已死亡，不处理输入
        if (_world == null) return;
        
        // --------------------------------------------------
        // 处理鼠标右键输入（点击移动）
        // --------------------------------------------------
        // Input.GetMouseButtonDown(1) 检测鼠标右键是否刚按下
        // 0 = 左键, 1 = 右键, 2 = 中键
        if (Input.GetMouseButtonDown(1))
        {
            HandleMoveInput();
        }
        
        // --------------------------------------------------
        // 处理鼠标左键输入（点击攻击）
        // --------------------------------------------------
        if (Input.GetMouseButtonDown(0))
        {
            HandleAttackInput();
        }
    }
    
    void OnDestroy()
    {
        // 重要：在对象销毁时取消事件订阅，防止内存泄漏
        CYFW.Event?.Unsubscribe<DamageEvent>(OnDamage);
    }

    // ========================================
    // 输入处理
    // ========================================

    /// <summary>
    /// 处理移动输入
    /// 从屏幕点击位置发射射线，找到地面交点，移动玩家
    /// </summary>
    private void HandleMoveInput()
    {
        // 从相机发射一条射线，穿过鼠标点击的屏幕位置
        // Camera.main 是场景中 Tag = MainCamera 的相机
        // Input.mousePosition 是鼠标在屏幕上的像素坐标
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        // 物理射线检测，检测射线是否碰到任何 Collider
        // out RaycastHit hit 会输出碰撞信息
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // hit.point 是射线与碰撞体的交点（世界坐标）
            // 把这个位置作为移动目标发送给玩法世界
            _world.HandleInput(new PlayerInput
            {
                Type = InputType.Move,           // 输入类型：移动
                PlayerId = _playerId,            // 哪个玩家在操作
                TargetPosition = hit.point       // 移动到这个位置
            });
            
            Log.D("Game", $"移动到: {hit.point}");
        }
    }
    
    /// <summary>
    /// 处理攻击输入
    /// 检测点击是否命中敌人，如果是则发起攻击
    /// </summary>
    private void HandleAttackInput()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // 检查点击的物体是否有 EntityView 组件（假设敌人物体上有这个）
            // 这里简化处理，实际项目中需要根据具体实现
            var entityView = hit.collider.GetComponent<EntityView>();
            if (entityView != null && entityView.EntityId != _playerId)
            {
                // 发送攻击输入
                _world.HandleInput(new PlayerInput
                {
                    Type = InputType.Attack,         // 输入类型：攻击
                    PlayerId = _playerId,            // 哪个玩家在攻击
                    TargetEntityId = entityView.EntityId  // 攻击目标
                });
            }
        }
    }

    // ========================================
    // 事件回调
    // ========================================

    /// <summary>
    /// 伤害事件回调
    /// 当任何实体受到伤害时触发
    /// </summary>
    private void OnDamage(DamageEvent evt)
    {
        Log.I("Game", $"实体 {evt.TargetId} 受到 {evt.Damage} 点伤害");
        
        // 如果是玩家受伤，可以更新 UI、播放音效等
        if (evt.TargetId == _playerId)
        {
            // 播放受伤音效
            CYFW.Sound.PlaySFX("Audio/SFX/Hurt");
            
            // 这里可以更新血条 UI
            // UpdatePlayerHpBar();
        }
    }
}

/// <summary>
/// 实体视图组件（示例）
/// 挂在游戏物体上，用于关联 GameObject 和玩法实体
/// </summary>
public class EntityView : MonoBehaviour
{
    /// <summary>
    /// 对应的玩法实体 ID
    /// </summary>
    public int EntityId { get; set; }
}
```

### 3.3 运行示例

框架提供了完整的示例代码：

1. 打开 `Assets/CYFramework/Samples/` 目录
2. 查看 `SampleGame.cs`
3. 将其挂载到场景中的 GameObject 上
4. 运行游戏

---

## 4. 核心概念

### 4.1 模块系统

**什么是模块？**

模块是框架功能的基本单元，每个模块负责一个特定功能（如日志、定时器、声音等）。所有模块都实现 `IModule` 接口。

**模块特性**：

| 特性 | 说明 |
|------|------|
| **优先级** | 数字越小越先初始化。日志模块是 -10（最先），UI 模块是 25（较晚） |
| **生命周期** | `Initialize()` → 每帧 `Update()` → `Shutdown()` |
| **懒加载** | 首次访问时自动从模块管理器获取，后续访问使用缓存 |
| **可配置** | 可以在 Inspector 中配置参数，框架启动时自动应用 |

**获取模块的两种方式**：

```csharp
// ========================================
// 方式一：使用 CYFW 静态类（推荐）
// ========================================
// 直接使用，代码简洁
CYFW.Timer.Delay(1f, () => { });
CYFW.Sound.PlaySFX("Audio/Click");
CYFW.Log.I("Tag", "消息");

// ========================================
// 方式二：通过框架入口获取
// ========================================
var fw = CYFrameworkEntry.Instance;
var log = fw.Log;         // 日志模块
var evt = fw.Event;       // 事件模块
var timer = fw.Timer;     // 定时器模块
var pool = fw.Pool;       // 对象池模块
var res = fw.Resource;    // 资源模块
var storage = fw.Storage; // 存储模块
var sound = fw.Sound;     // 声音模块
var ui = fw.UI;           // UI模块
var proc = fw.Procedure;  // 流程模块
var sched = fw.Scheduler; // 调度器模块
```

**模块优先级表**：

| 模块 | 优先级 | 说明 |
|------|--------|------|
| LogModule | -10 | 最先初始化，其他模块可以用它输出日志 |
| EventModule | 0 | 事件系统，模块间通信基础 |
| ObjectPoolModule | 5 | 对象池 |
| TimerModule | 10 | 定时器 |
| SchedulerModule | 12 | 分帧调度 |
| ProcedureModule | 15 | 流程管理 |
| ResourceModule | 18 | 资源加载 |
| StorageModule | 20 | 数据存储 |
| SoundModule | 25 | 声音播放 |
| UIModule | 25 | UI 管理 |

---

### 4.2 事件系统

**什么是事件系统？**

事件系统用于**解耦模块间的通信**。发布者不需要知道谁在监听，订阅者也不需要知道谁在发布。

**为什么事件必须是 struct？**

- **避免 GC**：struct 是值类型，分配在栈上，不产生垃圾回收
- **性能更好**：传递时直接复制值，不需要引用
- **线程安全**：值类型天然是线程安全的

**完整示例**：

```csharp
// ========================================
// 第一步：定义事件结构体
// ========================================
// 事件必须是 struct（值类型），不能是 class
// 字段应该是基础类型或值类型，避免引用类型
public struct EnemyKilledEvent
{
    public int EnemyId;      // 被杀死的敌人 ID
    public int KillerId;     // 击杀者 ID
    public int Score;        // 获得的分数
    public Vector3 Position; // 死亡位置（可选，用于显示特效）
}

// ========================================
// 第二步：订阅事件（在 Start 或 OnEnable 中）
// ========================================
void Start()
{
    // Subscribe<T> 需要一个 Action<T> 类型的回调
    // T 就是事件类型
    CYFW.Event.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
}

// ========================================
// 第三步：发布事件（在需要通知的地方）
// ========================================
void KillEnemy(int enemyId, int killerId)
{
    // 创建事件数据
    var evt = new EnemyKilledEvent
    {
        EnemyId = enemyId,
        KillerId = killerId,
        Score = 50,
        Position = GetEnemyPosition(enemyId)
    };
    
    // 发布事件，所有订阅者会立即收到通知
    CYFW.Event.Publish(evt);
}

// ========================================
// 第四步：处理事件
// ========================================
// 回调方法的参数类型必须和订阅时的泛型类型一致
void OnEnemyKilled(EnemyKilledEvent evt)
{
    // 增加分数
    _totalScore += evt.Score;
    
    // 更新 UI
    _scoreText.text = $"分数: {_totalScore}";
    
    // 在死亡位置播放特效
    SpawnEffect("DeathEffect", evt.Position);
    
    // 播放音效
    CYFW.Sound.PlaySFX("Audio/SFX/Kill");
}

// ========================================
// 第五步：取消订阅（非常重要！）
// ========================================
// 必须在 OnDestroy 或 OnDisable 中取消订阅
// 否则对象销毁后，事件回调会指向已销毁的对象，导致报错
void OnDestroy()
{
    // 使用 ?. 运算符防止框架已关闭
    CYFW.Event?.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
}
```

**事件系统使用场景**：

| 场景 | 发布者 | 订阅者 |
|------|--------|--------|
| 敌人死亡 | CombatSystem | UI（显示得分）、SoundManager（播放音效）、QuestSystem（任务进度） |
| 玩家升级 | PlayerSystem | UI（显示升级特效）、SkillSystem（解锁技能） |
| 游戏暂停 | PauseButton | SoundManager（暂停BGM）、World（暂停逻辑） |
| 关卡完成 | LevelManager | UI（显示结算）、SaveSystem（保存进度） |

### 4.3 玩法世界

**什么是玩法世界？**

玩法世界（`IGameplayWorld`）是**游戏逻辑的核心容器**，负责管理所有游戏实体和子系统。它类似于 ECS 架构中的 World。

**架构图**：

```
┌─────────────────────────────────────────────────────────────┐
│                    玩法世界 (IGameplayWorld)                 │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │  实体管理    │  │  子系统     │  │  战斗状态   │         │
│  ├─────────────┤  ├─────────────┤  ├─────────────┤         │
│  │ • 玩家      │  │ Movement    │  │ • 游戏时间  │         │
│  │ • 敌人      │  │ AI          │  │ • 战斗结果  │         │
│  │ • NPC       │  │ Combat      │  │ • 击杀统计  │         │
│  │ • 投射物    │  │ Buff        │  │ • 死亡统计  │         │
│  │ • 道具      │  │ LifeTime    │  │             │         │
│  │ • 特效      │  │ Cleanup     │  │             │         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
├─────────────────────────────────────────────────────────────┤
│  Tick(deltaTime)  →  各子系统按顺序更新  →  清理死亡实体     │
└─────────────────────────────────────────────────────────────┘
```

**OOP vs DOTS 实现**：

| 特性 | GameplayWorldOOP | GameplayWorldDOTS |
|------|------------------|-------------------|
| 适用平台 | 全平台 | PC/iOS/Android |
| 实体上限 | 数百~数千 | 数千~数万 |
| 内存占用 | 较高 | 较低 |
| CPU 效率 | 一般 | 高（多线程+缓存友好） |
| 开发难度 | 简单 | 复杂 |
| 推荐场景 | 小游戏/休闲游戏 | 大规模战斗/RTS |

**子系统说明**：

| 系统 | 职责 |
|------|------|
| **MovementSystem** | 处理实体移动，路径寻找，位置更新 |
| **AISystem** | 敌人 AI 状态机，巡逻/追击/攻击等行为 |
| **CombatSystem** | 攻击请求处理，伤害计算，技能释放 |
| **BuffSystem** | Buff/Debuff 管理，效果叠加，持续时间 |
| **LifeTimeSystem** | 限时实体管理（如投射物、特效的自动销毁） |
| **CleanupSystem** | 清理死亡实体，回收资源 |

**使用示例**：

```csharp
// ========================================
// 获取玩法世界
// ========================================
// CYFW.World 返回 IGameplayWorld 接口
// 直接使用接口即可，框架会自动选择 OOP 或 DOTS 实现
// WebGL/微信小游戏 → GameplayWorldOOP
// PC/iOS/Android → GameplayWorldDOTS（如果可用）
IGameplayWorld world = CYFW.World;

// 注意：不要强制转换为具体实现！
// ❌ var world = CYFW.World as GameplayWorldOOP;  // 错误
// ✅ IGameplayWorld world = CYFW.World;           // 正确

// ========================================
// 生成实体
// ========================================
// 玩家
int playerId = world.SpawnEntity(new EntitySpawnInfo
{
    Type = EntityType.Player,
    ConfigId = 1,        // 配置表 ID，用于读取血量/攻击力等属性
    CampId = 1,          // 阵营 1 = 玩家方
    Position = Vector3.zero
});

// 敌人（30秒后自动销毁）
int enemyId = world.SpawnEntity(new EntitySpawnInfo
{
    Type = EntityType.Enemy,
    ConfigId = 100,
    CampId = 2,          // 阵营 2 = 敌方
    Position = new Vector3(10, 0, 10),
    MaxLifeTime = 30f    // 30秒后自动消失
});

// ========================================
// 操作实体
// ========================================
// 移动
world.MoveEntityTo(playerId, new Vector3(5, 0, 5));

// 造成伤害
world.DamageEntity(enemyId, 50);

// 治疗
world.HealEntity(playerId, 20);

// 销毁
world.DestroyEntity(enemyId);

// ========================================
// 获取子系统
// ========================================
var aiSystem = world.GetSystem<AISystem>();
var combatSystem = world.GetSystem<CombatSystem>();
var buffSystem = world.GetSystem<BuffSystem>();

// 给敌人添加 AI
aiSystem.AddAI(enemyId);

// 请求攻击
combatSystem.RequestAttack(playerId, enemyId, skillId: 1);

// 添加 Buff
buffSystem.AddBuff(playerId, new BuffAddInfo
{
    EffectType = BuffEffectType.AttackUp,
    EffectValue = 20,    // 攻击力 +20
    Duration = 10f       // 持续 10 秒
});

// ========================================
// 控制玩法世界
// ========================================
world.Pause();           // 暂停
world.Resume();          // 恢复
world.SetTimeScale(2f);  // 2倍速
world.Reset();           // 重置（清空所有实体）
```

---

### 4.4 实体与组件

**什么是实体？**

实体（Entity）是游戏中的一个对象，如玩家、敌人、子弹等。每个实体有一个唯一的 **ID**（整数）。

**为什么用 struct 存储实体数据？**

- **避免 GC**：值类型不产生垃圾
- **缓存友好**：连续内存，CPU 缓存命中率高
- **性能好**：没有引用类型的间接寻址开销

**实体数据结构详解**：

```csharp
/// <summary>
/// 实体数据（核心数据结构）
/// 使用 struct 以避免 GC，所有字段都是值类型
/// </summary>
public struct EntityData
{
    // ========== 标识信息 ==========
    public int Id;              // 唯一 ID，框架自动分配
    public EntityType Type;     // 类型：Player/Enemy/Npc/Projectile/Item/Effect
    public EntityState State;   // 状态：Invalid/Active/Dead/PendingDestroy
    public int ConfigId;        // 配置表 ID，用于读取数值配置
    public int CampId;          // 阵营 ID，同阵营不会互相攻击
    
    // ========== 位置信息 ==========
    public Vector3 Position;    // 当前世界坐标
    public float Rotation;      // 朝向（Y 轴欧拉角，0 = 面朝 Z 正方向）
    public Vector3 TargetPosition;  // 移动目标位置
    public bool IsMoving;       // 是否正在移动中
    public float MoveSpeed;     // 移动速度（单位/秒）
    
    // ========== 属性信息 ==========
    public int Hp;              // 当前血量
    public int MaxHp;           // 最大血量
    public int Attack;          // 攻击力
    public int Defense;         // 防御力
    
    // ========== 生命周期 ==========
    public float CreateTime;    // 创建时间（游戏时间）
    public float LifeTime;      // 已存活时间
    public float MaxLifeTime;   // 最大存活时间（<=0 表示无限）
}
```

**实体类型枚举**：

```csharp
public enum EntityType
{
    Player,      // 玩家
    Enemy,       // 敌人
    Npc,         // NPC（中立单位）
    Projectile,  // 投射物（子弹、箭矢等）
    Item,        // 道具
    Effect       // 特效
}
```

**实体状态枚举**：

```csharp
public enum EntityState
{
    Invalid,         // 无效（已回收）
    Active,          // 活跃（正常运行）
    Dead,            // 死亡（等待清理）
    PendingDestroy   // 待销毁（下一帧清理）
}
```

**实体操作示例**：

```csharp
// ========================================
// 生成实体
// ========================================
int id = world.SpawnEntity(new EntitySpawnInfo
{
    Type = EntityType.Enemy,
    ConfigId = 101,
    CampId = 2,
    Position = spawnPoint.position,
    Rotation = 180f,     // 面朝 Z 负方向
    MaxLifeTime = 60f    // 60秒后自动消失
});

// ========================================
// 获取实体数据
// ========================================
// 方式一：TryGetEntity（推荐，安全）
if (world.TryGetEntity(id, out EntityData data))
{
    Debug.Log($"实体 {id} 位置: {data.Position}");
    Debug.Log($"血量: {data.Hp}/{data.MaxHp}");
    Debug.Log($"状态: {data.State}");
}

// 方式二：GetEntitySnapshot（用于 UI 显示）
EntitySnapshot snapshot = world.GetEntitySnapshot(id);

// ========================================
// 判断实体是否存活
// ========================================
if (world.IsEntityAlive(id))
{
    // 实体还活着
}

// ========================================
// 遍历所有实体
// ========================================
var allEntities = world.GetAllEntities();
foreach (var entity in allEntities)
{
    if (entity.Type == EntityType.Enemy && entity.State == EntityState.Active)
    {
        // 处理所有活跃的敌人
    }
}

// ========================================
// 销毁实体
// ========================================
world.DestroyEntity(id);  // 标记为 PendingDestroy，下帧清理
```

---

## 5. 常用场景

### 5.1 定时器使用

```csharp
// 使用 CYFW 快捷访问
var timer = CYFW.Timer;

// 延时执行
timer.Delay(3f, () => ShowReward());

// 倒计时
int countdownId = -1;
int remaining = 60;

countdownId = timer.Loop(1f, () =>
{
    remaining--;
    UpdateCountdownUI(remaining);
    
    if (remaining <= 0)
    {
        timer.Cancel(countdownId);
        OnTimeUp();
    }
});

// 暂停时使用真实时间
timer.Delay(5f, () => ShowPauseHint(), useUnscaledTime: true);
```

### 5.2 对象池使用

```csharp
var pool = CYFW.Pool;

// 子弹类
public class Bullet
{
    public GameObject GameObject;
    public float Speed;
    public int Damage;
    
    public void Reset()
    {
        Speed = 0;
        Damage = 0;
    }
}

// 发射子弹
void FireBullet()
{
    Bullet bullet = pool.Get<Bullet>();
    bullet.Speed = 20f;
    bullet.Damage = 10;
    // 使用子弹...
}

// 回收子弹
void OnBulletHit(Bullet bullet)
{
    bullet.Reset();
    pool.Return(bullet);
}
```

### 5.3 资源加载

```csharp
var res = CYFW.Resource;

// 同步加载（小资源）
Sprite icon = res.Load<Sprite>("UI/Icons/Coin");

// 异步加载（大资源）
res.LoadAsync<GameObject>("Prefabs/Boss", prefab =>
{
    if (prefab != null)
    {
        Instantiate(prefab, spawnPoint.position, Quaternion.identity);
    }
});

// 预加载
void PreloadResources()
{
    res.Load<AudioClip>("Audio/SFX/Hit");
    res.Load<AudioClip>("Audio/SFX/Die");
    res.Load<GameObject>("Prefabs/Effect/Explosion");
}
```

### 5.4 数据存储

**多平台支持**：

| 平台 | 存储实现 | 说明 |
|------|----------|------|
| PC/iOS/Android | `UnityLocalStorage` | 使用 Unity `PlayerPrefs` |
| 微信小游戏 | `WeChatLocalStorage` | 使用 `wx.setStorageSync` 等 API |

框架会根据编译宏 `WECHAT_MINIGAME` 自动选择存储实现，用户代码无需修改。

```csharp
var storage = CYFW.Storage;

// ========================================
// 保存游戏进度
// ========================================
// 定义存档数据结构（必须标记 [Serializable]）
[Serializable]
public class SaveData
{
    public int Level;           // 当前关卡
    public int Gold;            // 金币数量
    public List<int> UnlockedItems;  // 已解锁物品列表
}

void SaveGame()
{
    // 创建存档数据
    var data = new SaveData
    {
        Level = currentLevel,
        Gold = playerGold,
        UnlockedItems = unlockedList
    };
    
    // SetObject 会将对象序列化为 JSON 字符串存储
    storage.SetObject("SaveData", data);
    
    // 调用 Save 确保数据写入磁盘
    // 微信小游戏平台此调用为空操作（Sync API 自动持久化）
    storage.Save();
}

void LoadGame()
{
    // GetObject 会从存储读取 JSON 并反序列化为对象
    var data = storage.GetObject<SaveData>("SaveData");
    
    if (data != null)
    {
        currentLevel = data.Level;
        playerGold = data.Gold;
        unlockedList = data.UnlockedItems;
    }
}

// ========================================
// 其他常用 API
// ========================================
// 存储/读取基础类型
storage.SetInt("HighScore", 9999);
int score = storage.GetInt("HighScore", 0);

storage.SetFloat("MusicVolume", 0.8f);
float volume = storage.GetFloat("MusicVolume", 1f);

storage.SetString("PlayerName", "玩家1");
string name = storage.GetString("PlayerName", "默认名称");

// 检查键是否存在
if (storage.HasKey("SaveData"))
{
    // 存档已存在
}

// 删除数据
storage.DeleteKey("TempData");
storage.DeleteAll();  // 清空所有存储（慎用！）
```

### 5.5 流程管理

**注册方式**：

| 方式 | 说明 |
|------|------|
| 手动注册（默认） | 调用 `RegisterProcedure()` 逐个注册，轻量无反射 |
| 自动注册（可选） | 设置 `AutoRegister = true`，通过反射自动注册所有 `ProcedureBase` 子类 |

```csharp
// ========================================
// 定义流程
// ========================================
public class ProcedureSplash : ProcedureBase
{
    public override void OnEnter(ProcedureModule owner)
    {
        base.OnEnter(owner);
        // 显示 Logo
        CYFW.Timer.Delay(2f, () =>
        {
            ChangeProcedure<ProcedureLogin>();
        });
    }
}

public class ProcedureLogin : ProcedureBase
{
    public override void OnEnter(ProcedureModule owner)
    {
        base.OnEnter(owner);
        // 显示登录界面
        ShowLoginUI();
    }
    
    public void OnLoginSuccess()
    {
        ChangeProcedure<ProcedureMainMenu>();
    }
}

// ========================================
// 方式一：手动注册（默认，推荐）
// ========================================
var proc = CYFW.Procedure;
proc.RegisterProcedure(new ProcedureSplash());
proc.RegisterProcedure(new ProcedureLogin());
proc.RegisterProcedure(new ProcedureMainMenu());
proc.StartProcedure<ProcedureSplash>();

// ========================================
// 方式二：自动注册（可选，需要反射）
// ========================================
// 在 CYFrameworkEntry Inspector 中勾选 "流程自动注册"
// 或在代码中设置：
// CYFW.Procedure.AutoRegister = true;
// 然后直接启动：
// CYFW.Procedure.StartProcedure<ProcedureSplash>();
```

### 5.6 战斗系统

```csharp
// 获取玩法世界（使用接口，框架自动选择 OOP 或 DOTS 实现）
IGameplayWorld world = CYFW.World;

// 获取子系统
var combat = world.GetSystem<CombatSystem>();
var buff = world.GetSystem<BuffSystem>();
var ai = world.GetSystem<AISystem>();

// 请求攻击
combat.RequestAttack(attackerId, targetId, skillId: 0);

// 添加 Buff
buff.AddBuff(new AddBuffParams
{
    TargetId = entityId,    // 目标实体 ID
    ConfigId = 1001,        // Buff 配置 ID
    EffectType = BuffEffectType.AttackUp,  // 效果类型：攻击力提升
    EffectValue = 20,       // 攻击力 +20
    Duration = 10f,         // 持续 10 秒
    MaxStackCount = 3       // 最多叠加 3 层
});

// 检查 Buff
if (buff.HasBuff(entityId, BuffEffectType.Stun))
{
    // 被眩晕，无法行动
}

// 添加 AI
ai.AddAI(enemyId);
ai.SetAIState(enemyId, AIState.Chase);
```

### 5.7 UI 管理

```csharp
var ui = CYFW.UI;

// ========================================
// 1. 创建面板预制体
// ========================================
// 在 Resources/UI/Panels/ 目录下创建面板预制体
// 面板预制体需要挂载继承自 UIPanel 的脚本

// ========================================
// 2. 定义面板类
// ========================================
using CYFramework.Runtime.Core.UI;

public class MainMenuPanel : UIPanel
{
    // 设置层级（可选）
    public override UILayer Layer => UILayer.Main;

    public override void OnOpen(object param = null)
    {
        base.OnOpen(param);
        // 初始化面板数据
    }

    public void OnStartClick()
    {
        PlayClickSound();
        CloseSelf();
        OpenPanel<GamePanel>();
    }

    public void OnSettingsClick()
    {
        PlayClickSound();
        OpenPanel<SettingsPanel>();
    }
}

// ========================================
// 3. 打开/关闭面板
// ========================================

// 打开主菜单
ui.OpenPanel<MainMenuPanel>();

// 打开设置面板（弹窗层，会显示在主菜单上面）
ui.OpenPanel<SettingsPanel>();

// 关闭设置面板
ui.ClosePanel<SettingsPanel>();

// 返回上一个面板
ui.GoBack();

// ========================================
// 4. 面板间传递数据
// ========================================

// 打开商店，传入分类参数
ui.OpenPanel<ShopPanel>("weapons");

// 在 ShopPanel 中接收参数
public class ShopPanel : UIPanel
{
    public override void OnOpen(object param = null)
    {
        base.OnOpen(param);
        string category = param as string;  // "weapons"
        LoadShopItems(category);
    }
}
```

**面板生命周期**：

```
OnLoad()   → 面板首次加载（只调用一次）
OnOpen()   → 面板打开（可接收参数）
OnShow()   → 面板显示（每次显示都调用）
OnHide()   → 面板隐藏
OnClose()  → 面板关闭
OnUnload() → 面板销毁（IsCached=false 时才调用）
```

---

## 6. 性能优化

### 6.1 避免 GC

```csharp
// ❌ 错误：每帧创建新对象
void Update()
{
    var list = new List<Enemy>();  // GC!
    GetEnemies(list);
}

// ✅ 正确：复用对象
private List<Enemy> _enemyCache = new List<Enemy>();

void Update()
{
    _enemyCache.Clear();
    GetEnemies(_enemyCache);
}
```

### 6.2 使用对象池

```csharp
// ❌ 错误：频繁创建销毁
void SpawnBullet()
{
    var bullet = new Bullet();  // GC!
}

// ✅ 正确：使用对象池
void SpawnBullet()
{
    var bullet = pool.Get<Bullet>();
}
```

### 6.3 缓存模块引用

```csharp
// ❌ 错误：每次都获取
void Update()
{
    CYFrameworkEntry.Instance.Timer.xxx;  // 每帧查找
}

// ✅ 正确：缓存引用
private TimerModule _timer;

void Start()
{
    _timer = CYFrameworkEntry.Instance.Timer;
}

void Update()
{
    _timer.xxx;
}
```

### 6.4 使用分帧调度

```csharp
// ❌ 错误：一帧处理太多
void ProcessAllEnemies()
{
    foreach (var enemy in allEnemies)  // 可能卡顿
    {
        enemy.ComplexUpdate();
    }
}

// ✅ 正确：分帧处理
void ProcessAllEnemies()
{
    CYFrameworkEntry.Instance.Scheduler.ScheduleBatch(
        allEnemies,
        enemy => enemy.ComplexUpdate(),
        itemsPerFrame: 10
    );
}
```

### 6.5 启用 DOTS

对于大量实体的场景，使用 DOTS 实现：

```csharp
var config = new GameplayConfig
{
    UseDotsImplementation = true,
    MaxEntityCount = 10000
};
```

---

## 7. 多平台适配

### 7.1 平台检测

```csharp
#if UNITY_WEBGL || WECHAT_MINIGAME
    // WebGL / 微信小游戏
    // 不支持 DOTS
    // 不支持多线程
#elif UNITY_IOS || UNITY_ANDROID
    // 移动平台
    // 支持 DOTS
    // 注意内存限制
#else
    // PC 平台
    // 完整功能
#endif
```

### 7.2 框架自动适配

框架会根据平台自动选择合适的实现：

| 平台 | 玩法世界 | 说明 |
|------|----------|------|
| PC | OOP / DOTS | 可选 |
| iOS/Android | OOP / DOTS | 可选 |
| WebGL | OOP | 强制 |
| 微信小游戏 | OOP | 强制 |

### 7.3 微信小游戏适配

```csharp
// 定义宏
// Project Settings → Player → Scripting Define Symbols
// 添加：WECHAT_MINIGAME

// 实现微信存储
public class WeChatStorage : ILocalStorage
{
    public string GetString(string key, string defaultValue = "")
    {
        // 调用微信 API
        return WX.StorageGetString(key, defaultValue);
    }
    // ...
}

// 替换存储实现
CYFrameworkEntry.Instance.Storage.SetStorageImpl(new WeChatStorage());
```

---

## 8. 常见问题

### Q1: 框架初始化失败？

确保场景中有 `CYFrameworkEntry` 组件，且只有一个实例。

### Q2: 事件没有触发？

1. 检查是否正确订阅了事件
2. 检查事件类型是否匹配（必须是 struct）
3. 检查是否在 OnDestroy 前取消订阅导致丢失

### Q3: DOTS 不可用？

1. 确认已安装 Entities 包
2. 确认平台支持（非 WebGL/小游戏）
3. 确认配置 `UseDotsImplementation = true`

### Q4: 资源加载失败？

1. 确认资源路径正确（相对于 Resources 目录）
2. 确认资源类型匹配
3. 检查资源是否在 Resources 目录下

### Q5: 定时器不执行？

1. 确认框架已初始化
2. 检查是否被取消
3. 使用 `useUnscaledTime` 避免受 TimeScale 影响

### Q6: 内存占用过高？

1. 使用对象池复用对象
2. 及时卸载不用的资源
3. 减少实体数量或使用 DOTS

---

## 联系与支持

- 查看 API 文档：`Documentation/API_Reference.md`
- 查看设计文档：`CYFramework_Architecture_Design.md`
- 查看示例代码：`Samples/SampleGame.cs`

---

*文档版本：1.0*  
*最后更新：2024年*
