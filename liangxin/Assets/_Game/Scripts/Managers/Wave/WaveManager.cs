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
/// 波次管理器：准备阶段 + 刷怪阶段，支持手动推进波次。
/// </summary>
public sealed partial class WaveManager : MonoBehaviour, IInitializable, IUpdateable, IPausable, IDisposableEx // 波次管理器定义
{
    /// <summary>生成类型表名。</summary>
    private const string SpawnTypeTableName = "SpawnType"; // 生成类型表名
    /// <summary>奇袭生成类型表名。</summary>
    private const string AssaultSpawnTypeTableName = "AssaultSpawnType"; // 奇袭生成类型表名
    /// <summary>是否默认暂停（避免未开始游戏就刷怪）。</summary>
    [SerializeField] private bool _startPaused = true; // 初始暂停配置
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
    /// <summary>生成类型配置缓存（SpawnTypeId -> Row）。</summary>
    private readonly Dictionary<int, SpawnTypeRow> _spawnTypeMap = new Dictionary<int, SpawnTypeRow>(64); // 生成类型缓存
    /// <summary>生成类型敌人池缓存（SpawnTypeId -> Pool）。</summary>
    private readonly Dictionary<int, SpawnTypeEnemyPool> _spawnTypeEnemyPoolMap = new Dictionary<int, SpawnTypeEnemyPool>(64); // 敌人池缓存
    /// <summary>生成类型刷新点池缓存（SpawnTypeId -> Pool）。</summary>
    private readonly Dictionary<int, SpawnTypePointPool> _spawnTypePointPoolMap = new Dictionary<int, SpawnTypePointPool>(64); // 刷新点池缓存
    /// <summary>奇袭生成类型配置缓存（SpawnTypeId -> Row）。</summary>
    private readonly Dictionary<int, AssaultSpawnTypeRow> _assaultSpawnTypeMap = new Dictionary<int, AssaultSpawnTypeRow>(64); // 奇袭生成类型缓存
    /// <summary>奇袭生成类型敌人池缓存（SpawnTypeId -> Pool）。</summary>
    private readonly Dictionary<int, SpawnTypeEnemyPool> _assaultSpawnTypeEnemyPoolMap = new Dictionary<int, SpawnTypeEnemyPool>(64); // 奇袭敌人池缓存
    /// <summary>奇袭生成类型刷新点池缓存（SpawnTypeId -> Pool）。</summary>
    private readonly Dictionary<int, SpawnTypePointPool> _assaultSpawnTypePointPoolMap = new Dictionary<int, SpawnTypePointPool>(64); // 奇袭刷新点池缓存
    /// <summary>可用奇袭波次 Id 列表。</summary>
    private readonly List<int> _assaultWaveIdList = new List<int>(16); // 奇袭波次列表
    /// <summary>奇袭波次去重集合。</summary>
    private readonly HashSet<int> _assaultWaveIdSet = new HashSet<int>(); // 奇袭波次去重集合
    /// <summary>当前展示的波次 Id（用于 UI）。</summary>
    private int _currentWaveId; // 当前波次 Id
    /// <summary>最近一次启动的波次 Id（用于推进下一波）。</summary>
    private int _lastWaveId; // 最近启动的波次 Id
    /// <summary>活动波次运行时列表。</summary>
    private readonly List<WaveRuntime> _activeWaves = new List<WaveRuntime>(8); // 活动波次列表
    /// <summary>活动主线波次映射（WaveId -> Runtime）。</summary>
    private readonly Dictionary<int, WaveRuntime> _activeWaveMap = new Dictionary<int, WaveRuntime>(8); // 主线波次映射
    /// <summary>活动奇袭波次映射（WaveId -> Runtime）。</summary>
    private readonly Dictionary<int, WaveRuntime> _activeAssaultWaveMap = new Dictionary<int, WaveRuntime>(8); // 奇袭波次映射

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

        _paused = _startPaused; // 初始化暂停状态
        ServiceLocator.RegisterInstance(this); // 注册服务
        _registered = true; // 标记已注册
        EnsureAutoAdvanceManager(); // 确保自动推进管理器存在
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
    /// 确保自动推进管理器存在（避免遗漏挂载）。
    /// </summary>
    private void EnsureAutoAdvanceManager() // 自动推进管理器检查入口
    {
        if (GetComponent<WaveAutoAdvanceManager>() != null)
        {
            return; // 已存在组件时直接退出
        }

        gameObject.AddComponent<WaveAutoAdvanceManager>(); // 自动补齐组件
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
    /// 重置运行时波次状态（保留配置缓存）。
    /// </summary>
    public void ResetRuntime() // 运行时重置入口
    {
        _currentWaveId = 0; // 重置当前展示波次
        _lastWaveId = 0; // 重置最近波次
        _activeWaves.Clear(); // 清空活动波次列表
        _activeWaveMap.Clear(); // 清空主线波次映射
        _activeAssaultWaveMap.Clear(); // 清空奇袭波次映射
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
    /// 手动启动波次（由外部控制推进）。
    /// </summary>
    /// <param name="waveId">波次 Id。</param>
    public bool TryStartWave(int waveId) // 手动启动入口
    {
        if (waveId <= 0)
        {
            CY.LogWarning("[WaveManager] 波次 Id 无效，无法启动。"); // 输出无效 Id 警告
            return false; // 无效 Id 时失败
        }

        if (_activeWaveMap.ContainsKey(waveId))
        {
            return false; // 已在运行时直接退出
        }

        var runtime = new WaveRuntime(waveId, false); // 创建运行时波次
        BuildRuntimeSpawnTypes(runtime); // 构建运行时生成类型池
        runtime.NextRefreshTimer = 0f; // 刷新计时归零

        if (runtime.SpawnTypes.Count == 0)
        {
            CY.LogWarning($"[WaveManager] 波次未找到可用生成类型，WaveId={waveId}"); // 输出生成类型警告
        }

        _activeWaves.Add(runtime); // 添加到活动列表
        _activeWaveMap.Add(runtime.WaveId, runtime); // 添加到主线映射表
        _currentWaveId = waveId; // 记录当前展示波次
        _lastWaveId = waveId; // 记录最近波次

        PostPrepareStarted(runtime); // 派发准备阶段开始事件
        return true; // 启动成功
    }

    /// <summary>
    /// 手动启动奇袭波次（与主线并行）。
    /// </summary>
    /// <param name="waveId">奇袭波次 Id。</param>
    public bool TryStartAssaultWave(int waveId) // 奇袭波次启动入口
    {
        if (waveId <= 0)
        {
            CY.LogWarning("[WaveManager] 奇袭波次 Id 无效，无法启动。"); // 输出无效 Id 警告
            return false; // 无效 Id 时失败
        }

        if (_activeAssaultWaveMap.ContainsKey(waveId))
        {
            return false; // 已在运行时直接退出
        }

        var runtime = new WaveRuntime(waveId, true); // 创建奇袭运行时
        BuildRuntimeAssaultSpawnTypes(runtime, waveId); // 构建奇袭生成类型池
        runtime.NextRefreshTimer = 0f; // 刷新计时归零

        if (runtime.SpawnTypes.Count == 0)
        {
            CY.LogWarning($"[WaveManager] 奇袭波次未找到可用生成类型，WaveId={waveId}"); // 输出生成类型警告
        }

        _activeWaves.Add(runtime); // 添加到活动列表
        _activeAssaultWaveMap.Add(runtime.WaveId, runtime); // 添加到奇袭映射表

        PostPrepareStarted(runtime); // 派发准备阶段开始事件
        if (HasImmediateSpawn(runtime))
        {
            runtime.HasSpawnStarted = true; // 立即进入刷怪阶段
            PostSpawnStarted(runtime); // 立即派发刷怪开始事件
        }
        return true; // 启动成功
    }

    /// <summary>
    /// 随机启动一个奇袭波次（从表内可用波次中选择）。
    /// </summary>
    /// <param name="waveId">输出触发的奇袭波次 Id。</param>
    public bool TryStartRandomAssaultWave(out int waveId) // 随机奇袭入口
    {
        waveId = 0; // 默认输出
        if (_assaultWaveIdList.Count == 0)
        {
            CY.LogWarning("[WaveManager] 未找到可用奇袭波次。"); // 输出提示
            return false; // 没有奇袭波次时失败
        }

        var count = _assaultWaveIdList.Count; // 记录数量
        var startIndex = Random.Range(0, count); // 随机起点
        for (int i = 0; i < count; i++)
        {
            var id = _assaultWaveIdList[(startIndex + i) % count]; // 轮询波次 Id
            if (_activeAssaultWaveMap.ContainsKey(id))
            {
                continue; // 该奇袭已在运行时跳过
            }

            if (TryStartAssaultWave(id))
            {
                waveId = id; // 写回触发 Id
                return true; // 启动成功
            }
        }

        return false; // 所有波次都在运行时失败
    }

    /// <summary>
    /// 推进到下一波（当前为 0 时从 1 开始）。
    /// </summary>
    public bool TryAdvanceWave() // 波次推进入口
    {
        var nextWaveId = _lastWaveId <= 0 ? 1 : _lastWaveId + 1; // 计算下一波 Id
        return TryStartWave(nextWaveId); // 启动下一波
    }


    /// <summary>
    /// 获取当前波次状态（用于 UI 显示）。
    /// </summary>
    /// <param name="waveId">输出波次 Id。</param>
    /// <param name="stage">输出阶段。</param>
    /// <param name="remainingSeconds">输出剩余时间（秒）。</param>
    public bool TryGetMainWaveStatus(out int waveId, out WaveStage stage, out float remainingSeconds) // 当前波次状态查询
    {
        waveId = _currentWaveId; // 输出当前展示波次 Id
        stage = WaveStage.None; // 默认阶段
        remainingSeconds = 0f; // 默认剩余时间

        if (waveId <= 0)
        {
            return false; // 没有活动波次时返回失败
        }

        if (!_activeWaveMap.TryGetValue(waveId, out var runtime) || runtime == null)
        {
            return false; // 运行时不存在时失败
        }

        if (runtime.SpawnTypeRuntimes.Count == 0)
        {
            return false; // 无生成类型时失败
        }

        if (!runtime.HasSpawnStarted)
        {
            stage = WaveStage.Prepare; // 处于准备阶段
            remainingSeconds = GetPrepareRemaining(runtime); // 计算准备剩余
            return true; // 返回成功
        }

        stage = WaveStage.Spawn; // 处于刷怪阶段
        remainingSeconds = GetWaveRemaining(runtime); // 计算波次剩余
        return true; // 返回成功
    }

    /// <summary>
    /// 获取准备阶段剩余时间（取最小值）。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    private float GetPrepareRemaining(WaveRuntime runtime) // 准备剩余时间入口
    {
        var min = float.MaxValue; // 最小剩余时间
        for (int i = 0; i < runtime.SpawnTypeRuntimes.Count; i++)
        {
            var spawnRuntime = runtime.SpawnTypeRuntimes[i]; // 取出运行时
            if (spawnRuntime == null)
            {
                continue; // 跳过空对象
            }

            if (spawnRuntime.SpawnRemaining <= 0f)
            {
                continue; // 已结束则跳过
            }

            if (spawnRuntime.PrepareRemaining > 0f && spawnRuntime.PrepareRemaining < min)
            {
                min = spawnRuntime.PrepareRemaining; // 记录更小的准备剩余
            }
        }

        return min == float.MaxValue ? 0f : min; // 返回结果
    }

    /// <summary>
    /// 获取波次剩余时间（取最大值）。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    private float GetWaveRemaining(WaveRuntime runtime) // 波次剩余时间入口
    {
        var max = 0f; // 最大剩余时间
        for (int i = 0; i < runtime.SpawnTypeRuntimes.Count; i++)
        {
            var spawnRuntime = runtime.SpawnTypeRuntimes[i]; // 取出运行时
            if (spawnRuntime == null)
            {
                continue; // 跳过空对象
            }

            if (spawnRuntime.SpawnRemaining <= 0f && spawnRuntime.PrepareRemaining <= 0f)
            {
                continue; // 已结束则跳过
            }

            var remaining = spawnRuntime.PrepareRemaining + spawnRuntime.SpawnRemaining; // 计算剩余时间
            if (remaining > max)
            {
                max = remaining; // 记录最大剩余
            }
        }

        return max; // 返回结果
    }

}
