// 引用 CYFramework 入口，使用 CY.Event/CY.Log
using CYFramework; // 框架统一入口
// 引用生命周期接口命名空间，使用 IInitializable/IUpdateable/IDisposableEx
using CYFramework.Infrastructure; // 生命周期接口引用
// 引用 UnityEngine，使用 MonoBehaviour/SerializeField
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 波次自动推进管理器：在波次结束后自动推进下一波。
/// </summary>
public sealed class WaveAutoAdvanceManager : MonoBehaviour, IInitializable, IUpdateable, IDisposableEx // 自动推进管理器定义
{
    /// <summary>是否启用自动推进。</summary>
    [SerializeField] private bool _autoAdvance = true; // 自动推进开关

    /// <summary>是否已注册到 ServiceLocator。</summary>
    private bool _registered; // 注册标记
    /// <summary>是否已初始化。</summary>
    private bool _initialized; // 初始化标记
    /// <summary>是否已释放。</summary>
    private bool _disposed; // 释放标记
    /// <summary>是否已订阅事件。</summary>
    private bool _subscribed; // 订阅标记
    /// <summary>是否已请求下一帧自动推进。</summary>
    private bool _pendingAdvance; // 自动推进请求标记
    /// <summary>是否允许在下一帧自动推进。</summary>
    private bool _advanceAllowed; // 自动推进许可标记

    /// <summary>初始化顺序（确保在 WaveManager 之后）。</summary>
    public int InitOrder => 150; // 初始化顺序
    /// <summary>释放顺序（数值大的先释放）。</summary>
    public int DisposeOrder => -150; // 释放顺序
    /// <summary>更新顺序（数值小的先执行）。</summary>
    public int UpdateOrder => 360; // 更新顺序

    /// <summary>
    /// Unity Awake：注册到 ServiceLocator。
    /// </summary>
    private void Awake() // 生命周期：Awake
    {
        if (ServiceLocator.TryGet<WaveAutoAdvanceManager>(out var existing) && existing != this) // 重复实例判定
        {
            Destroy(this); // 已存在实例时销毁当前组件
            return; // 直接退出
        }

        ServiceLocator.RegisterInstance(this); // 注册到服务定位器
        _registered = true; // 标记已注册
        EnsureSubscribed(); // 提前订阅事件
        _initialized = true; // 标记已初始化
    }

    /// <summary>
    /// Unity OnDestroy：注销服务并清理。
    /// </summary>
    private void OnDestroy() // 生命周期：OnDestroy
    {
        if (_registered) // 注册判定
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
        if (_initialized) // 已初始化判定
        {
            return; // 已初始化时直接退出
        }

        _initialized = true; // 标记已初始化
        EnsureSubscribed(); // 确保已订阅事件
    }

    /// <summary>
    /// 每帧更新（保留接口，当前无逻辑）。
    /// </summary>
    /// <param name="deltaTime">帧间隔时间。</param>
    public void OnUpdate(float deltaTime) // Update 入口
    {
        if (_disposed) // 已释放判定
        {
            return; // 已释放时退出
        }
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
        _initialized = false; // 重置初始化标记
        _pendingAdvance = false; // 清理自动推进请求标记
        _advanceAllowed = false; // 清理自动推进许可标记
        if (_subscribed) // 订阅判定
        {
            CY.Event.UnsubscribeAll(this); // 取消事件订阅
            _subscribed = false; // 清理订阅标记
        }
    }

    /// <summary>
    /// 重置运行时状态（供流程切换时调用）。
    /// </summary>
    public void ResetRuntime() // 运行时重置入口
    {
        if (_disposed) // 已释放判定
        {
            return; // 已释放时退出
        }

        _pendingAdvance = false; // 重置自动推进请求标记
        _advanceAllowed = false; // 重置自动推进许可标记
        EnsureSubscribed(); // 确保仍然订阅波次事件
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

        CY.Event.Subscribe<WaveFinishedEvent>(OnWaveFinished, this); // 订阅波次结束事件
        _subscribed = true; // 标记已订阅
    }

    /// <summary>
    /// 波次结束回调：自动推进下一波。
    /// </summary>
    /// <param name="evt">波次结束事件。</param>
    private void OnWaveFinished(ref WaveFinishedEvent evt) // 波次结束事件入口
    {
        if (_disposed) // 已释放判定
        {
            return; // 已释放时退出
        }

        if (!_autoAdvance) // 自动推进开关判定
        {
            return; // 关闭自动推进时退出
        }

        var waveManager = CY.Wave; // 获取波次管理器
        if (waveManager == null) // 管理器判定
        {
            CY.LogWarning("[WaveAutoAdvanceManager] WaveManager 未就绪，无法推进下一波。"); // 输出警告
            return; // 管理器为空时退出
        }

        if (!waveManager.AutoAdvanceEnabled) // 波次配置判定
        {
            return; // 未启用自动推进时退出
        }

        if (waveManager.IsPaused) // 暂停判定
        {
            return; // 暂停状态下不推进
        }

        if (_pendingAdvance) // 重复请求判定
        {
            return; // 已请求时直接退出
        }

        _advanceAllowed = true; // 记录允许自动推进
        _pendingAdvance = true; // 标记已请求自动推进
        CY.Timer.NextFrame(AdvanceNextWave); // 下一帧执行推进，确保当前波次已完成清理
    }

    /// <summary>
    /// 下一帧执行自动推进。
    /// </summary>
    private void AdvanceNextWave() // 自动推进执行入口
    {
        _pendingAdvance = false; // 清理请求标记

        if (_disposed) // 已释放判定
        {
            return; // 已释放时退出
        }

        if (!_autoAdvance) // 自动推进开关判定
        {
            return; // 关闭自动推进时退出
        }

        if (!_advanceAllowed) // 自动推进许可判定
        {
            return; // 未允许推进时退出
        }

        _advanceAllowed = false; // 清理许可标记
        var waveManager = CY.Wave; // 获取波次管理器
        if (waveManager == null) // 管理器判定
        {
            CY.LogWarning("[WaveAutoAdvanceManager] WaveManager 未就绪，无法推进下一波。"); // 输出警告
            return; // 管理器为空时退出
        }

        if (waveManager.IsPaused) // 暂停判定
        {
            return; // 暂停状态下不推进
        }

        if (!waveManager.TryAdvanceWave()) // 推进下一波判定
        {
            CY.LogWarning("[WaveAutoAdvanceManager] 自动推进失败。"); // 输出失败日志
        }
    }
}
