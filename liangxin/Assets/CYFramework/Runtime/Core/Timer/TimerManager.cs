// ============================================================================
// CYFramework - 计时器管理器
// ============================================================================

using System;
using System.Collections.Generic;
using CYFramework.Core.Config;
using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Core.Timer
{
    /// <summary>
    /// 计时器
    /// </summary>
    public class Timer
    {
        /// <summary>
        /// 计时器唯一 ID
        /// </summary>
        public int Id { get; internal set; }

        /// <summary>
        /// 持续时间（秒）
        /// </summary>
        public float Duration { get; private set; }

        /// <summary>
        /// 已流逝时间（秒）
        /// </summary>
        public float Elapsed { get; private set; }

        /// <summary>
        /// 是否循环
        /// </summary>
        public bool IsLoop { get; private set; }

        /// <summary>
        /// 是否暂停
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted { get; private set; }

        /// <summary>
        /// 是否使用不受 TimeScale 影响的时间
        /// </summary>
        public bool UseUnscaledTime { get; private set; }
        
        /// <summary>
        /// 完成回调
        /// </summary>
        private Action _onComplete;

        /// <summary>
        /// 进度回调（0~1）
        /// </summary>
        private Action<float> _onUpdate;
        
        /// <summary>
        /// 创建计时器（仅供管理器内部使用）
        /// </summary>
        internal Timer(float duration, Action onComplete, bool isLoop, bool useUnscaledTime)
        {
            Duration = duration;
            _onComplete = onComplete;
            IsLoop = isLoop;
            UseUnscaledTime = useUnscaledTime;
        }
        
        /// <summary>
        /// 设置更新回调
        /// </summary>
        /// <param name="onUpdate">回调函数（参数为 0~1 的进度）</param>
        public Timer OnUpdate(Action<float> onUpdate)
        {
            _onUpdate = onUpdate;
            return this;
        }
        
        /// <summary>
        /// 暂停
        /// </summary>
        public void Pause() => IsPaused = true;
        
        /// <summary>
        /// 恢复
        /// </summary>
        public void Resume() => IsPaused = false;
        
        /// <summary>
        /// 停止
        /// </summary>
        public void Stop() => IsCompleted = true;
        
        /// <summary>
        /// 重置
        /// </summary>
        public void Reset()
        {
            Elapsed = 0f;
            IsCompleted = false;
        }
        
        /// <summary>
        /// 更新计时器（内部调用）
        /// </summary>
        internal bool Update(float deltaTime)
        {
            if (IsPaused || IsCompleted) return false;

            // 防御：Duration <= 0 的计时器视为“下一次更新立即完成”，避免除零与不确定行为。
            // 常见用法：Delay(0f) / NextFrameTimer 等。
            if (Duration <= 0f)
            {
                Elapsed = Duration;
                _onUpdate?.Invoke(1f);
                _onComplete?.Invoke();
                
                if (IsLoop)
                {
                    Elapsed = 0f;
                    return false;
                }
                
                IsCompleted = true;
                return true;
            }

            // 累计时间并触发进度
            Elapsed += deltaTime;
            _onUpdate?.Invoke(Elapsed / Duration);
            
            if (Elapsed >= Duration)
            {
                _onComplete?.Invoke();
                
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
    
    /// <summary>
    /// 计时器管理器
    /// 实现 IUpdateable 由框架自动调度
    /// </summary>
    public class TimerManager : IInitializable, IUpdateable
    {
        /// <summary>
        /// 初始化顺序（数值越小越靠前）
        /// </summary>
        public int InitOrder => -50;

        /// <summary>
        /// Update 顺序（数值越小越靠前）
        /// </summary>
        public int UpdateOrder => -100; // 优先级高，先于其他系统更新
        
        /// <summary>
        /// 计时器列表
        /// </summary>
        private List<Timer> _timers;

        /// <summary>
        /// 下一个计时器 ID
        /// </summary>
        private int _nextId = 1;

        /// <summary>
        /// 默认是否使用不受 TimeScale 影响的时间
        /// </summary>
        private bool _defaultUseUnscaledTime;
        
        /// <summary>
        /// 初始化计时器管理器
        /// </summary>
        public void Initialize()
        {
            // 允许被多次调用（例如：CY.Timer 在 CYBootstrap.InitializeAll 前被访问并提前创建）
            if (_timers != null)
            {
                return;
            }

            int initialCapacity = 32; // 初始容量
            
            // 从 CYConfigurator 读取配置
            var configurator = CYConfigurator.Instance; // 配置中心
            if (configurator != null)
            {
                var config = configurator.GetConfig<TimerManagerConfig>(); // 计时器配置
                if (config != null)
                {
                    initialCapacity = config.InitialCapacity;
                    _defaultUseUnscaledTime = config.UseUnscaledTime;
                    CYLog.Debug("[TimerManager] 使用 CYConfigurator 配置");
                }
            }
            
            _timers = new List<Timer>(initialCapacity);
            CYLog.Debug("[TimerManager] 初始化完成");
        }
        
        /// <summary>
        /// 延迟执行
        /// 默认使用配置中的 UseUnscaledTime 设置
        /// </summary>
        /// <param name="seconds">延迟秒数</param>
        /// <param name="onComplete">完成回调</param>
        /// <returns>计时器对象</returns>
        public Timer Delay(float seconds, Action onComplete)
        {
            return Delay(seconds, onComplete, _defaultUseUnscaledTime);
        }

        /// <summary>
        /// 延迟执行（可指定是否使用不随 TimeScale 变化的时间）
        /// </summary>
        public Timer Delay(float seconds, Action onComplete, bool useUnscaledTime)
        {
            var timer = new Timer(seconds, onComplete, false, useUnscaledTime) { Id = _nextId++ }; // 计时器实例
            _timers.Add(timer);
            return timer;
        }
        
        /// <summary>
        /// 延迟执行（显式指定时间模式）
        /// </summary>
        /// <summary>
        /// 延迟执行（带进度回调，进度为 0~1）
        /// </summary>
        public Timer Delay(float seconds, Action onComplete, Action<float> onProgress, bool useUnscaledTime)
        {
            var timer = new Timer(seconds, onComplete, false, useUnscaledTime) { Id = _nextId++ }; // 计时器实例
            if (onProgress != null)
            {
                timer.OnUpdate(onProgress);
            }
            _timers.Add(timer);
            return timer;
        }
        
        /// <summary>
        /// 循环执行
        /// 默认使用配置中的 UseUnscaledTime 设置
        /// </summary>
        public Timer Loop(float interval, Action onTick)
        {
            return Loop(interval, onTick, _defaultUseUnscaledTime);
        }

        /// <summary>
        /// 循环执行（可指定是否使用不随 TimeScale 变化的时间）
        /// </summary>
        public Timer Loop(float interval, Action onTick, bool useUnscaledTime)
        {
            var timer = new Timer(interval, onTick, true, useUnscaledTime) { Id = _nextId++ }; // 计时器实例
            _timers.Add(timer);
            return timer;
        }
        
        /// <summary>
        /// 循环执行（显式指定时间模式）
        /// </summary>
        /// <summary>
        /// 循环执行（带进度回调，进度为 0~1）
        /// </summary>
        public Timer Loop(float interval, Action onTick, Action<float> onProgress, bool useUnscaledTime)
        {
            var timer = new Timer(interval, onTick, true, useUnscaledTime) { Id = _nextId++ }; // 计时器实例
            if (onProgress != null)
            {
                timer.OnUpdate(onProgress);
            }
            _timers.Add(timer);
            return timer;
        }
        
        /// <summary>
        /// 下一帧执行队列
        /// </summary>
        private readonly List<Action> _nextFrameActions = new();

        /// <summary>
        /// 执行中的临时队列（避免遍历时修改集合）
        /// </summary>
        private readonly List<Action> _executingActions = new();
        
        /// <summary>
        /// 下一帧执行（真正的下一帧，非延迟）
        /// </summary>
        public void NextFrame(Action onComplete)
        {
            if (onComplete != null)
            {
                _nextFrameActions.Add(onComplete);
            }
        }
        
        /// <summary>
        /// 下一帧执行（返回 Timer 以保持兼容性）
        /// </summary>
        public Timer NextFrameTimer(Action onComplete)
        {
            return Delay(0f, onComplete, true);
        }
        
        /// <summary>
        /// 取消计时器
        /// </summary>
        public void Cancel(Timer timer)
        {
            timer?.Stop();
        }
        
        /// <summary>
        /// 通过 ID 取消计时器
        /// </summary>
        public void Cancel(int timerId)
        {
            var timer = FindTimerById(timerId); // 目标计时器
            timer?.Stop();
        }

        /// <summary>
        /// 尝试取消计时器（找不到则返回 false）
        /// </summary>
        public bool TryCancel(int timerId)
        {
            var timer = FindTimerById(timerId); // 目标计时器
            if (timer == null) return false;
            timer.Stop();
            return true;
        }
        
        /// <summary>
        /// 获取计时器
        /// </summary>
        public Timer GetTimer(int timerId)
        {
            return FindTimerById(timerId);
        }

        /// <summary>
        /// 是否存在指定计时器（未完成且未被移除）
        /// </summary>
        public bool HasTimer(int timerId)
        {
            var timer = FindTimerById(timerId); // 目标计时器
            return timer != null && !timer.IsCompleted;
        }
        
        /// <summary>
        /// 暂停计时器
        /// </summary>
        public void Pause(int timerId)
        {
            GetTimer(timerId)?.Pause();
        }
        
        /// <summary>
        /// 恢复计时器
        /// </summary>
        public void Resume(int timerId)
        {
            GetTimer(timerId)?.Resume();
        }
        
        /// <summary>
        /// 取消所有计时器
        /// </summary>
        public void CancelAll()
        {
            foreach (var timer in _timers)
            {
                // timer 为当前计时器
                timer.Stop();
            }
        }
        
        /// <summary>
        /// IUpdateable 实现 - 由框架自动调用
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            float unscaledDeltaTime = Time.unscaledDeltaTime; // 不受 TimeScale 影响的时间
            
            // 执行下一帧回调
            if (_nextFrameActions.Count > 0)
            {
                _executingActions.Clear();
                _executingActions.AddRange(_nextFrameActions);
                _nextFrameActions.Clear();
                
                foreach (var action in _executingActions)
                {
                    // action 为当前回调
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        CYLog.Error($"[TimerManager] NextFrame 回调异常: {ex.Message}");
                    }
                }
            }
            
            // 更新计时器
            for (int i = _timers.Count - 1; i >= 0; i--) // i 为索引（反向遍历便于删除）
            {
                var timer = _timers[i]; // 当前计时器
                float dt = timer.UseUnscaledTime ? unscaledDeltaTime : deltaTime; // 本次更新使用的时间步长
                if (timer.Update(dt))
                {
                    _timers.RemoveAt(i);
                }
            }
        }
        
        /// <summary>
        /// 当前激活的计时器数量
        /// </summary>
        public int ActiveCount => _timers.Count;

        /// <summary>
        /// 按 ID 查找计时器
        /// </summary>
        private Timer FindTimerById(int timerId)
        {
            if (timerId <= 0) return null;
            for (int i = 0; i < _timers.Count; i++) // i 为索引
            {
                var timer = _timers[i]; // 当前计时器
                if (timer != null && timer.Id == timerId)
                {
                    return timer;
                }
            }
            return null;
        }
    }
}
