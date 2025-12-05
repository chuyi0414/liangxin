# CYFramework 示例

## 使用方法

1. 在场景中创建一个空 GameObject，命名为 `CYFramework`
2. 挂载 `CYFrameworkEntry` 组件
3. 创建另一个 GameObject，挂载 `SampleGame` 组件
4. 运行游戏

## 操作说明

- **鼠标右键点击**：移动玩家到目标位置
- **空格键**：攻击最近的敌人

## 示例功能演示

### 框架核心模块

```csharp
var fw = CYFrameworkEntry.Instance;

// 日志
fw.Log.I("Game", "游戏开始");

// 事件
fw.Event.Subscribe<DamageEvent>(OnDamage);
fw.Event.Publish(new DamageEvent { ... });

// 定时器
fw.Timer.Delay(3f, () => Debug.Log("3秒后"));
fw.Timer.Loop(1f, () => Debug.Log("每秒执行"));

// 对象池
var obj = fw.Pool.Get<MyClass>();
fw.Pool.Return(obj);

// 存储
fw.Storage.SetInt("HighScore", 100);
int score = fw.Storage.GetInt("HighScore");

// 声音
fw.Sound.PlayBGM("Audio/BGM/Main");
fw.Sound.PlaySFX("Audio/SFX/Click");
```

### 玩法世界

```csharp
var world = fw.GameplayWorld as GameplayWorldOOP;

// 生成实体
int id = world.SpawnEntity(new EntitySpawnInfo { ... });

// 移动实体
world.MoveEntityTo(id, targetPosition);

// 造成伤害
world.DamageEntity(id, 50);

// 处理输入
world.HandleInput(new PlayerInput { Type = InputType.Attack, ... });

// 执行命令
world.ExecuteCommand(new GameplayCommand { Type = CommandType.SpawnEntity, ... });

// 获取战斗结果
BattleResult result = world.GetBattleResult();
```
