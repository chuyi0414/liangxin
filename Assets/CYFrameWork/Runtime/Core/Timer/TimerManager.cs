// ============================================================================
// CYFramework - 计时器管理器
// ============================================================================

using System;
using System.Collections.Generic;
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
    public class TimerManager : IUpdateable
    {
        public int UpdateOrder => -100; // 优先级高，先于其他系统更新
        private readonly List<Timer> _timers = new List<Timer>();
        private readonly List<Timer> _toRemove = new List<Timer>();
        private int _nextId = 1;
        
        /// <summary>
        /// 延迟执行
        /// </summary>
        public Timer Delay(float seconds, Action onComplete, bool useUnscaledTime = false)
        {
            var timer = new Timer(seconds, onComplete, false, useUnscaledTime) { Id = _nextId++ };
            _timers.Add(timer);
            return timer;
        }
        
        /// <summary>
        /// 循环执行
        /// </summary>
        public Timer Loop(float interval, Action onTick, bool useUnscaledTime = false)
        {
            var timer = new Timer(interval, onTick, true, useUnscaledTime) { Id = _nextId++ };
            _timers.Add(timer);
            return timer;
        }
        
        /// <summary>
        /// 下一帧执行
        /// </summary>
        public Timer NextFrame(Action onComplete)
        {
            return Delay(0.001f, onComplete);
        }
        
        /// <summary>
        /// 取消计时器
        /// </summary>
        public void Cancel(Timer timer)
        {
            timer?.Stop();
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
