using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 计时器组件（模块形式）。
/// 使用框架的流逝时间：逻辑时间=Time.deltaTime，真实时间=Time.unscaledDeltaTime，
/// 与 BaseComponent 中调用 GameFrameworkEntry.Update 的时间来源保持一致。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Timer")]
public sealed class TimerComponent : GameFrameworkComponent
{
    /// <summary>
    /// 计时器列表初始容量（避免频繁扩容）。
    /// </summary>
    [SerializeField]
    private int m_InitialCapacity = 32;

    /// <summary>
    /// 默认是否使用真实时间（不受 Time.timeScale 影响）。
    /// </summary>
    [SerializeField]
    private bool m_DefaultUseUnscaledTime = false;

    /// <summary>
    /// 所有激活中的计时器。
    /// </summary>
    private List<Timer> m_Timers;

    /// <summary>
    /// 下一帧要执行的回调（真正的下一帧）。
    /// </summary>
    private readonly List<Action> m_NextFrameActions = new();

    /// <summary>
    /// 执行中的回调缓存（避免遍历时修改集合）。
    /// </summary>
    private readonly List<Action> m_ExecutingActions = new();

    /// <summary>
    /// 计时器 ID 自增计数器。
    /// </summary>
    private int m_NextId = 1;

    /// <summary>
    /// 当前激活计时器数量。
    /// </summary>
    public int ActiveCount => m_Timers?.Count ?? 0;

    /// <summary>
    /// 默认时间模式（true：真实时间；false：逻辑时间）。
    /// </summary>
    public bool DefaultUseUnscaledTime
    {
        get => m_DefaultUseUnscaledTime;
        set => m_DefaultUseUnscaledTime = value;
    }

    /// <summary>
    /// 初始化计时器列表。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        if (m_InitialCapacity < 1)
        {
            m_InitialCapacity = 1;
        }

        m_Timers = new List<Timer>(m_InitialCapacity);
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
        EnsureInitialized();
        Timer timer = new Timer(seconds, onComplete, false, useUnscaledTime) { Id = m_NextId++ };
        m_Timers.Add(timer);
        return timer;
    }

    /// <summary>
    /// 延迟执行（带进度回调）。
    /// </summary>
    public Timer Delay(float seconds, Action onComplete, Action<float> onProgress, bool useUnscaledTime)
    {
        Timer timer = Delay(seconds, onComplete, useUnscaledTime);
        if (onProgress != null)
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
        EnsureInitialized();
        Timer timer = new Timer(interval, onTick, true, useUnscaledTime) { Id = m_NextId++ };
        m_Timers.Add(timer);
        return timer;
    }

    /// <summary>
    /// 循环执行（带进度回调）。
    /// </summary>
    public Timer Loop(float interval, Action onTick, Action<float> onProgress, bool useUnscaledTime)
    {
        Timer timer = Loop(interval, onTick, useUnscaledTime);
        if (onProgress != null)
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
        if (onComplete != null)
        {
            m_NextFrameActions.Add(onComplete);
        }
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
        if (timer == null || m_Timers == null)
        {
            return;
        }

        timer.Stop();
        m_Timers.Remove(timer);
    }

    /// <summary>
    /// 取消指定计时器（按 ID）。
    /// </summary>
    public void Cancel(int timerId)
    {
        RemoveTimerById(timerId, out _);
    }

    /// <summary>
    /// 尝试取消计时器（找不到返回 false）。
    /// </summary>
    public bool TryCancel(int timerId)
    {
        return RemoveTimerById(timerId, out _);
    }

    /// <summary>
    /// 获取计时器实例。
    /// </summary>
    public Timer GetTimer(int timerId)
    {
        int index = FindTimerIndex(timerId);
        return index >= 0 ? m_Timers[index] : null;
    }

    /// <summary>
    /// 是否存在指定计时器。
    /// </summary>
    public bool HasTimer(int timerId)
    {
        Timer timer = GetTimer(timerId);
        return timer != null && !timer.IsCompleted;
    }

    /// <summary>
    /// 暂停计时器。
    /// </summary>
    public void Pause(int timerId)
    {
        GetTimer(timerId)?.Pause();
    }

    /// <summary>
    /// 恢复计时器。
    /// </summary>
    public void Resume(int timerId)
    {
        GetTimer(timerId)?.Resume();
    }

    /// <summary>
    /// 取消所有计时器（不触发回调）。
    /// </summary>
    public void CancelAll()
    {
        if (m_Timers == null)
        {
            return;
        }

        m_Timers.Clear();
    }

    /// <summary>
    /// Unity Update。
    /// 注意：这里使用的逻辑/真实流逝时间与框架更新一致。
    /// </summary>
    private void Update()
    {
        if (m_Timers == null)
        {
            return;
        }

        ExecuteNextFrameActions();

        float elapseSeconds = Time.deltaTime; // 逻辑流逝时间（受 Time.timeScale 影响）
        float realElapseSeconds = Time.unscaledDeltaTime; // 真实流逝时间（不受 Time.timeScale 影响）

        UpdateTimers(elapseSeconds, realElapseSeconds);
    }

    /// <summary>
    /// 执行下一帧回调队列。
    /// </summary>
    private void ExecuteNextFrameActions()
    {
        if (m_NextFrameActions.Count <= 0)
        {
            return;
        }

        m_ExecutingActions.Clear();
        m_ExecutingActions.AddRange(m_NextFrameActions);
        m_NextFrameActions.Clear();

        for (int i = 0; i < m_ExecutingActions.Count; i++)
        {
            try
            {
                m_ExecutingActions[i]?.Invoke();
            }
            catch (Exception exception)
            {
                Log.Warning("Timer NextFrame callback exception: {0}", exception.Message);
            }
        }
    }

    /// <summary>
    /// 使用框架时间更新计时器列表。
    /// </summary>
    private void UpdateTimers(float elapseSeconds, float realElapseSeconds)
    {
        for (int i = m_Timers.Count - 1; i >= 0; i--)
        {
            Timer timer = m_Timers[i];
            float step = timer.UseUnscaledTime ? realElapseSeconds : elapseSeconds;
            if (timer.Update(step))
            {
                m_Timers.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 确保计时器列表已初始化。
    /// </summary>
    private void EnsureInitialized()
    {
        if (m_Timers == null)
        {
            m_Timers = new List<Timer>(m_InitialCapacity < 1 ? 1 : m_InitialCapacity);
        }
    }

    /// <summary>
    /// 查找计时器索引（按 ID）。
    /// </summary>
    private int FindTimerIndex(int timerId)
    {
        if (timerId <= 0 || m_Timers == null)
        {
            return -1;
        }

        for (int i = 0; i < m_Timers.Count; i++)
        {
            if (m_Timers[i] != null && m_Timers[i].Id == timerId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 根据 ID 移除计时器。
    /// </summary>
    private bool RemoveTimerById(int timerId, out Timer timer)
    {
        timer = null;
        int index = FindTimerIndex(timerId);
        if (index < 0)
        {
            return false;
        }

        timer = m_Timers[index];
        timer?.Stop();
        m_Timers.RemoveAt(index);
        return true;
    }
}
