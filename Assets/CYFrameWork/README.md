# CYFramework 2.2

**工业级 Unity 游戏框架** - 一套"可落地"的多平台底座

## 特性

- ✅ **零 GC** - 事件系统、对象池全程无装箱
- ✅ **多平台** - PC / Android / iOS / 微信小游戏 / WebGL
- ✅ **混合架构** - OOP 写逻辑，DOTS 做计算（PC 端可选）
- ✅ **平台适配** - 自动处理微信/WebGL 的 API 限制
- ✅ **开箱即用** - 网络、存档、音频、热更新一应俱全

## 快速开始

### 1. 安装

将 `CYFramework` 文件夹放入 `Assets/` 目录。

### 2. 配置启动器

1. 创建空 GameObject，命名为 `[CYFramework]`
2. 添加 `CYBootstrap` 组件
3. 运行即可

### 3. 使用服务

```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Event;

public class MyGame : MonoBehaviour
{
    void Start()
    {
        // 获取服务
        var eventBus = ServiceLocator.Get<EventBus>();
        
        // 订阅事件
        eventBus.Subscribe<GameEvent>(OnGameEvent, this);
        
        // 发布事件
        var evt = new GameEvent { Score = 100 };
        eventBus.Post(ref evt);
    }
    
    void OnGameEvent(GameEvent e)
    {
        CYLog.Info($"得分: {e.Score}");
    }
}

public struct GameEvent { public int Score; }
```

## 核心模块

| 模块 | 说明 | 命名空间 |
|------|------|----------|
| **ServiceLocator** | 依赖注入 | `CYFramework.Infrastructure` |
| **EventBus** | 零 GC 事件 | `CYFramework.Core.Event` |
| **CYLog** | 分级日志 | `CYFramework.Infrastructure` |
| **PoolManager** | 对象池 | `CYFramework.Core.Pool` |
| **NetworkService** | HTTP/WebSocket | `CYFramework.Core.Network` |
| **SaveService** | 加密存档 | `CYFramework.Core.Save` |
| **AudioService** | 音频管理 | `CYFramework.Modules.Audio` |
| **ConfigLoader** | 配置加载 | `CYFramework.Core.Config` |
| **ResourceLoader** | 资源加载 | `CYFramework.Core.Resource` |

## 平台宏定义

在 `Player Settings > Scripting Define Symbols` 添加：

| 平台 | 宏定义 |
|------|--------|
| 微信小游戏 | `CY_WECHAT;CY_SINGLE_THREAD` |
| PC 旗舰版 | `CY_PC;ENABLE_DOTS` |
| 移动端 | `CY_MOBILE` |

## 调试工具

| 工具 | 快捷键 | 功能 |
|------|--------|------|
| RuntimeProfiler | `F1` | FPS、内存、DrawCall 监控 |
| CheatConsole | `` ` `` | 命令控制台 |

## 目录结构

```
Assets/CYFramework/
├── Runtime/              # 运行时代码
│   ├── Infrastructure/   # 启动器、服务定位器
│   ├── Platform/         # 平台适配器
│   ├── Core/             # 核心服务
│   ├── Gameplay/         # 玩法核心
│   ├── Modules/          # 功能模块
│   └── Debug/            # 调试工具
├── Editor/               # 编辑器工具
├── Plugins/WebGL/        # JS 桥接
├── Tests/                # 单元测试
└── Documentation/        # 文档
    ├── API_Reference.md  # API 参考
    └── Usage_Guide.md    # 使用指南
```

## 文档

- [API 参考文档](Documentation/API_Reference.md) - 完整 API 说明
- [使用指南](Documentation/Usage_Guide.md) - 详细使用示例
- [设计文档](CYFramework.md) - 架构设计白皮书

## 性能目标

| 指标 | 微信/WebGL | Mobile | PC |
|------|-----------|--------|----|
| 帧率 | 45-60 FPS | 60-90 FPS | 60-144 FPS |
| DrawCall | < 100 | < 300 | < 1000 |
| 每帧 GC | 0 | 0 | < 1KB |

## 许可证

MIT License

---

**版本**: 2.2 Enhanced Hybrid  
**Unity 版本**: 2021.3 LTS 及以上
