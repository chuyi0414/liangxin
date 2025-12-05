// ============================================================================
// CYFramework - 分帧调度器模块
// 将大任务拆成多帧执行，减小单帧耗时波动
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 分帧任务状态
    /// </summary>
    public enum ScheduledTaskState
    {
        Pending,
        Running,
        Completed,
        Cancelled
    }

    /// <summary>
    /// 分帧任务
    /// </summary>
    public class ScheduledTask
    {
        public int Id { get; internal set; }
        public ScheduledTaskState State { get; internal set; }
        public int Priority { get; set; }
        public float Progress { get; internal set; }
        public Action OnComplete { get; set; }
        internal IEnumerator<float> Enumerator;
    }

    /// <summary>
    /// 分帧调度器模块
    /// </summary>
    public class SchedulerModule : IModule
    {
        public int Priority => 12;
        public bool NeedUpdate => true;

        private List<ScheduledTask> _pendingTasks;
        private ScheduledTask _currentTask;
        private int _nextTaskId = 1;
        private float _maxFrameTimeMs = 5f;

        /// <summary>
        /// 每帧最大执行时间（毫秒）
        /// </summary>
        public float MaxTimePerFrameMs
        {
            get => _maxFrameTimeMs;
            set => _maxFrameTimeMs = Mathf.Max(0.1f, value);
        }

        public void Initialize()
        {
            _pendingTasks = new List<ScheduledTask>(16);
            Log.I("SchedulerModule", "初始化完成");
        }

        public void Update(float deltaTime)
        {
            float startTime = Time.realtimeSinceStartup;
            float maxTime = _maxFrameTimeMs / 1000f;

            while (_currentTask != null || _pendingTasks.Count > 0)
            {
                if (Time.realtimeSinceStartup - startTime > maxTime)
                    break;

                if (_currentTask == null)
                {
                    if (_pendingTasks.Count == 0) break;
                    _pendingTasks.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                    _currentTask = _pendingTasks[0];
                    _pendingTasks.RemoveAt(0);
                    _currentTask.State = ScheduledTaskState.Running;
                }

                if (_currentTask.Enumerator.MoveNext())
                {
                    _currentTask.Progress = _currentTask.Enumerator.Current;
                }
                else
                {
                    _currentTask.State = ScheduledTaskState.Completed;
                    _currentTask.Progress = 1f;
                    _currentTask.OnComplete?.Invoke();
                    _currentTask = null;
                }
            }
        }

        public void Shutdown()
        {
            CancelAll();
            Log.I("SchedulerModule", "已关闭");
        }

        /// <summary>
        /// 设置每帧最大执行时间（毫秒）
        /// </summary>
        public void SetMaxFrameTime(float milliseconds)
        {
            _maxFrameTimeMs = Mathf.Max(0.1f, milliseconds);
        }

        /// <summary>
        /// 调度分帧任务
        /// </summary>
        public ScheduledTask Schedule(Func<IEnumerator<float>> taskFunc, int priority = 0, Action onComplete = null)
        {
            var task = new ScheduledTask
            {
                Id = _nextTaskId++,
                State = ScheduledTaskState.Pending,
                Priority = priority,
                Progress = 0f,
                OnComplete = onComplete,
                Enumerator = taskFunc()
            };
            _pendingTasks.Add(task);
            return task;
        }

        /// <summary>
        /// 调度批量处理任务
        /// </summary>
        public ScheduledTask ScheduleBatch<T>(IList<T> items, Action<T> processAction, 
            int itemsPerFrame = 10, Action onComplete = null)
        {
            return Schedule(() => BatchProcess(items, processAction, itemsPerFrame), 0, onComplete);
        }

        private IEnumerator<float> BatchProcess<T>(IList<T> items, Action<T> processAction, int itemsPerFrame)
        {
            int count = items.Count;
            int processed = 0;

            while (processed < count)
            {
                int end = Mathf.Min(processed + itemsPerFrame, count);
                for (int i = processed; i < end; i++)
                {
                    processAction(items[i]);
                }
                processed = end;
                yield return (float)processed / count;
            }
        }

        /// <summary>
        /// 取消任务
        /// </summary>
        public void Cancel(int taskId)
        {
            if (_currentTask != null && _currentTask.Id == taskId)
            {
                _currentTask.State = ScheduledTaskState.Cancelled;
                _currentTask = null;
                return;
            }

            for (int i = _pendingTasks.Count - 1; i >= 0; i--)
            {
                if (_pendingTasks[i].Id == taskId)
                {
                    _pendingTasks[i].State = ScheduledTaskState.Cancelled;
                    _pendingTasks.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// 取消所有任务
        /// </summary>
        public void CancelAll()
        {
            if (_currentTask != null)
            {
                _currentTask.State = ScheduledTaskState.Cancelled;
                _currentTask = null;
            }
            foreach (var task in _pendingTasks)
            {
                task.State = ScheduledTaskState.Cancelled;
            }
            _pendingTasks.Clear();
        }

        /// <summary>
        /// 待执行任务数量
        /// </summary>
        public int PendingCount => _pendingTasks.Count + (_currentTask != null ? 1 : 0);
    }
}
