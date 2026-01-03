// 引用泛型集合命名空间，使用 Dictionary/List/HashSet
using System.Collections.Generic; // 集合类型引用
// 引用 CYFramework 入口，使用 CY.Event/CY.Data/CY.Unit
using CYFramework; // 框架统一入口
// 引用数据表命名空间，使用 DataTable
using CYFramework.Core.DataTable; // 数据表类型引用
// 引用生命周期命名空间，使用 IInitializable/IUpdateable/IPausable/IDisposableEx
using CYFramework.Infrastructure; // 生命周期接口引用
// 引用 UnityEngine，使用 MonoBehaviour/SerializeField
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 波次管理器：准备阶段 + 刷怪阶段，支持时间/清空/手动触发与突袭并发。
/// </summary>
public sealed partial class WaveManager : MonoBehaviour, IInitializable, IUpdateable, IPausable, IDisposableEx // 波次管理器定义
{
    /// <summary>波次表名。</summary>
    private const string WaveTableName = "Wave"; // 波次表名
    /// <summary>波次生成类型池表名。</summary>
    private const string WaveSpawnPoolTableName = "WaveSpawnPool"; // 生成类型池表名
    /// <summary>生成类型表名。</summary>
    private const string SpawnTypeTableName = "SpawnType"; // 生成类型表名
    /// <summary>敌人池表名。</summary>
    private const string EnemyPoolTableName = "EnemyPool"; // 敌人池表名
    /// <summary>刷新类型表名。</summary>
    private const string RefreshTypeTableName = "RefreshType"; // 刷新类型表名

    /// <summary>是否在切场景时保留该对象。</summary>
    [SerializeField] private bool _dontDestroyOnLoad = true; // 切场景保留配置
    /// <summary>单帧允许的最大刷新次数（防止极小间隔导致卡死）。</summary>
    [SerializeField] private int _maxRefreshPerUpdate = 8; // 单帧刷新次数上限
    /// <summary>刷新间隔最小兜底值（秒）。</summary>
    [SerializeField] private float _minRefreshInterval = 0.05f; // 最小刷新间隔

    /// <summary>是否已注册到 ServiceLocator。</summary>
    private bool _registered; // 注册标记
    /// <summary>是否已初始化。</summary>
    private bool _initialized; // 初始化标记
    /// <summary>是否已释放。</summary>
    private bool _disposed; // 释放标记
    /// <summary>是否暂停。</summary>
    private bool _paused; // 暂停标记
    /// <summary>是否需要重试加载。</summary>
    private bool _needRetryLoad; // 重试加载标记
    /// <summary>全局运行时间（秒）。</summary>
    private float _elapsedTime; // 运行时间

    /// <summary>波次配置缓存（WaveId -> WaveRow）。</summary>
    private readonly Dictionary<int, WaveRow> _waveMap = new Dictionary<int, WaveRow>(32); // 波次配置缓存
    /// <summary>波次生成类型池（WaveId -> 列表）。</summary>
    private readonly Dictionary<int, List<WeightedId>> _waveSpawnPoolMap = new Dictionary<int, List<WeightedId>>(32); // 生成类型池缓存
    /// <summary>生成类型配置缓存（SpawnTypeId -> Row）。</summary>
    private readonly Dictionary<int, SpawnTypeRow> _spawnTypeMap = new Dictionary<int, SpawnTypeRow>(64); // 生成类型缓存
    /// <summary>敌人池配置缓存（PoolId -> Group）。</summary>
    private readonly Dictionary<int, EnemyPoolGroup> _enemyPoolMap = new Dictionary<int, EnemyPoolGroup>(64); // 敌人池缓存
    /// <summary>刷新类型配置缓存（RefreshTypeId -> Row）。</summary>
    private readonly Dictionary<int, RefreshTypeRow> _refreshTypeMap = new Dictionary<int, RefreshTypeRow>(32); // 刷新类型缓存

    /// <summary>时间触发波次列表。</summary>
    private readonly List<WaveRow> _timeTriggeredWaves = new List<WaveRow>(16); // 时间触发波次
    /// <summary>清空触发波次映射（前置波次 Id -> 波次列表）。</summary>
    private readonly Dictionary<int, List<WaveRow>> _clearTriggeredWaves = new Dictionary<int, List<WaveRow>>(16); // 清空触发映射

    /// <summary>活动波次运行时列表。</summary>
    private readonly List<WaveRuntime> _activeWaves = new List<WaveRuntime>(16); // 活动波次列表
    /// <summary>活动波次运行时映射（WaveId -> Runtime）。</summary>
    private readonly Dictionary<int, WaveRuntime> _activeWaveMap = new Dictionary<int, WaveRuntime>(16); // 活动波次映射
    /// <summary>已完成波次集合。</summary>
    private readonly HashSet<int> _completedWaveIds = new HashSet<int>(); // 完成波次集合

    /// <summary>当前主线活动波次 Id（0 表示无）。</summary>
    private int _activeMainWaveId; // 当前主线波次 Id

    /// <summary>初始化顺序（数值小的先执行）。</summary>
    public int InitOrder => 140; // 初始化顺序
    /// <summary>更新顺序（数值小的先执行）。</summary>
    public int UpdateOrder => 350; // 更新顺序
    /// <summary>释放顺序（数值大的先释放）。</summary>
    public int DisposeOrder => -140; // 释放顺序
    /// <summary>是否暂停（只读）。</summary>
    public bool IsPaused => _paused; // 暂停状态

    /// <summary>
    /// Unity Awake：注册到 ServiceLocator。
    /// </summary>
    private void Awake() // 生命周期：Awake
    {
        if (ServiceLocator.TryGet<WaveManager>(out var existing) && existing != this)
        {
            Destroy(gameObject); // 场景重复挂载时销毁
            return; // 直接退出
        }

        if (_dontDestroyOnLoad && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject); // 设置常驻
        }

        ServiceLocator.RegisterInstance(this); // 注册服务
        _registered = true; // 标记已注册
    }

    /// <summary>
    /// Unity OnDestroy：注销服务并清理。
    /// </summary>
    private void OnDestroy() // 生命周期：OnDestroy
    {
        if (_registered)
        {
            Dispose(); // 释放资源
            ServiceLocator.Unregister<WaveManager>(); // 注销服务
            _registered = false; // 标记未注册
        }
    }

    /// <summary>
    /// 初始化（由 ServiceLocator 驱动，只会执行一次）。
    /// </summary>
    public void Initialize() // 初始化入口
    {
        _needRetryLoad = !TryBuildWaveCache(); // 构建波次缓存
        _initialized = true; // 标记已初始化
    }

    /// <summary>
    /// 每帧更新（Update）。
    /// </summary>
    /// <param name="deltaTime">帧间隔时间。</param>
    public void OnUpdate(float deltaTime) // Update 入口
    {
        if (!_initialized || _disposed)
        {
            return; // 未初始化或已释放时直接退出
        }

        if (_needRetryLoad)
        {
            _needRetryLoad = !TryBuildWaveCache(); // 延迟重试加载
        }

        if (_paused)
        {
            return; // 暂停时不推进
        }

        _elapsedTime += deltaTime; // 更新运行时间
        UpdateTimeTriggeredWaves(); // 更新时间触发波次
        UpdateActiveWaves(deltaTime); // 更新活动波次
    }

    /// <summary>
    /// 暂停回调（切后台）。
    /// </summary>
    public void OnPause() // 暂停入口
    {
        SetPaused(true); // 设置暂停
    }

    /// <summary>
    /// 恢复回调（切前台）。
    /// </summary>
    /// <param name="pauseDuration">暂停时长（秒）。</param>
    public void OnResume(float pauseDuration) // 恢复入口
    {
        SetPaused(false); // 解除暂停
    }

    /// <summary>
    /// 手动设置暂停状态（调试入口）。
    /// </summary>
    /// <param name="paused">是否暂停。</param>
    public void SetPaused(bool paused) // 暂停设置入口
    {
        if (_paused == paused)
        {
            return; // 状态不变时直接退出
        }

        _paused = paused; // 设置暂停状态
        PostPauseEvent(_paused); // 派发暂停事件
    }

    /// <summary>
    /// 释放清理。
    /// </summary>
    public void Dispose() // 释放入口
    {
        if (_disposed)
        {
            return; // 已释放时直接退出
        }

        _disposed = true; // 标记已释放
        ClearAll(); // 清理缓存
    }

    /// <summary>
    /// 手动触发波次（仅对 Manual 触发类型生效）。
    /// </summary>
    /// <param name="waveId">波次 Id。</param>
    /// <param name="force">是否强制触发（忽略触发类型）。</param>
    public bool TryStartWave(int waveId, bool force = false) // 手动触发入口
    {
        if (!_waveMap.TryGetValue(waveId, out var wave))
        {
            CY.LogWarning($"[WaveManager] 未找到波次配置，Id={waveId}"); // 输出未找到警告
            return false; // 找不到配置时失败
        }

        if (!force && wave.TriggerType != WaveTriggerType.Manual)
        {
            CY.LogWarning($"[WaveManager] 波次不是手动触发，Id={waveId}"); // 输出触发类型警告
            return false; // 触发类型不匹配时失败
        }

        return StartWaveInternal(wave); // 启动波次
    }

    /// <summary>
    /// 通知波次清空（触发 Clear 类型波次）。
    /// </summary>
    /// <param name="clearedWaveId">已清空的波次 Id。</param>
    public void NotifyWaveCleared(int clearedWaveId) // 清空触发入口
    {
        if (!_clearTriggeredWaves.TryGetValue(clearedWaveId, out var list) || list == null)
        {
            return; // 无对应清空触发时退出
        }

        for (int i = 0; i < list.Count; i++)
        {
            StartWaveInternal(list[i]); // 启动清空触发波次
        }
    }
}
