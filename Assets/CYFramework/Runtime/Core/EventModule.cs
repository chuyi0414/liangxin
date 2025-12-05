// ============================================================================
// CYFramework - 事件模块
// 提供模块与业务之间的解耦通讯机制
// 
// 设计要点：
// - 使用强类型事件，避免装箱和反射
// - 支持多监听者
// - 避免闭包导致的隐藏分配
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 事件模块
    /// 全局事件发布/订阅中心
    /// </summary>
    public class EventModule : IModule
    {
        // ====================================================================
        // IModule 实现
        // ====================================================================

        public int Priority => 0; // 事件模块优先级最高
        public bool NeedUpdate => false; // 事件模块不需要帧更新

        public void Initialize()
        {
            Log.I("EventModule", "初始化完成");
        }

        public void Update(float deltaTime) { }

        public void Shutdown()
        {
            Clear();
            Log.I("EventModule", "已关闭");
        }

        // ====================================================================
        // 事件系统核心
        // ====================================================================

        // 存储所有事件的监听者列表
        // Key: 事件类型, Value: 该事件的所有监听者
        private readonly Dictionary<Type, Delegate> _eventTable = new Dictionary<Type, Delegate>();

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="T">事件类型（建议使用 struct）</typeparam>
        /// <param name="handler">事件处理函数</param>
        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            Type eventType = typeof(T);

            if (_eventTable.TryGetValue(eventType, out Delegate existingDelegate))
            {
                _eventTable[eventType] = Delegate.Combine(existingDelegate, handler);
            }
            else
            {
                _eventTable[eventType] = handler;
            }
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">事件处理函数</param>
        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            Type eventType = typeof(T);

            if (_eventTable.TryGetValue(eventType, out Delegate existingDelegate))
            {
                Delegate newDelegate = Delegate.Remove(existingDelegate, handler);
                if (newDelegate == null)
                {
                    _eventTable.Remove(eventType);
                }
                else
                {
                    _eventTable[eventType] = newDelegate;
                }
            }
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="eventData">事件数据</param>
        public void Publish<T>(T eventData) where T : struct
        {
            Type eventType = typeof(T);

            if (_eventTable.TryGetValue(eventType, out Delegate existingDelegate))
            {
                Action<T> callback = existingDelegate as Action<T>;
                if (callback != null)
                {
                    try
                    {
                        callback.Invoke(eventData);
                    }
                    catch (Exception e)
                    {
                        Log.E("EventModule", $"事件 {eventType.Name} 处理出错", e);
                    }
                }
            }
        }

        /// <summary>
        /// 检查是否有监听者
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <returns>是否有监听者</returns>
        public bool HasListeners<T>() where T : struct
        {
            return _eventTable.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 清除所有事件监听
        /// </summary>
        public void Clear()
        {
            _eventTable.Clear();
        }

        /// <summary>
        /// 清除指定类型的事件监听
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        public void Clear<T>() where T : struct
        {
            _eventTable.Remove(typeof(T));
        }
    }

    // ========================================================================
    // 示例事件定义（实际项目中应放到单独文件）
    // ========================================================================

    /// <summary>
    /// 示例：游戏开始事件
    /// </summary>
    public struct GameStartEvent
    {
        public int LevelId;
        public float StartTime;
    }

    /// <summary>
    /// 示例：游戏结束事件
    /// </summary>
    public struct GameEndEvent
    {
        public bool IsWin;
        public int Score;
    }

    /// <summary>
    /// 示例：伤害事件
    /// </summary>
    public struct DamageEvent
    {
        public int AttackerId;
        public int TargetId;
        public int Damage;
        public bool IsCritical;
    }
}
