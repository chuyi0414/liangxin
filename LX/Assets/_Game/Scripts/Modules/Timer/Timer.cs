using System;

/// <summary>
/// 计时器句柄。
/// 保存计时状态，并由计时器组件驱动更新。
/// </summary>
public sealed class Timer
{
    /// <summary>
    /// 计时器唯一 ID。
    /// </summary>
    public int Id { get; internal set; }

    /// <summary>
    /// 持续时长（秒）。
    /// </summary>
    public float Duration { get; private set; }

    /// <summary>
    /// 已流逝时间（秒）。
    /// </summary>
    public float Elapsed { get; private set; }

    /// <summary>
    /// 是否循环。
    /// </summary>
    public bool IsLoop { get; private set; }

    /// <summary>
    /// 是否暂停。
    /// </summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// 是否已完成。
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// 是否使用真实时间（不受 Time.timeScale 影响）。
    /// </summary>
    public bool UseUnscaledTime { get; private set; }

    /// <summary>
    /// 完成回调。
    /// </summary>
    private Action m_OnComplete;

    /// <summary>
    /// 进度回调（0~1）。
    /// </summary>
    private Action<float> m_OnUpdate;

    /// <summary>
    /// 构造计时器（仅限组件内部创建）。
    /// </summary>
    internal Timer(float duration, Action onComplete, bool isLoop, bool useUnscaledTime)
    {
        Duration = duration;
        m_OnComplete = onComplete;
        IsLoop = isLoop;
        UseUnscaledTime = useUnscaledTime;
    }

    /// <summary>
    /// 设置进度回调（返回 this 便于链式调用）。
    /// </summary>
    public Timer OnUpdate(Action<float> onUpdate)
    {
        m_OnUpdate = onUpdate;
        return this;
    }

    /// <summary>
    /// 暂停计时器。
    /// </summary>
    public void Pause()
    {
        IsPaused = true;
    }

    /// <summary>
    /// 恢复计时器。
    /// </summary>
    public void Resume()
    {
        IsPaused = false;
    }

    /// <summary>
    /// 停止计时器（标记为完成）。
    /// </summary>
    public void Stop()
    {
        IsCompleted = true;
    }

    /// <summary>
    /// 重置计时器（不会改变时长与回调）。
    /// </summary>
    public void Reset()
    {
        Elapsed = 0f;
        IsCompleted = false;
    }

    /// <summary>
    /// 内部更新，由 TimerComponent 传入框架流逝时间驱动。
    /// 返回 true 表示计时完成，需要从列表移除。
    /// </summary>
    internal bool Update(float deltaTime)
    {
        if (IsPaused || IsCompleted)
        {
            return false;
        }

        // 约定：Duration <= 0 时下一次更新立即完成，避免除零或异常。
        if (Duration <= 0f)
        {
            Elapsed = Duration;
            m_OnUpdate?.Invoke(1f);
            m_OnComplete?.Invoke();

            if (IsLoop)
            {
                Elapsed = 0f;
                return false;
            }

            IsCompleted = true;
            return true;
        }

        Elapsed += deltaTime;
        m_OnUpdate?.Invoke(Elapsed / Duration);

        if (Elapsed >= Duration)
        {
            m_OnComplete?.Invoke();

            if (IsLoop)
            {
                Elapsed = 0f;
                return false;
            }

            IsCompleted = true;
            return true;
        }

        return false;
    }
}
