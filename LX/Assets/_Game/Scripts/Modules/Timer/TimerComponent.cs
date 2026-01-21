using System;
using GameFramework;
using GameFramework.Timer;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 计时器组件（框架模块包装）。
/// 仅提供 Unity 侧入口，计时更新由框架模块统一轮询驱动。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Timer")]
public sealed class TimerComponent : GameFrameworkComponent
{
    /// <summary>
    /// 计时器列表初始容量（用于减少模块内部扩容开销）。
    /// </summary>
    [SerializeField]
    private int m_InitialCapacity = 32;

    /// <summary>
    /// 默认是否使用真实时间（不受 Time.timeScale 影响）。
    /// </summary>
    [SerializeField]
    private bool m_DefaultUseUnscaledTime = false;

    /// <summary>
    /// 计时器模块实例（由框架创建并统一轮询）。
    /// </summary>
    private ITimerManager m_TimerManager;

    /// <summary>
    /// 当前激活计时器数量。
    /// </summary>
    public int ActiveCount
    {
        get
        {
            EnsureTimerManager();
            return m_TimerManager != null ? m_TimerManager.ActiveCount : 0;
        }
    }

    /// <summary>
    /// 默认时间模式（true：真实时间；false：逻辑时间）。
    /// </summary>
    public bool DefaultUseUnscaledTime
    {
        get => m_DefaultUseUnscaledTime;
        set => m_DefaultUseUnscaledTime = value;
    }

    /// <summary>
    /// 初始化组件并配置计时器模块。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        if (m_InitialCapacity < 1)
        {
            m_InitialCapacity = 1;
        }

        EnsureTimerManager();
        m_TimerManager?.SetInitialCapacity(m_InitialCapacity);
    }

    /// <summary>
    /// 延迟执行（使用默认时间模式）。
    /// </summary>
    public Timer Delay(float seconds, Action onComplete)
    {
        return Delay(seconds, onComplete, m_DefaultUseUnscaledTime);
    }

    /// <summary>
    /// 延迟执行（可指定时间模式）。
    /// </summary>
    public Timer Delay(float seconds, Action onComplete, bool useUnscaledTime)
    {
        EnsureTimerManager();
        return m_TimerManager != null ? m_TimerManager.Delay(seconds, onComplete, useUnscaledTime) : null;
    }

    /// <summary>
    /// 延迟执行（带进度回调）。
    /// </summary>
    public Timer Delay(float seconds, Action onComplete, Action<float> onProgress, bool useUnscaledTime)
    {
        Timer timer = Delay(seconds, onComplete, useUnscaledTime);
        if (timer != null && onProgress != null)
        {
            timer.OnUpdate(onProgress);
        }
        return timer;
    }

    /// <summary>
    /// 循环执行（使用默认时间模式）。
    /// </summary>
    public Timer Loop(float interval, Action onTick)
    {
        return Loop(interval, onTick, m_DefaultUseUnscaledTime);
    }

    /// <summary>
    /// 循环执行（可指定时间模式）。
    /// </summary>
    public Timer Loop(float interval, Action onTick, bool useUnscaledTime)
    {
        EnsureTimerManager();
        return m_TimerManager != null ? m_TimerManager.Loop(interval, onTick, useUnscaledTime) : null;
    }

    /// <summary>
    /// 循环执行（带进度回调）。
    /// </summary>
    public Timer Loop(float interval, Action onTick, Action<float> onProgress, bool useUnscaledTime)
    {
        Timer timer = Loop(interval, onTick, useUnscaledTime);
        if (timer != null && onProgress != null)
        {
            timer.OnUpdate(onProgress);
        }
        return timer;
    }

    /// <summary>
    /// 下一帧执行（不走计时器列表）。
    /// </summary>
    public void NextFrame(Action onComplete)
    {
        EnsureTimerManager();
        m_TimerManager?.NextFrame(onComplete);
    }

    /// <summary>
    /// 下一帧执行（返回 Timer 以保持统一调用风格）。
    /// </summary>
    public Timer NextFrameTimer(Action onComplete)
    {
        return Delay(0f, onComplete, true);
    }

    /// <summary>
    /// 取消指定计时器（按实例）。
    /// </summary>
    public void Cancel(Timer timer)
    {
        if (timer == null)
        {
            return;
        }

        Cancel(timer.Id);
    }

    /// <summary>
    /// 取消指定计时器（按 ID）。
    /// </summary>
    public void Cancel(int timerId)
    {
        EnsureTimerManager();
        m_TimerManager?.Cancel(timerId);
    }

    /// <summary>
    /// 尝试取消计时器（找不到返回 false）。
    /// </summary>
    public bool TryCancel(int timerId)
    {
        EnsureTimerManager();
        return m_TimerManager != null && m_TimerManager.TryCancel(timerId);
    }

    /// <summary>
    /// 获取计时器实例。
    /// </summary>
    public Timer GetTimer(int timerId)
    {
        EnsureTimerManager();
        return m_TimerManager != null ? m_TimerManager.GetTimer(timerId) : null;
    }

    /// <summary>
    /// 是否存在指定计时器。
    /// </summary>
    public bool HasTimer(int timerId)
    {
        EnsureTimerManager();
        return m_TimerManager != null && m_TimerManager.HasTimer(timerId);
    }

    /// <summary>
    /// 暂停计时器。
    /// </summary>
    public void Pause(int timerId)
    {
        EnsureTimerManager();
        m_TimerManager?.Pause(timerId);
    }

    /// <summary>
    /// 恢复计时器。
    /// </summary>
    public void Resume(int timerId)
    {
        EnsureTimerManager();
        m_TimerManager?.Resume(timerId);
    }

    /// <summary>
    /// 取消所有计时器（不触发回调）。
    /// </summary>
    public void CancelAll()
    {
        EnsureTimerManager();
        m_TimerManager?.CancelAll();
    }

    /// <summary>
    /// 确保计时器模块已创建并缓存。
    /// </summary>
    private void EnsureTimerManager()
    {
        if (m_TimerManager == null)
        {
            m_TimerManager = GameFrameworkEntry.GetModule<ITimerManager>();
        }
    }
}
