# CYFramework 快速入门（5分钟上手）

## 🎯 这个框架能干什么？

简单说：**帮你省事**。把游戏开发中常用的功能都封装好了：

| 你想做的事 | 框架帮你做好了 |
|-----------|---------------|
| 播放音效/音乐 | ✅ `AudioService` |
| 保存/读取存档 | ✅ `SaveService` |
| 发送网络请求 | ✅ `NetworkService` |
| 发布/订阅事件 | ✅ `EventBus` |
| 对象池优化 | ✅ `PoolManager` |
| 打日志调试 | ✅ `CYLog` |

---

## 🚀 第一步：让框架跑起来

1. 新建一个空场景
2. 创建空物体，命名为 `[CYFramework]`
3. 给它添加 `CYBootstrap` 组件
4. 运行游戏

**看到这些日志就成功了：**
```
=== CYFramework 2.2 启动 ===
[ServiceLocator] 所有服务初始化完成，共 8 个
=== CYFramework 初始化完成 ===
```

---

## 📝 常用功能示例

### 1️⃣ 打日志

```csharp
using CYFramework.Infrastructure;

// 不同级别的日志
CYLog.Debug("调试信息");    // 灰色
CYLog.Info("普通信息");     // 白色
CYLog.Warning("警告");      // 黄色
CYLog.Error("错误");        // 红色
```

### 2️⃣ 播放音效

```csharp
using CYFramework.Infrastructure;
using CYFramework.Modules.Audio;

// 获取音频服务
var audio = ServiceLocator.Get<IAudioService>();

// 播放背景音乐
audio.PlayBGM("bgm_main");

// 播放音效
audio.PlaySFX("click");

// 调节音量 (0~1)
audio.SetBGMVolume(0.5f);
```

### 3️⃣ 保存/读取数据

```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Save;

// 获取存档服务
var save = ServiceLocator.Get<SaveService>();

// 定义你的存档数据
public class PlayerData
{
    public int Level = 1;
    public int Gold = 0;
    public string Name = "玩家";
}

// 保存
var data = new PlayerData { Level = 5, Gold = 100 };
save.Save("player", data);

// 读取
var loaded = save.Load<PlayerData>("player");
CYLog.Info($"等级: {loaded.Level}, 金币: {loaded.Gold}");
```

### 4️⃣ 发送事件（解耦神器）

**场景：** 玩家升级了，需要通知 UI、音效、成就系统...

**传统做法（耦合严重）：**
```csharp
// ❌ 不好的写法
uiManager.RefreshLevel();
audioManager.PlayLevelUp();
achievementManager.CheckLevelUp();
// 每加一个系统就要改这里...
```

**用 EventBus（解耦）：**
```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Event;

// 1. 定义事件
public struct PlayerLevelUpEvent
{
    public int NewLevel;
}

// 2. 发送事件（在玩家脚本里）
var eventBus = ServiceLocator.Get<EventBus>();
var evt = new PlayerLevelUpEvent { NewLevel = 5 };
eventBus.Post(ref evt);

// 3. 监听事件（在 UI 脚本里）
void Start()
{
    var eventBus = ServiceLocator.Get<EventBus>();
    eventBus.Subscribe<PlayerLevelUpEvent>(OnLevelUp, this);
}

void OnLevelUp(ref PlayerLevelUpEvent e)
{
    levelText.text = $"Lv.{e.NewLevel}";
}
```

### 5️⃣ 使用对象池（优化性能）

```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Pool;

// 获取对象池
var pool = ServiceLocator.Get<PoolManager>();

// 创建子弹池（预热10个）
pool.CreateGameObjectPool("Bullet", bulletPrefab, 10);

// 获取子弹（从池中取或新建）
var bullet = pool.SpawnGameObject("Bullet", spawnPos, Quaternion.identity);

// 回收子弹（不是销毁，是放回池中）
pool.DespawnGameObject("Bullet", bullet);
```

---

## ❓ 常见问题

### Q: 怎么获取服务？
```csharp
// 通用写法
var 服务 = ServiceLocator.Get<服务类型>();

// 例子
var audio = ServiceLocator.Get<IAudioService>();
var save = ServiceLocator.Get<SaveService>();
var eventBus = ServiceLocator.Get<EventBus>();
var pool = ServiceLocator.Get<PoolManager>();
```

### Q: 为什么要用 `ref` 传事件？
为了**零 GC**。结构体用 ref 传递不产生垃圾回收，游戏更流畅。

### Q: 微信小游戏怎么用？
在 `Project Settings > Player > Scripting Define Symbols` 添加：
```
CY_WECHAT;CY_SINGLE_THREAD
```
框架会自动切换到微信兼容的实现。

---

## 🎮 完整示例：一个简单的游戏管理器

```csharp
using UnityEngine;
using CYFramework.Infrastructure;
using CYFramework.Core.Event;
using CYFramework.Core.Save;
using CYFramework.Modules.Audio;

public class GameManager : MonoBehaviour
{
    // 游戏数据
    public class GameData
    {
        public int Score;
        public int HighScore;
    }
    
    private GameData _data;
    private EventBus _eventBus;
    private SaveService _saveService;
    private IAudioService _audioService;
    
    void Start()
    {
        // 获取服务
        _eventBus = ServiceLocator.Get<EventBus>();
        _saveService = ServiceLocator.Get<SaveService>();
        _audioService = ServiceLocator.Get<IAudioService>();
        
        // 读取存档
        _data = _saveService.Load<GameData>("game") ?? new GameData();
        
        // 播放背景音乐
        _audioService.PlayBGM("bgm_game");
        
        CYLog.Info($"游戏开始！最高分: {_data.HighScore}");
    }
    
    public void AddScore(int amount)
    {
        _data.Score += amount;
        
        // 播放音效
        _audioService.PlaySFX("score");
        
        // 发送事件通知 UI
        var evt = new ScoreChangedEvent { NewScore = _data.Score };
        _eventBus.Post(ref evt);
        
        // 检查最高分
        if (_data.Score > _data.HighScore)
        {
            _data.HighScore = _data.Score;
            _saveService.Save("game", _data);
            CYLog.Info($"新纪录！{_data.HighScore}");
        }
    }
}

// 分数变化事件
public struct ScoreChangedEvent
{
    public int NewScore;
}
```

---

## 📁 框架结构一览

```
CYFramework/
├── Runtime/                 # 运行时代码
│   ├── Infrastructure/      # 基础设施
│   │   ├── CYBootstrap.cs   # 👈 启动器（挂这个组件）
│   │   ├── ServiceLocator   # 👈 服务定位器（获取服务用）
│   │   └── CYLog.cs         # 👈 日志系统
│   ├── Core/                # 核心服务
│   │   ├── Event/           # 事件系统
│   │   ├── Save/            # 存档系统
│   │   ├── Pool/            # 对象池
│   │   └── Network/         # 网络服务
│   └── Modules/             # 功能模块
│       └── Audio/           # 音频服务
└── Documentation/           # 文档
```

---

**有问题？** 直接问我！
