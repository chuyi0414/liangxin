// 引用基础命名空间，使用 StringComparer
using System; // 基础类型引用
// 引用泛型集合命名空间，使用 Dictionary/List/HashSet
using System.Collections.Generic; // 集合类型引用
// 引用 CYFramework 入口，使用 CY.Event/CY.Unit/CY.Log
using CYFramework; // 框架统一入口
// 引用生命周期命名空间，使用 IInitializable/IUpdateable/IPausable/IDisposableEx
using CYFramework.Infrastructure; // 生命周期接口引用
// 引用 UnityEngine，使用 MonoBehaviour/SerializeField/Mathf
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 波次管理器：基于波次编排系统的精确刷怪控制。
/// </summary>
public sealed partial class WaveManager : MonoBehaviour, IInitializable, IUpdateable, IPausable, IDisposableEx // 波次管理器定义
{
    /// <summary>波次计划表名。</summary>
    private const string WavePlanTableName = "WavePlan"; // 波次计划表名
    /// <summary>波次轨道表名。</summary>
    private const string WaveTrackTableName = "WaveTrack"; // 波次轨道表名
    /// <summary>刷怪组表名。</summary>
    private const string WaveSpawnGroupTableName = "WaveSpawnGroup"; // 刷怪组表名

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
    /// <summary>是否已订阅事件。</summary>
    private bool _subscribed; // 订阅标记

    /// <summary>波次计划缓存（WaveId -> Row）。</summary>
    private readonly Dictionary<int, WavePlanRow> _wavePlanMap = new Dictionary<int, WavePlanRow>(32); // 波次计划缓存
    /// <summary>波次轨道缓存（WaveId -> TrackRows）。</summary>
    private readonly Dictionary<int, List<WaveTrackRow>> _waveTrackMap = new Dictionary<int, List<WaveTrackRow>>(32); // 波次轨道缓存
    /// <summary>刷怪组配置缓存（GroupId -> Row）。</summary>
    private readonly Dictionary<int, WaveSpawnGroupRow> _spawnGroupMap = new Dictionary<int, WaveSpawnGroupRow>(64); // 刷怪组缓存
    /// <summary>刷怪组运行时缓存（GroupId -> Runtime）。</summary>
    private readonly Dictionary<int, SpawnGroupRuntime> _spawnGroupRuntimeMap = new Dictionary<int, SpawnGroupRuntime>(64); // 刷怪组运行时缓存

    /// <summary>当前波次运行时。</summary>
    private WaveRuntime _currentWave; // 当前波次运行时
    /// <summary>当前展示波次 Id（用于 UI）。</summary>
    private int _currentWaveId; // 当前波次 Id
    /// <summary>最近一次启动的波次 Id（用于推进下一波）。</summary>
    private int _lastWaveId; // 最近波次 Id
    /// <summary>已完成波次数（用于解锁随机波次）。</summary>
    private int _completedWaveCount; // 已完成波次数

    /// <summary>敌人实体到轨道映射（用于存活统计）。</summary>
    private readonly Dictionary<UnitEntity, TrackRuntime> _enemyTrackMap = new Dictionary<UnitEntity, TrackRuntime>(128); // 敌人轨道映射

    /// <summary>事件触发缓存集合。</summary>
    private readonly HashSet<string> _eventTriggerSet = new HashSet<string>(StringComparer.Ordinal); // 事件触发集合
    /// <summary>区域进入触发缓存集合。</summary>
    private readonly HashSet<string> _areaEnterSet = new HashSet<string>(StringComparer.Ordinal); // 区域进入集合
    /// <summary>区域离开触发缓存集合。</summary>
    private readonly HashSet<string> _areaExitSet = new HashSet<string>(StringComparer.Ordinal); // 区域离开集合

    /// <summary>初始化顺序（数值小的先执行）。</summary>
    public int InitOrder => 140; // 初始化顺序
    /// <summary>更新顺序（数值小的先执行）。</summary>
    public int UpdateOrder => 350; // 更新顺序
    /// <summary>释放顺序（数值大的先释放）。</summary>
    public int DisposeOrder => -140; // 释放顺序
    /// <summary>是否暂停（只读）。</summary>
    public bool IsPaused => _paused; // 暂停状态
    /// <summary>是否允许自动推进下一波（只读）。</summary>
    public bool AutoAdvanceEnabled => _currentWave != null && _currentWave.Plan != null && _currentWave.Plan.AutoAdvance != 0; // 自动推进开关

    /// <summary>
    /// Unity Awake：注册到 ServiceLocator。
    /// </summary>
    private void Awake() // 生命周期：Awake
    {
        if (ServiceLocator.TryGet<WaveManager>(out var existing) && existing != this) // 重复实例判定
        {
            Destroy(gameObject); // 场景重复挂载时销毁
            return; // 直接退出
        }

        if (_dontDestroyOnLoad && transform.parent == null) // 常驻判定
        {
            DontDestroyOnLoad(gameObject); // 设置常驻
        }

        _paused = _startPaused; // 初始化暂停状态
        ServiceLocator.RegisterInstance(this); // 注册服务
        _registered = true; // 标记已注册
        EnsureAutoAdvanceManager(); // 确保自动推进管理器存在
        EnsureSubscribed(); // 确保事件订阅
    }

    /// <summary>
    /// Unity OnDestroy：注销服务并清理。
    /// </summary>
    private void OnDestroy() // 生命周期：OnDestroy
    {
        if (_registered) // 注册判定
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
        if (GetComponent<WaveAutoAdvanceManager>() != null) // 组件存在判定
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
        EnsureSubscribed(); // 确保已订阅事件
        _initialized = true; // 标记已初始化
    }

    /// <summary>
    /// 每帧更新（Update）。
    /// </summary>
    /// <param name="deltaTime">帧间隔时间。</param>
    public void OnUpdate(float deltaTime) // Update 入口
    {
        if (!_initialized || _disposed) // 状态判定
        {
            return; // 未初始化或已释放时直接退出
        }

        if (_needRetryLoad) // 重试加载判定
        {
            _needRetryLoad = !TryBuildWaveCache(); // 延迟重试加载
        }

        if (_paused) // 暂停判定
        {
            return; // 暂停时不推进
        }

        UpdateActiveWave(deltaTime); // 更新当前波次
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
        if (_paused == paused) // 状态不变判定
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
        ResetRuntimeInternal(); // 执行内部清理
        _completedWaveCount = 0; // 重置已完成波次数
        _lastWaveId = 0; // 重置最近波次 Id
    }

    /// <summary>
    /// 释放清理。
    /// </summary>
    public void Dispose() // 释放入口
    {
        if (_disposed) // 已释放判定
        {
            return; // 已释放时直接退出
        }

        _disposed = true; // 标记已释放
        if (_subscribed) // 已订阅判定
        {
            CY.Event.UnsubscribeAll(this); // 取消事件订阅
            _subscribed = false; // 清理订阅标记
        }

        ClearAll(); // 清理缓存与运行时
    }

    /// <summary>
    /// 手动启动波次（由外部控制推进）。
    /// </summary>
    /// <param name="waveId">波次 Id。</param>
    public bool TryStartWave(int waveId) // 手动启动入口
    {
        if (waveId <= 0) // Id 合法性判定
        {
            CY.LogWarning("[WaveManager] 波次 Id 无效，无法启动。"); // 输出无效 Id 警告
            return false; // 无效 Id 时失败
        }

        if (_currentWave != null) // 已有波次运行判定
        {
            CY.LogWarning("[WaveManager] 已有波次在运行，无法重复启动。"); // 输出重复启动警告
            return false; // 重复启动时失败
        }

        if (!_wavePlanMap.TryGetValue(waveId, out var plan) || plan == null) // 波次计划存在判定
        {
            CY.LogWarning($"[WaveManager] 未找到波次计划，WaveId={waveId}"); // 输出缺失警告
            return false; // 计划缺失时失败
        }

        if (!_waveTrackMap.TryGetValue(waveId, out var tracks) || tracks == null || tracks.Count == 0) // 轨道存在判定
        {
            CY.LogWarning($"[WaveManager] 波次未配置轨道，WaveId={waveId}"); // 输出缺失警告
            return false; // 无轨道时失败
        }

        ResetRuntimeInternal(); // 清理旧运行时
        _currentWave = new WaveRuntime(plan); // 创建波次运行时

        for (int i = 0; i < tracks.Count; i++) // 遍历轨道配置
        {
            var trackRow = tracks[i]; // 取出轨道配置
            if (trackRow == null) // 空轨道判定
            {
                continue; // 跳过空轨道
            }

            if (!_spawnGroupRuntimeMap.TryGetValue(trackRow.SpawnGroupId, out var groupRuntime) || groupRuntime == null) // 刷怪组存在判定
            {
                CY.LogWarning($"[WaveManager] 轨道未找到刷怪组，TrackId={trackRow.TrackId}, GroupId={trackRow.SpawnGroupId}"); // 输出缺失警告
                continue; // 刷怪组缺失时跳过
            }

            var runtime = new TrackRuntime(trackRow, groupRuntime); // 创建轨道运行时
            _currentWave.Tracks.Add(runtime); // 添加到轨道列表
        }

        if (_currentWave.Tracks.Count == 0) // 可用轨道判定
        {
            CY.LogWarning($"[WaveManager] 波次没有可用轨道，WaveId={waveId}"); // 输出警告
            _currentWave = null; // 清理运行时
            return false; // 无轨道时失败
        }

        _currentWaveId = waveId; // 记录当前波次 Id
        _lastWaveId = waveId; // 记录最近波次 Id
        PostPrepareStarted(); // 派发波次开始事件
        return true; // 启动成功
    }

    /// <summary>
    /// 随机启动波次（使用全局随机池规则）。
    /// </summary>
    public bool TryStartRandomWave() // 随机启动入口
    {
        if (TryPickRandomWave(out var randomWaveId)) // 随机抽取判定
        {
            return TryStartWave(randomWaveId); // 启动随机波次
        }

        CY.LogWarning("[WaveManager] 随机波次池为空，回退启动第 1 波。"); // 输出回退提示
        return TryStartWave(1); // 回退启动第一波
    }

    /// <summary>
    /// 推进到下一波（当前为 0 时从 1 开始）。
    /// </summary>
    public bool TryAdvanceWave() // 波次推进入口
    {
        var baseWaveId = _currentWaveId > 0 ? _currentWaveId : _lastWaveId; // 获取基准波次 Id
        if (baseWaveId <= 0) // 基准 Id 判定
        {
            return TryStartWave(1); // 回退启动第一波
        }

        var nextWaveId = baseWaveId + 1; // 默认下一波 Id
        WavePlanRow plan = null; // 波次计划缓存
        if (_currentWave != null && _currentWave.Plan != null) // 当前波次计划判定
        {
            plan = _currentWave.Plan; // 使用当前波次计划
        }
        else if (_wavePlanMap.TryGetValue(baseWaveId, out var cachedPlan)) // 缓存计划判定
        {
            plan = cachedPlan; // 使用缓存计划
        }

        if (plan != null) // 计划存在判定
        {
            var planNext = plan.NextWaveId; // 读取配置下一波
            if (planNext > 0) // 固定下一波判定
            {
                nextWaveId = planNext; // 使用固定下一波
            }
            else if (planNext < 0) // 随机下一波判定
            {
                if (TryPickRandomWave(out var randomWaveId)) // 随机抽取判定
                {
                    nextWaveId = randomWaveId; // 使用随机结果
                }
                else
                {
                    CY.LogWarning("[WaveManager] 随机波次池为空，回退为顺序下一波。"); // 输出回退提示
                }
            }
        }

        return TryStartWave(nextWaveId); // 启动下一波
    }

    /// <summary>
    /// 从全局波次池中随机选择下一波（受解锁/过期与权重影响）。
    /// </summary>
    /// <param name="waveId">输出随机波次 Id。</param>
    private bool TryPickRandomWave(out int waveId) // 随机波次选择入口
    {
        waveId = 0; // 默认输出
        if (_wavePlanMap.Count == 0) // 波次表为空判定
        {
            return false; // 无波次时返回 false
        }

        var totalWeight = 0; // 权重总和
        foreach (var pair in _wavePlanMap) // 遍历波次计划
        {
            var plan = pair.Value; // 取出计划
            if (plan == null) // 空计划判定
            {
                continue; // 跳过空计划
            }

            if (plan.RandomWeight <= 0) // 权重判定
            {
                continue; // 权重无效时跳过
            }

            if (_completedWaveCount < plan.UnlockAfterWave) // 解锁判定
            {
                continue; // 未解锁时跳过
            }

            if (plan.ExpireAfterWave > 0 && _completedWaveCount > plan.ExpireAfterWave) // 过期判定
            {
                continue; // 已过期时跳过
            }

            totalWeight += plan.RandomWeight; // 累加权重
        }

        if (totalWeight <= 0) // 权重有效性判定
        {
            return false; // 无有效权重时返回 false
        }

        var roll = UnityEngine.Random.Range(1, totalWeight + 1); // 随机权重点
        var cumulative = 0; // 权重累加
        foreach (var pair in _wavePlanMap) // 再次遍历波次计划
        {
            var plan = pair.Value; // 取出计划
            if (plan == null) // 空计划判定
            {
                continue; // 跳过空计划
            }

            if (plan.RandomWeight <= 0) // 权重判定
            {
                continue; // 权重无效时跳过
            }

            if (_completedWaveCount < plan.UnlockAfterWave) // 解锁判定
            {
                continue; // 未解锁时跳过
            }

            if (plan.ExpireAfterWave > 0 && _completedWaveCount > plan.ExpireAfterWave) // 过期判定
            {
                continue; // 已过期时跳过
            }

            cumulative += plan.RandomWeight; // 累加权重
            if (roll <= cumulative) // 命中判定
            {
                waveId = plan.WaveId; // 写入随机波次 Id
                return true; // 返回成功
            }
        }

        return false; // 未命中时返回 false
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

        if (_currentWave == null || _currentWave.Tracks.Count == 0) // 当前波次判定
        {
            return false; // 无有效波次时返回失败
        }

        stage = _currentWave.HasSpawnStarted ? WaveStage.Spawn : WaveStage.Prepare; // 计算阶段
        remainingSeconds = GetWaveRemaining(_currentWave); // 计算剩余时间
        return true; // 返回成功
    }

    /// <summary>
    /// 获取当前波次的显示编号（用于 UI 展示）。
    /// </summary>
    /// <param name="displayIndex">输出显示编号。</param>
    /// <param name="stage">输出阶段。</param>
    /// <param name="remainingSeconds">输出剩余时间（秒）。</param>
    public bool TryGetMainWaveDisplayStatus(out int displayIndex, out WaveStage stage, out float remainingSeconds) // 显示编号查询入口
    {
        displayIndex = 0; // 默认输出
        stage = WaveStage.None; // 默认阶段
        remainingSeconds = 0f; // 默认剩余时间

        if (!TryGetMainWaveStatus(out var waveId, out stage, out remainingSeconds)) // 复用状态查询
        {
            return false; // 无波次时返回失败
        }

        var plan = _currentWave != null ? _currentWave.Plan : null; // 获取当前波次计划
        if (plan != null && plan.DisplayIndex > 0) // 显示编号有效判定
        {
            displayIndex = plan.DisplayIndex; // 使用自定义显示编号
        }
        else
        {
            displayIndex = waveId; // 回退使用 WaveId
        }

        return true; // 返回成功
    }

    /// <summary>
    /// 内部运行时清理。
    /// </summary>
    private void ResetRuntimeInternal() // 运行时清理入口
    {
        _currentWave = null; // 清理当前波次
        _currentWaveId = 0; // 重置当前波次 Id
        _enemyTrackMap.Clear(); // 清理敌人映射
        _eventTriggerSet.Clear(); // 清理事件触发集合
        _areaEnterSet.Clear(); // 清理区域进入集合
        _areaExitSet.Clear(); // 清理区域离开集合
    }

    /// <summary>
    /// 确保已订阅事件（手动订阅，避免重复）。
    /// </summary>
    private void EnsureSubscribed() // 手动订阅入口
    {
        if (_subscribed) // 已订阅判定
        {
            return; // 已订阅时直接退出
        }

        CY.Event.Subscribe<UnitLifeStateChangedEvent>(OnUnitLifeStateChanged, this); // 订阅单位生命状态事件
        CY.Event.Subscribe<UnitDespawnedEvent>(OnUnitDespawned, this); // 订阅单位移除事件
        CY.Event.Subscribe<WaveTriggerEvent>(OnWaveTrigger, this); // 订阅波次触发事件
        CY.Event.Subscribe<WaveAreaTriggerEvent>(OnWaveAreaTrigger, this); // 订阅区域触发事件
        _subscribed = true; // 标记已订阅
    }

    /// <summary>
    /// 单位生命状态变化事件处理（用于击杀统计）。
    /// </summary>
    /// <param name="evt">事件数据。</param>
    private void OnUnitLifeStateChanged(ref UnitLifeStateChangedEvent evt) // 生命状态事件入口
    {
        if (_currentWave == null) // 波次存在判定
        {
            return; // 无波次时直接退出
        }

        if (evt.Unit == null) // 单位为空判定
        {
            return; // 单位为空时退出
        }

        if (evt.NewState != UnitLifeState.Dead) // 死亡状态判定
        {
            return; // 非死亡状态不处理
        }

        if (_enemyTrackMap.TryGetValue(evt.Unit, out var track)) // 敌人映射判定
        {
            _enemyTrackMap.Remove(evt.Unit); // 移除映射
            if (track != null && track.AliveCount > 0) // 轨道存活判定
            {
                track.AliveCount -= 1; // 递减轨道存活
            }

            if (_currentWave.EnemyAliveCount > 0) // 波次存活判定
            {
                _currentWave.EnemyAliveCount -= 1; // 递减波次存活
            }

            _currentWave.TotalKilled += 1; // 累加击杀数量
        }
    }

    /// <summary>
    /// 单位移除事件处理（兜底清理存活统计）。
    /// </summary>
    /// <param name="evt">事件数据。</param>
    private void OnUnitDespawned(ref UnitDespawnedEvent evt) // 单位移除事件入口
    {
        if (_currentWave == null) // 波次存在判定
        {
            return; // 无波次时退出
        }

        if (evt.Unit == null) // 单位为空判定
        {
            return; // 单位为空时退出
        }

        if (_enemyTrackMap.TryGetValue(evt.Unit, out var track)) // 映射判定
        {
            _enemyTrackMap.Remove(evt.Unit); // 移除映射
            if (track != null && track.AliveCount > 0) // 轨道存活判定
            {
                track.AliveCount -= 1; // 递减轨道存活
            }

            if (_currentWave.EnemyAliveCount > 0) // 波次存活判定
            {
                _currentWave.EnemyAliveCount -= 1; // 递减波次存活
            }
        }
    }

    /// <summary>
    /// 外部触发事件处理（用于 Event 条件）。
    /// </summary>
    /// <param name="evt">事件数据。</param>
    private void OnWaveTrigger(ref WaveTriggerEvent evt) // 波次触发事件入口
    {
        if (string.IsNullOrEmpty(evt.TriggerId)) // 触发 Id 判定
        {
            return; // 触发 Id 为空时退出
        }

        _eventTriggerSet.Add(evt.TriggerId); // 写入触发集合
    }

    /// <summary>
    /// 区域触发事件处理（用于 Area 条件）。
    /// </summary>
    /// <param name="evt">事件数据。</param>
    private void OnWaveAreaTrigger(ref WaveAreaTriggerEvent evt) // 区域触发事件入口
    {
        if (string.IsNullOrEmpty(evt.AreaId)) // 区域 Id 判定
        {
            return; // 区域 Id 为空时退出
        }

        if (evt.IsEnter) // 进入判定
        {
            _areaEnterSet.Add(evt.AreaId); // 写入进入集合
        }
        else
        {
            _areaExitSet.Add(evt.AreaId); // 写入离开集合
        }
    }

    /// <summary>
    /// 获取波次剩余时间（用于 UI 展示，非强保证）。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    private float GetWaveRemaining(WaveRuntime runtime) // 波次剩余时间入口
    {
        if (runtime == null || runtime.Plan == null) // 空判定
        {
            return 0f; // 无波次时返回 0
        }

        var endType = (WaveTriggerType)runtime.Plan.EndType; // 解析结束类型
        if (endType == WaveTriggerType.Time) // 时间结束判定
        {
            var remaining = runtime.Plan.EndValue - runtime.ElapsedTime; // 计算剩余
            return remaining > 0f ? remaining : 0f; // 返回非负剩余
        }

        var max = 0f; // 最大剩余时间
        for (int i = 0; i < runtime.Tracks.Count; i++) // 遍历轨道
        {
            var track = runtime.Tracks[i]; // 取出轨道
            if (track == null || track.IsFinished) // 轨道有效性判定
            {
                continue; // 跳过无效轨道
            }

            var remaining = GetTrackRemaining(track); // 计算轨道剩余
            if (remaining > max) // 最大值判定
            {
                max = remaining; // 更新最大值
            }
        }

        return max; // 返回最大剩余
    }

    /// <summary>
    /// 获取轨道剩余时间（仅支持时间型轨道）。
    /// </summary>
    /// <param name="track">轨道运行时。</param>
    private float GetTrackRemaining(TrackRuntime track) // 轨道剩余时间入口
    {
        if (track == null || track.Row == null) // 空判定
        {
            return 0f; // 无效轨道返回 0
        }

        if ((WaveTriggerType)track.Row.EndType != WaveTriggerType.Time) // 非时间结束判定
        {
            return 0f; // 非时间型返回 0
        }

        if (!track.IsStarted) // 未开始判定
        {
            return 0f; // 未开始返回 0
        }

        var remaining = track.Row.EndValue - track.ElapsedTime; // 计算剩余
        return remaining > 0f ? remaining : 0f; // 返回非负剩余
    }

    /// <summary>
    /// 派发准备阶段开始事件。
    /// </summary>
    private void PostPrepareStarted() // 事件派发入口
    {
        var evt = new WavePrepareStartedEvent // 创建事件
        {
            WaveId = _currentWaveId, // 写入波次 Id
            IsAssault = false // 写入奇袭标记
        };
        CY.Event.Post(ref evt); // 派发事件
    }

    /// <summary>
    /// 派发刷怪阶段开始事件。
    /// </summary>
    private void PostSpawnStarted() // 事件派发入口
    {
        var evt = new WaveSpawnStartedEvent // 创建事件
        {
            WaveId = _currentWaveId, // 写入波次 Id
            IsAssault = false // 写入奇袭标记
        };
        CY.Event.Post(ref evt); // 派发事件
    }

    /// <summary>
    /// 派发波次结束事件。
    /// </summary>
    private void PostWaveFinished() // 事件派发入口
    {
        var evt = new WaveFinishedEvent // 创建事件
        {
            WaveId = _currentWaveId, // 写入波次 Id
            IsAssault = false // 写入奇袭标记
        };
        CY.Event.Post(ref evt); // 派发事件
    }

    /// <summary>
    /// 派发暂停事件。
    /// </summary>
    /// <param name="paused">是否暂停。</param>
    private void PostPauseEvent(bool paused) // 事件派发入口
    {
        var evt = new WavePauseEvent // 创建事件
        {
            IsPaused = paused // 写入暂停状态
        };
        CY.Event.Post(ref evt); // 派发事件
    }
}
