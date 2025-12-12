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
        public int Id { get; internal set; }
        public float Duration { get; private set; }
        public float Elapsed { get; private set; }
        public bool IsLoop { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool UseUnscaledTime { get; private set; }
        
        private Action _onComplete;
        private Action<float> _onUpdate;
        
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
        
        internal bool Update(float deltaTime)
        {
            if (IsPaused || IsCompleted) return false;
            
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
        public int InitOrder => -50;
        public int UpdateOrder => -100; // 优先级高，先于其他系统更新
        
        private List<Timer> _timers;
        private readonly List<Timer> _toRemove = new List<Timer>();
        private int _nextId = 1;
        private bool _defaultUseUnscaledTime;
        
        public void Initialize()
        {
            int initialCapacity = 32;
            
            // 从 CYConfigurator 读取配置
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                var config = configurator.GetConfig<TimerManagerConfig>();
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
        public Timer Delay(float seconds, Action onComplete)
        {
            return Delay(seconds, onComplete, _defaultUseUnscaledTime);
        }
        
        /// <summary>
        /// 延迟执行（显式指定时间模式）
        /// </summary>
        public Timer Delay(float seconds, Action onComplete, bool useUnscaledTime)
        {
            var timer = new Timer(seconds, onComplete, false, useUnscaledTime) { Id = _nextId++ };
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
        /// 循环执行（显式指定时间模式）
        /// </summary>
        public Timer Loop(float interval, Action onTick, bool useUnscaledTime)
        {
            var timer = new Timer(interval, onTick, true, useUnscaledTime) { Id = _nextId++ };
            _timers.Add(timer);
            return timer;
        }
        
        private readonly List<Action> _nextFrameActions = new();
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
            var timer = _timers.Find(t => t.Id == timerId);
            timer?.Stop();
        }
        
        /// <summary>
        /// 获取计时器
        /// </summary>
        public Timer GetTimer(int timerId)
        {
            return _timers.Find(t => t.Id == timerId);
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
                timer.Stop();
            }
        }
        
        /// <summary>
        /// IUpdateable 实现 - 由框架自动调用
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            float unscaledDeltaTime = Time.unscaledDeltaTime;
            
            // 执行下一帧回调
            if (_nextFrameActions.Count > 0)
            {
                _executingActions.Clear();
                _executingActions.AddRange(_nextFrameActions);
                _nextFrameActions.Clear();
                
                foreach (var action in _executingActions)
                {
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
            _toRemove.Clear();
            
            foreach (var timer in _timers)
            {
                float dt = timer.UseUnscaledTime ? unscaledDeltaTime : deltaTime;
                if (timer.Update(dt))
                {
                    _toRemove.Add(timer);
                }
            }
            
            foreach (var timer in _toRemove)
            {
                _timers.Remove(timer);
            }
        }
        
        public int ActiveCount => _timers.Count;
    }
}
