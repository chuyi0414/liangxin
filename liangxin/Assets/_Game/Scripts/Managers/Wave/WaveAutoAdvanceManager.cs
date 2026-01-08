using CYFramework; // 框架统一入口
using CYFramework.Infrastructure; // 生命周期接口引用
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 波次自动推进管理器：主线自动推进 + 奇袭独立计时触发。
/// </summary>
public sealed class WaveAutoAdvanceManager : MonoBehaviour, IInitializable, IUpdateable, IDisposableEx // 自动推进管理器定义
{
    /// <summary>是否启用自动推进。</summary>
    [SerializeField] private bool _autoAdvance = true; // 自动推进开关
    /// <summary>是否启用概率奇袭。</summary>
    [SerializeField] private bool _autoAssault = true; // 自动奇袭开关
    /// <summary>奇袭概率提升的时间间隔（秒）。</summary>
    [SerializeField] private float _assaultProbIntervalSeconds = 120f; // 概率间隔
    /// <summary>奇袭基础概率（0-1）。</summary>
    [SerializeField] private float _assaultBaseProb = 0f; // 基础概率
    /// <summary>奇袭概率步进（每波递增）。</summary>
    [SerializeField] private float _assaultProbStep = 0.02f; // 概率步进
    /// <summary>奇袭最小概率（0-1）。</summary>
    [SerializeField] private float _assaultMinProb = 0f; // 最小概率
    /// <summary>奇袭最大概率（0-1）。</summary>
    [SerializeField] private float _assaultMaxProb = 0.35f; // 最大概率
    /// <summary>奇袭计时器累计时间。</summary>
    private float _assaultTimer; // 计时器
    /// <summary>概率提升次数。</summary>
    private int _assaultProbLevel; // 概率层级

    /// <summary>是否已注册到 ServiceLocator。</summary>
    private bool _registered; // 注册标记
    /// <summary>是否已初始化。</summary>
    private bool _initialized; // 初始化标记
    /// <summary>是否已释放。</summary>
    private bool _disposed; // 释放标记
    /// <summary>是否已订阅事件。</summary>
    private bool _subscribed; // 订阅标记
    /// <summary>奇袭是否正在运行。</summary>
    private bool _assaultRunning; // 奇袭运行标记

    /// <summary>初始化顺序（确保在 WaveManager 之后）。</summary>
    public int InitOrder => 150; // 初始化顺序
    /// <summary>释放顺序（数值大的先释放）。</summary>
    public int DisposeOrder => -150; // 释放顺序
    /// <summary>更新顺序（数值小的先执行）。</summary>
    public int UpdateOrder => 360; // 更新顺序

    private void Awake() // Unity 生命周期入口
    {
        if (ServiceLocator.TryGet<WaveAutoAdvanceManager>(out var existing) && existing != this)
        {
            Destroy(this); // 已存在实例时销毁当前组件
            return; // 直接退出
        }

        ServiceLocator.RegisterInstance(this); // 注册到服务定位器
        _registered = true; // 标记已注册
        EnsureSubscribed(); // 提前订阅事件，避免未初始化时漏订阅
        _initialized = true; // 标记已初始化
    }

    private void OnDestroy() // Unity 生命周期出口
    {
        if (_registered)
        {
            Dispose(); // 执行清理
            ServiceLocator.Unregister<WaveAutoAdvanceManager>(); // 注销服务
            _registered = false; // 标记未注册
        }
    }

    /// <summary>
    /// 初始化（由 ServiceLocator 驱动，只会执行一次）。
    /// </summary>
    public void Initialize() // 初始化入口
    {
        if (_initialized)
        {
            return; // 已初始化时直接退出
        }

        _initialized = true; // 标记已初始化
        EnsureSubscribed(); // 确保已订阅事件
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
        _initialized = false; // 重置初始化标记
        if (_subscribed)
        {
            CY.Event.UnsubscribeAll(this); // 取消事件订阅
            _subscribed = false; // 清理订阅标记
        }
    }

    /// <summary>
    /// 重置自动推进运行时状态（用于流程切换复位）。
    /// </summary>
    public void ResetRuntime() // 自动推进运行时重置入口
    {
        if (_disposed)
        {
            return; // 已释放时直接退出
        }

        _assaultTimer = 0f; // 重置奇袭计时器
        _assaultProbLevel = 0; // 重置概率层级
        _assaultRunning = false; // 清理奇袭运行标记
    }

    /// <summary>
    /// 每帧更新（驱动奇袭计时器）。
    /// </summary>
    /// <param name="deltaTime">帧间隔时间。</param>
    public void OnUpdate(float deltaTime) // Update 入口
    {
        if (_disposed)
        {
            return; // 已释放时退出
        }

        if (!_autoAssault)
        {
            return; // 未启用自动奇袭时退出
        }

        if (_assaultProbIntervalSeconds <= 0f)
        {
            return; // 时间间隔无效时退出
        }

        var waveManager = CY.Wave; // 获取波次管理器
        if (waveManager == null || waveManager.IsPaused)
        {
            return; // 管理器未就绪或暂停时退出
        }

        _assaultTimer += deltaTime; // 累积计时器
        var guard = 0; // 循环保护
        while (_assaultTimer >= _assaultProbIntervalSeconds && guard < 4)
        {
            _assaultTimer -= _assaultProbIntervalSeconds; // 消耗一段间隔
            TryRollAssault(waveManager); // 尝试触发奇袭
            guard++; // 递增保护计数
        }
    }

    /// <summary>
    /// 确保已订阅事件（手动订阅，避免重复）。
    /// </summary>
    private void EnsureSubscribed() // 手动订阅入口
    {
        if (_subscribed)
        {
            return; // 已订阅时直接退出
        }

        CY.Event.Subscribe<WaveFinishedEvent>(OnWaveFinished, this); // 手动订阅波次结束事件
        CY.Event.Subscribe<WavePrepareStartedEvent>(OnWavePrepareStarted, this); // 手动订阅波次准备开始事件
        _subscribed = true; // 标记已订阅
    }

    /// <summary>
    /// 按概率尝试触发奇袭。
    /// </summary>
    /// <param name="waveManager">波次管理器。</param>
    private void TryRollAssault(WaveManager waveManager) // 奇袭触发入口
    {
        if (waveManager == null)
        {
            return; // 管理器为空时退出
        }

        if (_assaultRunning)
        {
            return; // 奇袭运行中不触发
        }

        var prob = _assaultBaseProb + _assaultProbStep * _assaultProbLevel; // 计算触发概率
        prob = Mathf.Clamp(prob, _assaultMinProb, _assaultMaxProb); // 约束概率范围
        if (Random.value >= prob)
        {
            _assaultProbLevel++; // 未命中时提升概率层级
            return; // 直接退出
        }

        if (waveManager.TryStartRandomAssaultWave(out _))
        {
            _assaultRunning = true; // 标记奇袭已开始
            _assaultProbLevel = 0; // 重置概率层级
            _assaultTimer = 0f; // 重置计时器
        }
    }

    /// <summary>
    /// 波次结束回调：启动下一波。
    /// </summary>
    /// <param name="evt">波次结束事件。</param>
    private void OnWaveFinished(ref WaveFinishedEvent evt) // 波次结束事件入口
    {
        if (_disposed)
        {
            return; // 已释放时退出
        }

        if (!_autoAdvance)
        {
            return; // 关闭自动推进时退出
        }

        var waveManager = CY.Wave; // 获取波次管理器
        if (waveManager == null)
        {
            CY.LogWarning("[WaveAutoAdvanceManager] WaveManager 未就绪，无法推进下一波。"); // 输出警告
            return; // 管理器为空时退出
        }

        if (evt.IsAssault)
        {
            _assaultRunning = false; // 清理奇袭运行标记
            return; // 奇袭波次结束不自动推进
        }

        if (waveManager.IsPaused)
        {
            return; // 暂停状态下不推进
        }

        var nextWaveId = evt.WaveId + 1; // 计算下一波 Id
        if (!waveManager.TryStartWave(nextWaveId))
        {
            CY.LogWarning($"[WaveAutoAdvanceManager] 自动推进失败，NextWaveId={nextWaveId}。"); // 输出失败日志
        }
    }

    /// <summary>
    /// 波次准备阶段开始回调：判断是否触发奇袭。
    /// </summary>
    /// <param name="evt">波次准备开始事件。</param>
    private void OnWavePrepareStarted(ref WavePrepareStartedEvent evt) // 波次准备事件入口
    {
        if (_disposed)
        {
            return; // 已释放时退出
        }

        if (evt.IsAssault)
        {
            _assaultRunning = true; // 标记奇袭已开始
            _assaultProbLevel = 0; // 重置概率层级
            _assaultTimer = 0f; // 重置计时器
            return; // 奇袭自身不再触发奇袭
        }
    }
}
