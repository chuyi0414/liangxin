// ============================================================================
// CYFramework - 定时器模块
// 提供延时调用、循环调用等计时功能
// 
// 设计要点：
// - 支持一次性定时器和循环定时器
// - 支持暂停时间和真实时间
// - 定时器对象可复用，减少 GC
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 定时器模块
    /// </summary>
    public class TimerModule : IModule
    {
        // ====================================================================
        // IModule 实现
        // ====================================================================

        public int Priority => 10; // 定时器优先级较高
        public bool NeedUpdate => true; // 定时器需要帧更新

        public void Initialize()
        {
            _timers = new List<TimerData>(64);
            _timersToAdd = new List<TimerData>(16);
            _nextTimerId = 1;
            Log.I("TimerModule", "初始化完成");
        }

        public void Update(float deltaTime)
        {
            // 添加待添加的定时器
            if (_timersToAdd.Count > 0)
            {
                _timers.AddRange(_timersToAdd);
                _timersToAdd.Clear();
            }

            float unscaledDeltaTime = Time.unscaledDeltaTime;

            // 更新所有定时器
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                TimerData timer = _timers[i];

                if (timer.IsPaused || timer.IsCompleted)
                    continue;

                // 根据是否使用真实时间选择 deltaTime
                float dt = timer.UseUnscaledTime ? unscaledDeltaTime : deltaTime;
                timer.ElapsedTime += dt;

                // 检查是否触发
                if (timer.ElapsedTime >= timer.Duration)
                {
                    // 执行回调
                    try
                    {
                        timer.Callback?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Log.E("TimerModule", $"定时器 {timer.Id} 回调出错", e);
                    }

                    if (timer.IsLoop)
                    {
                        // 循环定时器，重置时间
                        timer.ElapsedTime -= timer.Duration;
                        timer.LoopCount++;
                    }
                    else
                    {
                        // 一次性定时器，标记完成
                        timer.IsCompleted = true;
                    }
                }

                _timers[i] = timer;
            }

            // 移除已完成的定时器
            _timers.RemoveAll(t => t.IsCompleted);
        }

        public void Shutdown()
        {
            _timers.Clear();
            _timersToAdd.Clear();
            Log.I("TimerModule", "已关闭");
        }

        // ====================================================================
        // 定时器核心
        // ====================================================================

        private List<TimerData> _timers;
        private List<TimerData> _timersToAdd;
        private int _nextTimerId;

        /// <summary>
        /// 添加延时调用（一次性）
        /// </summary>
        /// <param name="delay">延时时间（秒）</param>
        /// <param name="callback">回调函数</param>
        /// <param name="useUnscaledTime">是否使用真实时间（不受 TimeScale 影响）</param>
        /// <returns>定时器 ID，可用于取消</returns>
        public int Delay(float delay, Action callback, bool useUnscaledTime = false)
        {
            int id = _nextTimerId++;
            TimerData timer = new TimerData
            {
                Id = id,
                Duration = delay,
                Callback = callback,
                UseUnscaledTime = useUnscaledTime,
                IsLoop = false,
                ElapsedTime = 0f,
                IsPaused = false,
                IsCompleted = false,
                LoopCount = 0
            };
            _timersToAdd.Add(timer);
            return id;
        }

        /// <summary>
        /// 添加循环调用
        /// </summary>
        /// <param name="interval">间隔时间（秒）</param>
        /// <param name="callback">回调函数</param>
        /// <param name="useUnscaledTime">是否使用真实时间</param>
        /// <returns>定时器 ID，可用于取消</returns>
        public int Loop(float interval, Action callback, bool useUnscaledTime = false)
        {
            int id = _nextTimerId++;
            TimerData timer = new TimerData
            {
                Id = id,
                Duration = interval,
                Callback = callback,
                UseUnscaledTime = useUnscaledTime,
                IsLoop = true,
                ElapsedTime = 0f,
                IsPaused = false,
                IsCompleted = false,
                LoopCount = 0
            };
            _timersToAdd.Add(timer);
            return id;
        }

        /// <summary>
        /// 取消定时器
        /// </summary>
        /// <param name="timerId">定时器 ID</param>
        public void Cancel(int timerId)
        {
            for (int i = 0; i < _timers.Count; i++)
            {
                if (_timers[i].Id == timerId)
                {
                    TimerData timer = _timers[i];
                    timer.IsCompleted = true;
                    _timers[i] = timer;
                    return;
                }
            }

            // 也检查待添加列表
            _timersToAdd.RemoveAll(t => t.Id == timerId);
        }

        /// <summary>
        /// 暂停定时器
        /// </summary>
        /// <param name="timerId">定时器 ID</param>
        public void Pause(int timerId)
        {
            for (int i = 0; i < _timers.Count; i++)
            {
                if (_timers[i].Id == timerId)
                {
                    TimerData timer = _timers[i];
                    timer.IsPaused = true;
                    _timers[i] = timer;
                    return;
                }
            }
        }

        /// <summary>
        /// 恢复定时器
        /// </summary>
        /// <param name="timerId">定时器 ID</param>
        public void Resume(int timerId)
        {
            for (int i = 0; i < _timers.Count; i++)
            {
                if (_timers[i].Id == timerId)
                {
                    TimerData timer = _timers[i];
                    timer.IsPaused = false;
                    _timers[i] = timer;
                    return;
                }
            }
        }

        /// <summary>
        /// 取消所有定时器
        /// </summary>
        public void CancelAll()
        {
            _timers.Clear();
            _timersToAdd.Clear();
        }

        /// <summary>
        /// 获取活跃定时器数量
        /// </summary>
        public int ActiveTimerCount => _timers.Count + _timersToAdd.Count;
    }

    /// <summary>
    /// 定时器数据结构
    /// </summary>
    internal struct TimerData
    {
        public int Id;
        public float Duration;
        public Action Callback;
        public bool UseUnscaledTime;
        public bool IsLoop;
        public float ElapsedTime;
        public bool IsPaused;
        public bool IsCompleted;
        public int LoopCount;
    }
}
