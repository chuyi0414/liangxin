// ============================================================================
// CYFramework 2.2 - EventBus 零 GC 事件系统
// 文档位置：3.1.2 基础设施 - EventBus
// 功能：零 GC 结构体事件、优先级、延迟派发、自动解绑
// ============================================================================

using System;
using System.Collections.Generic;
using CYFramework.Infrastructure;

namespace CYFramework.Core.Event
{
    /// <summary>
    /// 事件处理器委托（泛型结构体事件）
    /// </summary>
    public delegate void EventHandler<T>(ref T evt) where T : struct;
    
    /// <summary>
    /// 事件优先级特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class EventPriorityAttribute : Attribute
    {
        public int Priority { get; }
        
        public EventPriorityAttribute(int priority = 0)
        {
            Priority = priority;
        }
    }
    
    /// <summary>
    /// 标记方法自动订阅事件
    /// 方法签名必须为: void MethodName(ref TEvent evt)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class OnEventAttribute : Attribute
    {
        public int Priority { get; }
        
        public OnEventAttribute(int priority = 0)
        {
            Priority = priority;
        }
    }
    
    /// <summary>
    /// 事件订阅信息
    /// </summary>
    internal class EventSubscription
    {
        public Delegate Handler;
        public int Priority;
        public object Target;  // 用于自动解绑
        public bool IsActive;
        public Type EventType;  // 所属事件类型（用于快速定位移除）
    }
    
    /// <summary>
    /// 延迟事件
    /// </summary>
    internal struct DelayedEvent
    {
        public Type EventType;
        public object EventData;
        public int FramesRemaining;
    }
    
    /// <summary>
    /// EventBus 零 GC 事件总线
    /// </summary>
    public class EventBus : IInitializable, ITickable, IDisposableEx
    {
        // 事件订阅表
        private readonly Dictionary<Type, List<EventSubscription>> _subscriptions = new();
        
        // 延迟事件队列
        private readonly List<DelayedEvent> _delayedEvents = new();
        
        // 订阅者对象到订阅列表的映射（用于自动解绑）
        private readonly Dictionary<object, List<EventSubscription>> _targetSubscriptions = new();
        
        // 临时列表，避免迭代时修改
        private readonly List<EventSubscription> _tempHandlers = new();
        
        // 待移除的订阅
        private readonly List<EventSubscription> _pendingRemove = new();
        
        // 是否正在派发事件
        private bool _isDispatching;
        
        // 延迟事件反射缓存（避免每次 GetMethod/MakeGenericMethod）
        private System.Reflection.MethodInfo _postBoxedMethod;
        private readonly Dictionary<Type, System.Reflection.MethodInfo> _postBoxedGenericCache = new();
        
        public int InitOrder => -100; // 最先初始化
        public int TickOrder => -100; // 最先 Tick
        public int DisposeOrder => 100; // 最后销毁
        
        #region 生命周期
        
        public void Initialize()
        {
            CYLog.Debug("[EventBus] 初始化完成");
        }
        
        public void Tick(float deltaTime)
        {
            // 处理延迟事件
            ProcessDelayedEvents();
            
            // 清理待移除的订阅
            ProcessPendingRemove();
        }
        
        public void Dispose()
        {
            _subscriptions.Clear();
            _delayedEvents.Clear();
            _targetSubscriptions.Clear();
            CYLog.Debug("[EventBus] 已销毁");
        }
        
        #endregion
        
        #region 订阅 API
        
        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <param name="handler">事件处理器</param>
        /// <param name="target">订阅者对象（用于自动解绑）</param>
        /// <param name="priority">优先级（数值越小越先执行）</param>
        public void Subscribe<T>(EventHandler<T> handler, object target = null, int priority = 0) where T : struct
        {
            var eventType = typeof(T);
            
            if (!_subscriptions.TryGetValue(eventType, out var list))
            {
                list = new List<EventSubscription>(8);
                _subscriptions[eventType] = list;
            }
            
            // 检查重复订阅
            foreach (var sub in list)
            {
                if (sub.Handler.Equals(handler) && sub.IsActive)
                {
                    CYLog.Warning($"[EventBus] 重复订阅: {eventType.Name}");
                    return;
                }
            }
            
            var subscription = new EventSubscription
            {
                Handler = handler,
                Priority = priority,
                Target = target,
                IsActive = true,
                EventType = eventType  // 记录所属事件类型，用于快速定位移除
            };
            
            // 按优先级插入
            int insertIndex = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Priority > priority)
                {
                    insertIndex = i;
                    break;
                }
            }
            list.Insert(insertIndex, subscription);
            
            // 记录订阅者映射（用于自动解绑）
            if (target != null)
            {
                if (!_targetSubscriptions.TryGetValue(target, out var targetList))
                {
                    targetList = new List<EventSubscription>(4);
                    _targetSubscriptions[target] = targetList;
                }
                targetList.Add(subscription);
            }
        }
        
        /// <summary>
        /// 取消订阅
        /// </summary>
        public void Unsubscribe<T>(EventHandler<T> handler) where T : struct
        {
            var eventType = typeof(T);
            
            if (!_subscriptions.TryGetValue(eventType, out var list)) return;
            
            foreach (var sub in list)
            {
                if (sub.Handler.Equals(handler))
                {
                    if (_isDispatching)
                    {
                        // 派发中标记为待移除
                        sub.IsActive = false;
                        _pendingRemove.Add(sub);
                    }
                    else
                    {
                        list.Remove(sub);
                    }
                    break;
                }
            }
        }
        
        /// <summary>
        /// 取消指定对象的所有订阅（自动解绑）
        /// </summary>
        public void UnsubscribeAll(object target)
        {
            if (target == null) return;
            
            if (!_targetSubscriptions.TryGetValue(target, out var subscriptions)) return;
            
            foreach (var sub in subscriptions)
            {
                sub.IsActive = false;
                _pendingRemove.Add(sub);
            }
            
            _targetSubscriptions.Remove(target);
        }
        
        /// <summary>
        /// 自动扫描并订阅所有标记 [OnEvent] 的方法
        /// </summary>
        public void SubscribeAll(object target)
        {
            if (target == null) return;
            
            var type = target.GetType();
            var methods = type.GetMethods(System.Reflection.BindingFlags.Instance | 
                                           System.Reflection.BindingFlags.Public | 
                                           System.Reflection.BindingFlags.NonPublic);
            
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttributes(typeof(OnEventAttribute), true);
                if (attr.Length == 0) continue;
                
                var onEvent = (OnEventAttribute)attr[0];
                var parameters = method.GetParameters();
                
                // 验证方法签名: void Method(ref TEvent evt)
                if (parameters.Length != 1) continue;
                if (!parameters[0].ParameterType.IsByRef) continue;
                
                var eventType = parameters[0].ParameterType.GetElementType();
                if (eventType == null || !eventType.IsValueType) continue;
                
                try
                {
                    // 创建委托
                    var handlerType = typeof(EventHandler<>).MakeGenericType(eventType);
                    var handler = Delegate.CreateDelegate(handlerType, target, method);
                    
                    // 调用泛型 Subscribe 方法
                    var subscribeMethod = typeof(EventBus).GetMethod("Subscribe")
                        ?.MakeGenericMethod(eventType);
                    subscribeMethod?.Invoke(this, new object[] { handler, target, onEvent.Priority });
                    
                    CYLog.Debug($"[EventBus] 自动订阅: {type.Name}.{method.Name} -> {eventType.Name}");
                }
                catch (Exception e)
                {
                    CYLog.Warning($"[EventBus] 自动订阅失败: {method.Name}, {e.Message}");
                }
            }
        }
        
        #endregion
        
        #region 派发 API
        
        /// <summary>
        /// 立即派发事件
        /// </summary>
        public void Post<T>(ref T evt) where T : struct
        {
            var eventType = typeof(T);
            
            if (!_subscriptions.TryGetValue(eventType, out var list)) return;
            if (list.Count == 0) return;
            
            _isDispatching = true;
            
            // 复制到临时列表，避免迭代时修改问题
            _tempHandlers.Clear();
            _tempHandlers.AddRange(list);
            
            foreach (var sub in _tempHandlers)
            {
                if (!sub.IsActive) continue;
                
                try
                {
                    ((EventHandler<T>)sub.Handler)(ref evt);
                }
                catch (Exception ex)
                {
                    CYLog.Error($"[EventBus] 事件处理异常: {eventType.Name}", ex);
                }
            }
            
            _isDispatching = false;
        }
        
        /// <summary>
        /// 延迟派发事件
        /// </summary>
        /// <param name="evt">事件数据</param>
        /// <param name="frames">延迟帧数</param>
        public void PostDelayed<T>(T evt, int frames = 1) where T : struct
        {
            if (frames <= 0)
            {
                var mutableEvt = evt;
                Post(ref mutableEvt);
                return;
            }
            
            _delayedEvents.Add(new DelayedEvent
            {
                EventType = typeof(T),
                EventData = evt,  // 装箱，但延迟事件较少使用
                FramesRemaining = frames
            });
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 处理延迟事件
        /// ❗ 注意：延迟事件会产生装箱，不建议在高频场景大量使用
        /// </summary>
        private void ProcessDelayedEvents()
        {
            for (int i = _delayedEvents.Count - 1; i >= 0; i--)
            {
                var delayed = _delayedEvents[i];
                delayed.FramesRemaining--;
                
                if (delayed.FramesRemaining <= 0)
                {
                    // 使用缓存的反射信息，避免每次 GetMethod/MakeGenericMethod
                    var genericMethod = GetOrCreatePostBoxedMethod(delayed.EventType);
                    genericMethod.Invoke(this, new[] { delayed.EventData });
                    
                    _delayedEvents.RemoveAt(i);
                }
                else
                {
                    _delayedEvents[i] = delayed;
                }
            }
        }
        
        /// <summary>
        /// 获取或创建 PostBoxed 泛型方法缓存
        /// </summary>
        private System.Reflection.MethodInfo GetOrCreatePostBoxedMethod(Type eventType)
        {
            if (_postBoxedGenericCache.TryGetValue(eventType, out var cached))
            {
                return cached;
            }
            
            // 缓存基础方法
            if (_postBoxedMethod == null)
            {
                _postBoxedMethod = typeof(EventBus).GetMethod(nameof(PostBoxed), 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }
            
            var genericMethod = _postBoxedMethod.MakeGenericMethod(eventType);
            _postBoxedGenericCache[eventType] = genericMethod;
            return genericMethod;
        }
        
        /// <summary>
        /// 装箱版本的 Post（仅用于延迟事件）
        /// </summary>
        private void PostBoxed<T>(object evt) where T : struct
        {
            var unboxed = (T)evt;
            Post(ref unboxed);
        }
        
        /// <summary>
        /// 处理待移除的订阅
        /// 使用 EventSubscription.EventType 直接定位列表，避免全量遍历
        /// </summary>
        private void ProcessPendingRemove()
        {
            if (_pendingRemove.Count == 0) return;
            
            foreach (var sub in _pendingRemove)
            {
                // 使用 EventType 直接定位列表，O(1) 查找 + O(n) 移除
                if (sub.EventType != null && _subscriptions.TryGetValue(sub.EventType, out var list))
                {
                    list.Remove(sub);
                }
            }
            
            _pendingRemove.Clear();
        }
        
        #endregion
    }
}
