# CYFramework 2.2 使用指南

本文档提供详细的使用示例和最佳实践。

---

## 目录

1. [项目配置](#1-项目配置)
2. [游戏启动流程](#2-游戏启动流程)
3. [场景管理](#3-场景管理)
4. [UI 开发模式](#4-ui-开发模式)
5. [网络通信](#5-网络通信)
6. [存档系统](#6-存档系统)
7. [音频管理](#7-音频管理)
8. [对象池优化](#8-对象池优化)
9. [玩法开发](#9-玩法开发)
10. [调试与测试](#10-调试与测试)
11. [发布构建](#11-发布构建)

---

## 1. 项目配置

### 1.1 初始设置

1. **导入框架**
   ```
   将 CYFramework 文件夹拖入 Assets/
   ```

2. **创建启动场景**
   ```
   创建 Scenes/Bootstrap.unity
   创建空 GameObject 命名为 [CYFramework]
   添加 CYBootstrap 组件
   ```

3. **配置 Build Settings**
   ```
   将 Bootstrap 场景设为第一个场景（Build Index 0）
   ```

### 1.2 平台宏定义

`Edit > Project Settings > Player > Scripting Define Symbols`

```
# 微信小游戏
CY_WECHAT;CY_SINGLE_THREAD

# PC 旗舰版
CY_PC;ENABLE_DOTS

# 移动端
CY_MOBILE
```

### 1.3 项目结构建议

```
Assets/
├── CYFramework/          # 框架（不要修改）
├── _Project/
│   ├── Scenes/
│   ├── Scripts/
│   │   ├── Game/         # 游戏逻辑
│   │   ├── UI/           # UI 逻辑
│   │   └── Data/         # 数据定义
│   ├── Prefabs/
│   ├── Resources/
│   │   ├── Config/       # 配置 SO
│   │   └── Audio/        # 音频资源
│   └── Art/
└── Plugins/
```

---

## 2. 游戏启动流程

### 2.1 启动器示例

```csharp
using CYFramework.Infrastructure;
using CYFramework.Core.Event;
using CYFramework.Core.Resource;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏管理器
/// 在 CYBootstrap 初始化完成后创建
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    private EventBus _eventBus;
    private IResourceLoader _resourceLoader;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnGameStart()
    {
        // 等待框架初始化完成后创建
        if (CYBootstrap.Instance == null) return;
        
        var go = new GameObject("[GameManager]");
        DontDestroyOnLoad(go);
        go.AddComponent<GameManager>();
    }
    
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // 获取服务
        _eventBus = ServiceLocator.Get<EventBus>();
        _resourceLoader = ServiceLocator.Get<IResourceLoader>();
        
        // 订阅事件
        _eventBus.Subscribe<SceneLoadedEvent>(OnSceneLoaded, this);
        
        // 开始游戏流程
        StartCoroutine(GameStartFlow());
    }
    
    System.Collections.IEnumerator GameStartFlow()
    {
        CYLog.Info("=== 游戏启动 ===");
        
        // 1. 显示 Logo
        yield return ShowLogo();
        
        // 2. 检查热更新
        yield return CheckHotUpdate();
        
        // 3. 加载配置
        yield return LoadConfigs();
        
        // 4. 进入主菜单
        yield return LoadMainMenu();
    }
    
    System.Collections.IEnumerator ShowLogo()
    {
        CYLog.Debug("显示 Logo...");
        yield return new WaitForSeconds(2f);
    }
    
    System.Collections.IEnumerator CheckHotUpdate()
    {
        CYLog.Debug("检查更新...");
        
        var hotUpdate = ServiceLocator.Get<IHotUpdateService>();
        // 实际更新逻辑...
        
        yield return null;
    }
    
    System.Collections.IEnumerator LoadConfigs()
    {
        CYLog.Debug("加载配置...");
        
        var configLoader = ServiceLocator.Get<IConfigLoader>();
        
        // 预加载所有配置
        yield return configLoader.PreloadAsync(new[] {
            "Config/GameSettings",
            "Config/Weapons",
            "Config/Enemies"
        });
    }
    
    System.Collections.IEnumerator LoadMainMenu()
    {
        CYLog.Debug("加载主菜单...");
        
        yield return _resourceLoader.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        
        // 发布事件
        var evt = new SceneLoadedEvent { SceneName = "MainMenu" };
        _eventBus.Post(ref evt);
    }
    
    void OnSceneLoaded(SceneLoadedEvent e)
    {
        CYLog.Info($"场景加载完成: {e.SceneName}");
    }
    
    void OnDestroy()
    {
        _eventBus?.UnsubscribeAll(this);
    }
}

// 事件定义
public struct SceneLoadedEvent
{
    public string SceneName;
}
```

---

## 3. 场景管理

### 3.1 场景加载

```csharp
public class SceneManager
{
    private readonly IResourceLoader _loader;
    private readonly EventBus _eventBus;
    
    public SceneManager()
    {
        _loader = ServiceLocator.Get<IResourceLoader>();
        _eventBus = ServiceLocator.Get<EventBus>();
    }
    
    /// <summary>
    /// 加载场景（带过渡）
    /// </summary>
    public async void LoadScene(string sceneName, System.Action<float> onProgress = null)
    {
        // 显示加载界面
        ShowLoadingScreen();
        
        // 清理 Scoped 服务
        ServiceLocator.ClearScoped();
        
        // 加载场景
        await _loader.LoadSceneAsync(sceneName, LoadSceneMode.Single, onProgress);
        
        // 隐藏加载界面
        HideLoadingScreen();
        
        // 发布事件
        var evt = new SceneLoadedEvent { SceneName = sceneName };
        _eventBus.Post(ref evt);
    }
    
    /// <summary>
    /// 附加加载场景
    /// </summary>
    public async void LoadSceneAdditive(string sceneName)
    {
        await _loader.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    }
    
    private void ShowLoadingScreen() { /* ... */ }
    private void HideLoadingScreen() { /* ... */ }
}
```

### 3.2 场景控制器模板

```csharp
/// <summary>
/// 场景控制器基类
/// 每个场景有一个继承此类的控制器
/// </summary>
public abstract class SceneController : MonoBehaviour
{
    protected EventBus EventBus { get; private set; }
    
    protected virtual void Awake()
    {
        EventBus = ServiceLocator.Get<EventBus>();
    }
    
    protected virtual void Start()
    {
        OnSceneEnter();
    }
    
    protected virtual void OnDestroy()
    {
        OnSceneExit();
        EventBus?.UnsubscribeAll(this);
    }
    
    /// <summary>
    /// 场景进入时调用
    /// </summary>
    protected abstract void OnSceneEnter();
    
    /// <summary>
    /// 场景退出时调用
    /// </summary>
    protected virtual void OnSceneExit() { }
}

/// <summary>
/// 战斗场景控制器
/// </summary>
public class BattleSceneController : SceneController
{
    [SerializeField] private Transform _spawnPoint;
    
    private IGameplayWorld _gameplayWorld;
    
    protected override void OnSceneEnter()
    {
        CYLog.Info("进入战斗场景");
        
        // 创建玩法世界
#if CY_WECHAT || UNITY_WEBGL
        _gameplayWorld = new OOPGameplayWorld();
#else
        _gameplayWorld = new HybridGameplayWorld();
#endif
        
        if (_gameplayWorld is IInitializable init)
            init.Initialize();
        
        // 生成玩家
        SpawnPlayer();
    }
    
    protected override void OnSceneExit()
    {
        if (_gameplayWorld is IDisposableEx disposable)
            disposable.Dispose();
    }
    
    private void SpawnPlayer()
    {
        var loader = ServiceLocator.Get<IResourceLoader>();
        var prefab = loader.Load<GameObject>("Prefabs/Player");
        Instantiate(prefab, _spawnPoint.position, Quaternion.identity);
    }
    
    void FixedUpdate()
    {
        _gameplayWorld?.FixedTick(Time.fixedDeltaTime);
    }
}
```

---

## 4. UI 开发模式

### 4.1 UI 管理器

```csharp
/// <summary>
/// UI 管理器
/// 管理所有 UI 面板的打开/关闭
/// </summary>
public class UIManager
{
    private static UIManager _instance;
    public static UIManager Instance => _instance ??= new UIManager();
    
    private readonly Dictionary<string, UIPanel> _panels = new();
    private readonly Stack<UIPanel> _panelStack = new();
    private readonly IResourceLoader _loader;
    private Transform _uiRoot;
    
    private UIManager()
    {
        _loader = ServiceLocator.Get<IResourceLoader>();
        
        // 创建 UI 根节点
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            _uiRoot = canvas.transform;
        }
    }
    
    /// <summary>
    /// 打开面板
    /// </summary>
    public T Open<T>(object data = null) where T : UIPanel
    {
        string panelName = typeof(T).Name;
        
        if (!_panels.TryGetValue(panelName, out var panel))
        {
            // 加载面板
            var prefab = _loader.Load<GameObject>($"UI/{panelName}");
            var go = Object.Instantiate(prefab, _uiRoot);
            panel = go.GetComponent<UIPanel>();
            _panels[panelName] = panel;
        }
        
        panel.gameObject.SetActive(true);
        panel.OnOpen(data);
        _panelStack.Push(panel);
        
        return panel as T;
    }
    
    /// <summary>
    /// 关闭面板
    /// </summary>
    public void Close<T>() where T : UIPanel
    {
        string panelName = typeof(T).Name;
        
        if (_panels.TryGetValue(panelName, out var panel))
        {
            panel.OnClose();
            panel.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 关闭顶层面板
    /// </summary>
    public void CloseTop()
    {
        if (_panelStack.Count > 0)
        {
            var panel = _panelStack.Pop();
            panel.OnClose();
            panel.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 关闭所有面板
    /// </summary>
    public void CloseAll()
    {
        foreach (var panel in _panels.Values)
        {
            panel.OnClose();
            panel.gameObject.SetActive(false);
        }
        _panelStack.Clear();
    }
}

/// <summary>
/// UI 面板基类
/// </summary>
public abstract class UIPanel : MonoBehaviour
{
    protected EventBus EventBus { get; private set; }
    
    protected virtual void Awake()
    {
        EventBus = ServiceLocator.Get<EventBus>();
    }
    
    /// <summary>
    /// 面板打开时调用
    /// </summary>
    public virtual void OnOpen(object data) { }
    
    /// <summary>
    /// 面板关闭时调用
    /// </summary>
    public virtual void OnClose() { }
    
    /// <summary>
    /// 刷新面板
    /// </summary>
    public virtual void Refresh() { }
    
    protected virtual void OnDestroy()
    {
        EventBus?.UnsubscribeAll(this);
    }
}
```

### 4.2 UI 面板示例

```csharp
/// <summary>
/// 主菜单面板
/// </summary>
public class MainMenuPanel : UIPanel
{
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _quitButton;
    
    protected override void Awake()
    {
        base.Awake();
        
        _startButton.onClick.AddListener(OnStartClick);
        _settingsButton.onClick.AddListener(OnSettingsClick);
        _quitButton.onClick.AddListener(OnQuitClick);
    }
    
    public override void OnOpen(object data)
    {
        // 播放 BGM
        var audio = ServiceLocator.Get<IAudioService>();
        audio.PlayBGM("bgm_menu");
    }
    
    void OnStartClick()
    {
        ServiceLocator.Get<IAudioService>().PlaySFX("sfx_click");
        
        // 关闭当前面板
        UIManager.Instance.Close<MainMenuPanel>();
        
        // 加载游戏场景
        new SceneManager().LoadScene("Level1");
    }
    
    void OnSettingsClick()
    {
        ServiceLocator.Get<IAudioService>().PlaySFX("sfx_click");
        UIManager.Instance.Open<SettingsPanel>();
    }
    
    void OnQuitClick()
    {
        Application.Quit();
    }
}

/// <summary>
/// 设置面板
/// </summary>
public class SettingsPanel : UIPanel
{
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _bgmVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private Toggle _muteToggle;
    [SerializeField] private Button _closeButton;
    
    private IAudioService _audio;
    private SaveService _saveService;
    
    protected override void Awake()
    {
        base.Awake();
        
        _audio = ServiceLocator.Get<IAudioService>();
        _saveService = ServiceLocator.Get<SaveService>();
        
        _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        _bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        _sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        _muteToggle.onValueChanged.AddListener(OnMuteChanged);
        _closeButton.onClick.AddListener(OnCloseClick);
    }
    
    public override void OnOpen(object data)
    {
        // 加载设置
        var settings = _saveService.Load<SettingsData>("settings", new SettingsData());
        
        _masterVolumeSlider.value = settings.masterVolume;
        _bgmVolumeSlider.value = settings.bgmVolume;
        _sfxVolumeSlider.value = settings.sfxVolume;
        _muteToggle.isOn = settings.isMuted;
    }
    
    public override void OnClose()
    {
        // 保存设置
        var settings = new SettingsData
        {
            masterVolume = _masterVolumeSlider.value,
            bgmVolume = _bgmVolumeSlider.value,
            sfxVolume = _sfxVolumeSlider.value,
            isMuted = _muteToggle.isOn
        };
        
        _saveService.Save("settings", settings);
    }
    
    void OnMasterVolumeChanged(float value) => _audio.SetMasterVolume(value);
    void OnBGMVolumeChanged(float value) => _audio.SetBGMVolume(value);
    void OnSFXVolumeChanged(float value) => _audio.SetSFXVolume(value);
    void OnMuteChanged(bool muted) => _audio.Mute(muted);
    void OnCloseClick() => UIManager.Instance.Close<SettingsPanel>();
}

[Serializable]
public class SettingsData
{
    public float masterVolume = 1f;
    public float bgmVolume = 0.8f;
    public float sfxVolume = 1f;
    public bool isMuted = false;
}
```

---

## 5. 网络通信

### 5.1 HTTP 请求封装

```csharp
/// <summary>
/// API 客户端
/// 封装所有服务器请求
/// </summary>
public class ApiClient
{
    private readonly NetworkService _network;
    private readonly string _baseUrl = "https://api.game.com";
    private string _token;
    
    public ApiClient()
    {
        _network = ServiceLocator.Get<NetworkService>();
    }
    
    /// <summary>
    /// 设置认证 Token
    /// </summary>
    public void SetToken(string token)
    {
        _token = token;
    }
    
    /// <summary>
    /// 登录
    /// </summary>
    public async Task<LoginResponse> Login(string username, string password)
    {
        var request = new LoginRequest { username = username, password = password };
        var response = await _network.PostAsync<LoginResponse>($"{_baseUrl}/auth/login", request);
        
        if (response != null && !string.IsNullOrEmpty(response.token))
        {
            SetToken(response.token);
        }
        
        return response;
    }
    
    /// <summary>
    /// 获取玩家数据
    /// </summary>
    public async Task<PlayerData> GetPlayerData()
    {
        var headers = GetAuthHeaders();
        return await _network.GetAsync<PlayerData>($"{_baseUrl}/player", headers);
    }
    
    /// <summary>
    /// 保存玩家进度
    /// </summary>
    public async Task<bool> SaveProgress(PlayerProgress progress)
    {
        var headers = GetAuthHeaders();
        var response = await _network.PostAsync<ApiResponse>(
            $"{_baseUrl}/player/progress", 
            progress, 
            headers
        );
        return response?.success ?? false;
    }
    
    private Dictionary<string, string> GetAuthHeaders()
    {
        return new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_token}" }
        };
    }
}

// 数据类
[Serializable]
public class LoginRequest
{
    public string username;
    public string password;
}

[Serializable]
public class LoginResponse
{
    public bool success;
    public string token;
    public string message;
}

[Serializable]
public class PlayerData
{
    public int id;
    public string name;
    public int level;
    public int gold;
}

[Serializable]
public class ApiResponse
{
    public bool success;
    public string message;
}
```

### 5.2 WebSocket 实时通信

```csharp
/// <summary>
/// 游戏同步客户端
/// </summary>
public class GameSyncClient
{
    private readonly NetworkService _network;
    private readonly EventBus _eventBus;
    
    public GameSyncClient()
    {
        _network = ServiceLocator.Get<NetworkService>();
        _eventBus = ServiceLocator.Get<EventBus>();
        
        // 注册消息处理
        _network.OnWebSocketMessage += OnMessage;
        _network.OnWebSocketDisconnected += OnDisconnected;
    }
    
    /// <summary>
    /// 连接服务器
    /// </summary>
    public async Task Connect(string roomId)
    {
        string url = $"wss://game.server.com/room/{roomId}";
        await _network.ConnectWebSocket(url);
        
        CYLog.Info($"已连接到房间: {roomId}");
    }
    
    /// <summary>
    /// 发送移动命令
    /// </summary>
    public void SendMove(Vector3 position, Vector3 direction)
    {
        var cmd = new MoveMessage
        {
            type = "move",
            x = position.x,
            y = position.y,
            z = position.z,
            dirX = direction.x,
            dirZ = direction.z
        };
        
        _network.SendWebSocketMessage(JsonUtility.ToJson(cmd));
    }
    
    /// <summary>
    /// 发送攻击命令
    /// </summary>
    public void SendAttack(int targetId, int skillId)
    {
        var cmd = new AttackMessage
        {
            type = "attack",
            targetId = targetId,
            skillId = skillId
        };
        
        _network.SendWebSocketMessage(JsonUtility.ToJson(cmd));
    }
    
    private void OnMessage(string message)
    {
        // 解析消息类型
        var baseMsg = JsonUtility.FromJson<BaseMessage>(message);
        
        switch (baseMsg.type)
        {
            case "sync":
                var syncData = JsonUtility.FromJson<SyncMessage>(message);
                var syncEvt = new ServerSyncEvent { Data = syncData };
                _eventBus.Post(ref syncEvt);
                break;
                
            case "player_join":
                var joinData = JsonUtility.FromJson<PlayerJoinMessage>(message);
                var joinEvt = new PlayerJoinEvent { PlayerId = joinData.playerId };
                _eventBus.Post(ref joinEvt);
                break;
                
            case "player_leave":
                var leaveData = JsonUtility.FromJson<PlayerLeaveMessage>(message);
                var leaveEvt = new PlayerLeaveEvent { PlayerId = leaveData.playerId };
                _eventBus.Post(ref leaveEvt);
                break;
        }
    }
    
    private void OnDisconnected()
    {
        CYLog.Warning("与服务器断开连接");
        
        // 发布断线事件
        var evt = new DisconnectedEvent();
        _eventBus.Post(ref evt);
    }
    
    public void Disconnect()
    {
        _network.DisconnectWebSocket();
    }
}

// 消息定义
[Serializable] public class BaseMessage { public string type; }
[Serializable] public class MoveMessage : BaseMessage { public float x, y, z, dirX, dirZ; }
[Serializable] public class AttackMessage : BaseMessage { public int targetId, skillId; }
[Serializable] public class SyncMessage : BaseMessage { public PlayerState[] players; }
[Serializable] public class PlayerJoinMessage : BaseMessage { public int playerId; }
[Serializable] public class PlayerLeaveMessage : BaseMessage { public int playerId; }

// 事件定义
public struct ServerSyncEvent { public SyncMessage Data; }
public struct PlayerJoinEvent { public int PlayerId; }
public struct PlayerLeaveEvent { public int PlayerId; }
public struct DisconnectedEvent { }
```

---

## 6. 存档系统

### 6.1 存档数据设计

```csharp
/// <summary>
/// 玩家存档数据
/// 使用 [Serializable] 以支持 JSON 序列化
/// </summary>
[Serializable]
public class PlayerSaveData
{
    // 版本号（用于数据迁移）
    public int version = 1;
    
    // 基础信息
    public string playerName;
    public int level;
    public int exp;
    
    // 货币
    public int gold;
    public int gems;
    
    // 属性
    public int hp;
    public int maxHp;
    public int attack;
    public int defense;
    
    // 装备
    public List<int> equippedItems = new();
    
    // 背包
    public List<InventoryItem> inventory = new();
    
    // 已解锁技能
    public List<int> unlockedSkills = new();
    
    // 进度
    public int currentChapter;
    public int currentLevel;
    public List<int> completedLevels = new();
    
    // 统计
    public int totalPlayTime; // 秒
    public int totalKills;
    public int totalDeaths;
    
    // 时间戳
    public long lastSaveTime;
}

[Serializable]
public class InventoryItem
{
    public int itemId;
    public int count;
}
```

### 6.2 存档管理器

```csharp
/// <summary>
/// 存档管理器
/// 封装存档操作
/// </summary>
public class SaveManager
{
    private static SaveManager _instance;
    public static SaveManager Instance => _instance ??= new SaveManager();
    
    private readonly SaveService _saveService;
    private PlayerSaveData _currentData;
    
    private const string SAVE_KEY = "player_save";
    private const string AUTO_SAVE_KEY = "player_autosave";
    
    public PlayerSaveData Data => _currentData;
    
    private SaveManager()
    {
        _saveService = ServiceLocator.Get<SaveService>();
    }
    
    /// <summary>
    /// 加载存档
    /// </summary>
    public bool Load()
    {
        if (_saveService.Exists(SAVE_KEY))
        {
            _currentData = _saveService.Load<PlayerSaveData>(SAVE_KEY);
            CYLog.Info($"存档加载成功，等级: {_currentData.level}");
            return true;
        }
        
        CYLog.Info("没有找到存档，创建新存档");
        _currentData = CreateNewSave();
        return false;
    }
    
    /// <summary>
    /// 保存存档
    /// </summary>
    public async Task Save()
    {
        _currentData.lastSaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        await _saveService.SaveAsync(SAVE_KEY, _currentData);
        CYLog.Info("存档保存成功");
    }
    
    /// <summary>
    /// 自动保存
    /// </summary>
    public async Task AutoSave()
    {
        _currentData.lastSaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        await _saveService.SaveAsync(AUTO_SAVE_KEY, _currentData);
        CYLog.Debug("自动保存完成");
    }
    
    /// <summary>
    /// 从自动存档恢复
    /// </summary>
    public bool LoadAutoSave()
    {
        if (_saveService.Exists(AUTO_SAVE_KEY))
        {
            _currentData = _saveService.Load<PlayerSaveData>(AUTO_SAVE_KEY);
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// 删除存档
    /// </summary>
    public void Delete()
    {
        _saveService.Delete(SAVE_KEY);
        _saveService.Delete(AUTO_SAVE_KEY);
        _currentData = null;
    }
    
    /// <summary>
    /// 检查是否有存档
    /// </summary>
    public bool HasSave()
    {
        return _saveService.Exists(SAVE_KEY);
    }
    
    /// <summary>
    /// 创建新存档
    /// </summary>
    private PlayerSaveData CreateNewSave()
    {
        return new PlayerSaveData
        {
            version = 1,
            playerName = "Player",
            level = 1,
            exp = 0,
            gold = 100,
            gems = 0,
            hp = 100,
            maxHp = 100,
            attack = 10,
            defense = 5,
            currentChapter = 1,
            currentLevel = 1,
            lastSaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
}
```

### 6.3 使用示例

```csharp
// 游戏开始时加载
void Start()
{
    SaveManager.Instance.Load();
    
    // 显示玩家数据
    var data = SaveManager.Instance.Data;
    playerNameText.text = data.playerName;
    levelText.text = $"Lv.{data.level}";
    goldText.text = data.gold.ToString();
}

// 获得金币时
void OnGoldCollected(int amount)
{
    SaveManager.Instance.Data.gold += amount;
    
    // 自动保存
    SaveManager.Instance.AutoSave();
}

// 关卡完成时
async void OnLevelComplete(int levelId)
{
    var data = SaveManager.Instance.Data;
    
    if (!data.completedLevels.Contains(levelId))
    {
        data.completedLevels.Add(levelId);
    }
    
    data.exp += 100;
    
    // 检查升级
    CheckLevelUp();
    
    // 保存
    await SaveManager.Instance.Save();
}

// 退出游戏时
void OnApplicationQuit()
{
    SaveManager.Instance.Save();
}
```

---

## 7. 音频管理

### 7.1 音频管理器

```csharp
/// <summary>
/// 音频管理器
/// 封装音频服务，提供更方便的 API
/// </summary>
public class AudioManager
{
    private static AudioManager _instance;
    public static AudioManager Instance => _instance ??= new AudioManager();
    
    private readonly IAudioService _audio;
    
    // 音频配置
    private readonly Dictionary<string, string> _bgmMap = new()
    {
        { "menu", "bgm_menu" },
        { "battle", "bgm_battle" },
        { "boss", "bgm_boss" },
        { "victory", "bgm_victory" },
        { "defeat", "bgm_defeat" }
    };
    
    private readonly Dictionary<string, string> _sfxMap = new()
    {
        { "click", "sfx_click" },
        { "coin", "sfx_coin" },
        { "hit", "sfx_hit" },
        { "explosion", "sfx_explosion" },
        { "levelup", "sfx_levelup" }
    };
    
    private AudioManager()
    {
        _audio = ServiceLocator.Get<IAudioService>();
    }
    
    /// <summary>
    /// 播放 BGM（使用别名）
    /// </summary>
    public void PlayBGM(string alias)
    {
        if (_bgmMap.TryGetValue(alias, out var name))
        {
            _audio.PlayBGM(name);
        }
        else
        {
            _audio.PlayBGM(alias);
        }
    }
    
    /// <summary>
    /// 停止 BGM
    /// </summary>
    public void StopBGM(float fadeOut = 0.5f)
    {
        _audio.StopBGM(fadeOut);
    }
    
    /// <summary>
    /// 播放音效（使用别名）
    /// </summary>
    public void PlaySFX(string alias, float volume = 1f)
    {
        if (_sfxMap.TryGetValue(alias, out var name))
        {
            _audio.PlaySFX(name, volume);
        }
        else
        {
            _audio.PlaySFX(alias, volume);
        }
    }
    
    /// <summary>
    /// 播放 UI 点击音效
    /// </summary>
    public void PlayClick()
    {
        PlaySFX("click", 0.5f);
    }
}
```

### 7.2 使用示例

```csharp
// 进入场景
void OnSceneEnter()
{
    AudioManager.Instance.PlayBGM("battle");
}

// UI 按钮点击
void OnButtonClick()
{
    AudioManager.Instance.PlayClick();
}

// 拾取金币
void OnCoinCollected()
{
    AudioManager.Instance.PlaySFX("coin");
}

// Boss 战
void OnBossBattle()
{
    AudioManager.Instance.StopBGM(1f);
    AudioManager.Instance.PlayBGM("boss");
}
```

---

## 8. 对象池优化

### 8.1 子弹池示例

```csharp
public class BulletPool : MonoBehaviour
{
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private int _prewarmCount = 50;
    
    private PoolManager _poolManager;
    
    void Start()
    {
        _poolManager = ServiceLocator.Get<PoolManager>();
        
        // 注册子弹池
        _poolManager.RegisterPrefabPool(
            _bulletPrefab,
            prewarm: _prewarmCount,
            maxSize: 200
        );
    }
    
    /// <summary>
    /// 发射子弹
    /// </summary>
    public void Fire(Vector3 position, Vector3 direction, float speed)
    {
        var bullet = _poolManager.SpawnPrefab(
            _bulletPrefab, 
            position, 
            Quaternion.LookRotation(direction)
        );
        
        var bulletScript = bullet.GetComponent<Bullet>();
        bulletScript.Initialize(direction, speed);
    }
}

/// <summary>
/// 子弹脚本
/// </summary>
public class Bullet : MonoBehaviour, IPoolable
{
    private Vector3 _direction;
    private float _speed;
    private float _lifeTime;
    private PoolManager _poolManager;
    
    public void Initialize(Vector3 direction, float speed)
    {
        _direction = direction;
        _speed = speed;
        _lifeTime = 0f;
    }
    
    public void OnSpawn()
    {
        _poolManager = ServiceLocator.Get<PoolManager>();
        _lifeTime = 0f;
    }
    
    public void OnDespawn()
    {
        _direction = Vector3.zero;
        _speed = 0f;
    }
    
    void Update()
    {
        // 移动
        transform.position += _direction * _speed * Time.deltaTime;
        
        // 生命周期
        _lifeTime += Time.deltaTime;
        if (_lifeTime > 5f)
        {
            _poolManager.DespawnPrefab(gameObject);
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            // 造成伤害
            other.GetComponent<Enemy>()?.TakeDamage(10);
            
            // 回收
            _poolManager.DespawnPrefab(gameObject);
        }
    }
}
```

### 8.2 特效池

```csharp
public class VFXPool : MonoBehaviour
{
    [System.Serializable]
    public class VFXEntry
    {
        public string name;
        public GameObject prefab;
        public int prewarm;
    }
    
    [SerializeField] private VFXEntry[] _vfxEntries;
    
    private PoolManager _poolManager;
    private Dictionary<string, GameObject> _prefabMap = new();
    
    void Start()
    {
        _poolManager = ServiceLocator.Get<PoolManager>();
        
        foreach (var entry in _vfxEntries)
        {
            _poolManager.RegisterPrefabPool(entry.prefab, entry.prewarm, 50);
            _prefabMap[entry.name] = entry.prefab;
        }
    }
    
    /// <summary>
    /// 播放特效
    /// </summary>
    public void Play(string name, Vector3 position)
    {
        if (!_prefabMap.TryGetValue(name, out var prefab)) return;
        
        var vfx = _poolManager.SpawnPrefab(prefab, position, Quaternion.identity);
        
        // 自动回收
        StartCoroutine(AutoDespawn(vfx, 2f));
    }
    
    private System.Collections.IEnumerator AutoDespawn(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);
        _poolManager.DespawnPrefab(vfx);
    }
}
```

---

## 9. 玩法开发

### 9.1 单位管理

```csharp
using CYFramework.Gameplay.OOP;

/// <summary>
/// 战斗管理器
/// </summary>
public class BattleManager : MonoBehaviour
{
    private OOPGameplayWorld _world;
    private RenderProxy _renderProxy;
    
    // 渲染对象映射
    private Dictionary<int, UnitRenderer> _renderers = new();
    
    void Start()
    {
        // 创建玩法世界
        _world = new OOPGameplayWorld();
        _world.Initialize();
        
        // 创建渲染代理
        _renderProxy = new RenderProxy(_world);
        
        // 生成初始单位
        SpawnPlayer();
        SpawnEnemies(10);
    }
    
    void FixedUpdate()
    {
        // 逻辑更新
        _world.FixedTick(Time.fixedDeltaTime);
    }
    
    void Update()
    {
        // 收集输入
        CollectInput();
        
        // 渲染更新
        UpdateRenderers();
    }
    
    private void CollectInput()
    {
        // 移动输入
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        
        if (h != 0 || v != 0)
        {
            _world.HandleInput(new InputCommand
            {
                Type = InputType.Move,
                Direction = new Vector2(h, v),
                Timestamp = Time.time
            });
        }
        
        // 攻击输入
        if (Input.GetButtonDown("Fire1"))
        {
            _world.HandleInput(new InputCommand
            {
                Type = InputType.Attack,
                Timestamp = Time.time
            });
        }
    }
    
    private void UpdateRenderers()
    {
        ref readonly var snapshot = ref _world.GetRenderSnapshot();
        
        // 更新现有单位
        for (int i = 0; i < snapshot.Count; i++)
        {
            int id = snapshot.IDs[i];
            
            if (!_renderers.TryGetValue(id, out var renderer))
            {
                // 创建新渲染对象
                renderer = CreateRenderer(id);
                _renderers[id] = renderer;
            }
            
            // 更新位置
            renderer.UpdatePosition(snapshot.Positions[i], snapshot.Rotations[i]);
            renderer.UpdateHP(snapshot.HPs[i]);
        }
        
        // 清理已销毁的单位
        CleanupDeadUnits(snapshot);
    }
    
    private UnitRenderer CreateRenderer(int id)
    {
        var loader = ServiceLocator.Get<IResourceLoader>();
        var prefab = loader.Load<GameObject>("Prefabs/Unit");
        var go = Instantiate(prefab);
        return go.GetComponent<UnitRenderer>();
    }
    
    private void CleanupDeadUnits(in RenderSnapshot snapshot)
    {
        var toRemove = new List<int>();
        
        foreach (var kvp in _renderers)
        {
            bool found = false;
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot.IDs[i] == kvp.Key)
                {
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (int id in toRemove)
        {
            Destroy(_renderers[id].gameObject);
            _renderers.Remove(id);
        }
    }
    
    private void SpawnPlayer() { /* ... */ }
    private void SpawnEnemies(int count) { /* ... */ }
    
    void OnDestroy()
    {
        _world?.Dispose();
    }
}
```

---

## 10. 调试与测试

### 10.1 使用 RuntimeProfiler

按 `F1` 打开性能面板，显示：
- FPS / 帧时间
- 内存使用
- DrawCall
- 对象池状态
- 网络延迟

### 10.2 使用 CheatConsole

按 `` ` `` 打开控制台，输入命令：

```bash
# 设置时间缩放（慢动作调试）
timescale 0.5

# 显示 FPS
fps

# 强制 GC
gc

# 自定义命令
god     # 无敌模式
gold 9999   # 设置金币
level 10    # 设置等级
```

### 10.3 单元测试

```csharp
[TestFixture]
public class MyGameTests
{
    [Test]
    public void Player_TakeDamage_ReducesHP()
    {
        // Arrange
        var player = new PlayerData { hp = 100 };
        
        // Act
        player.hp -= 30;
        
        // Assert
        Assert.AreEqual(70, player.hp);
    }
    
    [Test]
    public void SaveManager_SaveAndLoad_PreservesData()
    {
        // Arrange
        var saveService = new SaveService();
        saveService.Initialize();
        
        var data = new PlayerSaveData { level = 10, gold = 5000 };
        
        // Act
        saveService.Save("test", data);
        var loaded = saveService.Load<PlayerSaveData>("test");
        
        // Assert
        Assert.AreEqual(10, loaded.level);
        Assert.AreEqual(5000, loaded.gold);
        
        // Cleanup
        saveService.Delete("test");
    }
}
```

---

## 11. 发布构建

### 11.1 构建前检查

1. **运行平台兼容性检查器**
   ```
   菜单: CYFramework > 平台兼容性检查
   ```

2. **设置正确的宏定义**
   - 微信: `CY_WECHAT;CY_SINGLE_THREAD`
   - PC: `CY_PC;ENABLE_DOTS`

3. **烘焙配置**
   ```
   菜单: CYFramework > 配置烘焙工具
   ```

### 11.2 微信小游戏构建

1. 安装微信小游戏 Unity 插件
2. 设置 `Player Settings > WebGL`
3. 添加宏定义 `CY_WECHAT`
4. 构建 WebGL
5. 使用微信开发者工具发布

### 11.3 PC 构建

1. 设置 `Player Settings > Standalone`
2. 添加宏定义 `CY_PC;ENABLE_DOTS`
3. 构建 Windows/Mac/Linux

---

## 附录：常用代码片段

### 获取服务

```csharp
var eventBus = ServiceLocator.Get<EventBus>();
var audio = ServiceLocator.Get<IAudioService>();
var save = ServiceLocator.Get<SaveService>();
var loader = ServiceLocator.Get<IResourceLoader>();
var network = ServiceLocator.Get<NetworkService>();
var pool = ServiceLocator.Get<PoolManager>();
```

### 事件模式

```csharp
// 定义
public struct MyEvent { public int Value; }

// 订阅
eventBus.Subscribe<MyEvent>(OnMyEvent, this);

// 发布
var evt = new MyEvent { Value = 42 };
eventBus.Post(ref evt);

// 取消
eventBus.UnsubscribeAll(this);
```

### 异步加载

```csharp
// 资源
var prefab = await loader.LoadAsync<GameObject>("Prefabs/Enemy");

// 场景
await loader.LoadSceneAsync("Level1");

// 存档
var data = await saveService.LoadAsync<PlayerData>("save");
```
