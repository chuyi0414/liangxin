# CYFramework Resources

此目录用于存放框架自带的资源文件。

## 目录结构

```
Resources/
├── Prefabs/        # 预制体
├── Configs/        # 配置文件（ScriptableObject）
├── Audio/          # 音频资源
│   ├── BGM/        # 背景音乐
│   └── SFX/        # 音效
└── UI/             # UI 资源
```

## 使用方法

通过 ResourceModule 加载资源：

```csharp
var fw = CYFrameworkEntry.Instance;

// 同步加载
var prefab = fw.Resource.Load<GameObject>("Prefabs/Player");

// 异步加载
fw.Resource.LoadAsync<AudioClip>("Audio/BGM/Main", clip => {
    fw.Sound.PlayBGM(clip);
});
```
