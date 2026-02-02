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
        /// <summary>
        /// 优先级（数值越小越先执行）
        /// </summary>
        public int Priority { get; }
        
        /// <summary>
        /// 事件优先级特性
        /// </summary>
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
        /// <summary>
        /// 优先级（数值越小越先执行）
        /// </summary>
        public int Priority { get; }
        
        /// <summary>
        /// 自动订阅特性
        /// </summary>
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
        /// <summary>
        /// 事件处理委托
        /// </summary>
        public Delegate Handler;

        /// <summary>
        /// 优先级（数值越小越先执行）
        /// </summary>
        public int Priority;

        /// <summary>
        /// 订阅者对象（用于自动解绑）
        /// </summary>
        public object Target;  // 用于自动解绑

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// 所属事件类型（用于快速定位移除）
        /// </summary>
        public Type EventType;  // 所属事件类型（用于快速定位移除）
    }
    
    /// <summary>
    /// 延迟事件
    /// </summary>
    internal struct DelayedEvent
    {
        /// <summary>
        /// 事件类型
        /// </summary>
        public Type EventType;

        /// <summary>
        /// 事件数据（装箱）
        /// </summary>
        public object EventData;

        /// <summary>
        /// 剩余帧数
        /// </summary>
        public int FramesRemaining;
    }
    
    /// <summary>
    /// EventBus 零 GC 事件总线
    /// </summary>
    public class EventBus : IInitializable, ITickable, IDisposableEx
    {
        // 事件订阅表
        /// <summary>
        /// 事件订阅表：事件类型 -> 订阅列表
        /// </summary>
        private readonly Dictionary<Type, List<EventSubscription>> _subscriptions = new();
        
        // 延迟事件队列
        /// <summary>
        /// 延迟事件队列
        /// </summary>
        private readonly List<DelayedEvent> _delayedEvents = new();
        
        // 订阅者对象到订阅列表的映射（用于自动解绑）
        /// <summary>
        /// 订阅者 -> 订阅列表映射
        /// </summary>
        private readonly Dictionary<object, List<EventSubscription>> _targetSubscriptions = new();
        
        // 临时列表池，支持递归派发（避免递归时共用一个 tempHandlers 导致崩溃）
        /// <summary>
        /// 临时列表池（用于派发时复制列表，避免递归冲突）
        /// </summary>
        private readonly Stack<List<EventSubscription>> _tempListsPool = new();
        
        // 待移除的订阅
        /// <summary>
        /// 派发中待移除的订阅列表
        /// </summary>
        private readonly List<EventSubscription> _pendingRemove = new();
        
        // 派发深度
        /// <summary>
        /// 当前派发深度
        /// </summary>
        private int _dispatchingDepth = 0;
        
        // 延迟事件反射缓存（避免每次 GetMethod/MakeGenericMethod）
        /// <summary>
        /// 延迟派发使用的非泛型方法缓存
        /// </summary>
        private System.Reflection.MethodInfo _postBoxedMethod;

        /// <summary>
        /// 延迟派发泛型方法缓存
        /// </summary>
        private readonly Dictionary<Type, System.Reflection.MethodInfo> _postBoxedGenericCache = new();

        /// <summary>
        /// 反射调用参数缓存（避免重复分配数组）
        /// </summary>
        private readonly object[] _postBoxedInvokeArgs = new object[1];

        /// <summary>
        /// SubscribeAll 扫描结果缓存
        /// </summary>
        private readonly Dictionary<Type, List<(Type eventType, System.Reflection.MethodInfo method, int priority)>> _subscribeAllCache = new();

        /// <summary>
        /// Subscribe 泛型定义缓存
        /// </summary>
        private System.Reflection.MethodInfo _subscribeGenericDefinition;

        /// <summary>
        /// Subscribe 泛型方法缓存
        /// </summary>
        private readonly Dictionary<Type, System.Reflection.MethodInfo> _subscribeGenericCache = new();
        
        /// <summary>
        /// 初始化顺序（数值越小越靠前）
        /// </summary>
        public int InitOrder => -100; // 最先初始化

        /// <summary>
        /// Tick 顺序（数值越小越靠前）
        /// </summary>
        public int TickOrder => -100; // 最先 Tick

        /// <summary>
        /// 释放顺序（数值越小越靠前）
        /// </summary>
        public int DisposeOrder => 100; // 最后销毁
        
        #region 生命周期
        
        /// <summary>
        /// 初始化事件总线
        /// </summary>
        public void Initialize()
        {
            CYLog.Debug("[EventBus] 初始化完成");
        }
        
        /// <summary>
        /// Tick 驱动（处理延迟事件与延迟移除）
        /// </summary>
        public void Tick(float deltaTime)
        {
            // 处理延迟事件
            ProcessDelayedEvents();
            
            // 清理待移除的订阅
            ProcessPendingRemove();
        }
        
        /// <summary>
        /// 释放事件总线
        /// </summary>
        public void Dispose()
        {
            _subscriptions.Clear();
            _delayedEvents.Clear();
            _targetSubscriptions.Clear();
            _subscribeAllCache.Clear();
            _subscribeGenericCache.Clear();
            _tempListsPool.Clear();
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
            var eventType = typeof(T); // 事件类型
            
            if (!_subscriptions.TryGetValue(eventType, out var list)) // list 为订阅列表
            {
                list = new List<EventSubscription>(8);
                _subscriptions[eventType] = list;
            }
            
            // 检查重复订阅
            foreach (var sub in list)
            {
                // sub 为当前订阅
                if (sub.Handler.Equals(handler) && sub.IsActive)
                {
                    CYLog.Warning($"[EventBus] 重复订阅: {eventType.Name}");
                    return;
                }
            }
            
            var subscription = new EventSubscription // 新增订阅记录
            {
                Handler = handler,
                Priority = priority,
                Target = target,
                IsActive = true,
                EventType = eventType  // 记录所属事件类型，用于快速定位移除
            };
            
            // 按优先级插入
            int insertIndex = list.Count; // 默认插入末尾
            for (int i = 0; i < list.Count; i++) // i 为索引
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
                if (!_targetSubscriptions.TryGetValue(target, out var targetList)) // targetList 为目标订阅列表
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
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        public void Unsubscribe<T>(EventHandler<T> handler) where T : struct
        {
            var eventType = typeof(T); // 事件类型
            
            if (!_subscriptions.TryGetValue(eventType, out var list)) return; // list 为订阅列表

            for (int i = 0; i < list.Count; i++) // i 为索引
            {
                var sub = list[i]; // 当前订阅
                if (!sub.Handler.Equals(handler))
                {
                    continue;
                }

                // 同步清理 target -> subscriptions 映射，避免悬挂引用导致内存与逻辑噪音。
                if (sub.Target != null && _targetSubscriptions.TryGetValue(sub.Target, out var targetList)) // targetList 为目标订阅列表
                {
                    targetList.Remove(sub);
                    if (targetList.Count == 0)
                    {
                        _targetSubscriptions.Remove(sub.Target);
                    }
                }

                if (_dispatchingDepth > 0)
                {
                    // 派发中标记为待移除
                    sub.IsActive = false;
                    _pendingRemove.Add(sub);
                }
                else
                {
                    list.RemoveAt(i);
                }
                break;
            }
        }
        
        /// <summary>
        /// 取消指定对象的所有订阅（自动解绑）
        /// </summary>
        public void UnsubscribeAll(object target)
        {
            if (target == null) return;
            
            if (!_targetSubscriptions.TryGetValue(target, out var subscriptions)) return; // subscriptions 为目标订阅列表
            
            foreach (var sub in subscriptions)
            {
                // sub 为当前订阅
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

            var type = target.GetType(); // 目标类型
            var handlers = GetOrCreateSubscribeAllCache(type); // 缓存的订阅入口
            if (handlers == null || handlers.Count == 0) return;

            for (int i = 0; i < handlers.Count; i++) // i 为索引
            {
                var entry = handlers[i]; // 订阅条目
                try
                {
                    var handlerType = typeof(EventHandler<>).MakeGenericType(entry.eventType); // 目标委托类型
                    var handler = Delegate.CreateDelegate(handlerType, target, entry.method); // 事件处理委托

                    var subscribeMethod = GetOrCreateSubscribeGenericMethod(entry.eventType); // Subscribe<T> 方法
                    subscribeMethod.Invoke(this, new object[] { handler, target, entry.priority });

                    CYLog.Debug($"[EventBus] 自动订阅: {type.Name}.{entry.method.Name} -> {entry.eventType.Name}");
                }
                catch (Exception e)
                {
                    CYLog.Warning($"[EventBus] 自动订阅失败: {entry.method.Name}, {e.Message}");
                }
            }
        }

        /// <summary>
        /// 获取或创建 SubscribeAll 的扫描缓存
        /// </summary>
        private List<(Type eventType, System.Reflection.MethodInfo method, int priority)> GetOrCreateSubscribeAllCache(Type targetType)
        {
            if (_subscribeAllCache.TryGetValue(targetType, out var cached)) // cached 为已缓存条目
            {
                return cached;
            }

            var result = new List<(Type eventType, System.Reflection.MethodInfo method, int priority)>(); // 扫描结果

            // 目标方法列表
            var methods = targetType.GetMethods(System.Reflection.BindingFlags.Instance |
                                                System.Reflection.BindingFlags.Public |
                                                System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++) // i 为索引
            {
                var method = methods[i]; // 当前方法
                var attr = method.GetCustomAttributes(typeof(OnEventAttribute), true); // 特性数组
                if (attr == null || attr.Length == 0) continue;

                var onEvent = (OnEventAttribute)attr[0]; // OnEvent 特性
                var parameters = method.GetParameters(); // 参数列表

                if (parameters.Length != 1) continue;
                if (!parameters[0].ParameterType.IsByRef) continue;

                var eventType = parameters[0].ParameterType.GetElementType(); // 事件类型
                if (eventType == null || !eventType.IsValueType) continue;

                result.Add((eventType, method, onEvent.Priority));
            }

            _subscribeAllCache[targetType] = result;
            return result;
        }

        /// <summary>
        /// 获取或创建 Subscribe 的泛型方法缓存
        /// </summary>
        private System.Reflection.MethodInfo GetOrCreateSubscribeGenericMethod(Type eventType)
        {
            if (_subscribeGenericCache.TryGetValue(eventType, out var cached)) // cached 为已缓存方法
            {
                return cached;
            }

            if (_subscribeGenericDefinition == null)
            {
                _subscribeGenericDefinition = typeof(EventBus).GetMethod(nameof(Subscribe));
            }

            var method = _subscribeGenericDefinition.MakeGenericMethod(eventType); // 目标泛型方法
            _subscribeGenericCache[eventType] = method;
            return method;
        }
        
        #endregion
        
        #region 派发 API

        /// <summary>
        /// 是否存在事件订阅者（用于避免无意义的构造/派发）
        /// </summary>
        public bool HasSubscribers<T>() where T : struct
        {
            return _subscriptions.TryGetValue(typeof(T), out var list) && list != null && list.Count > 0; // list 为订阅列表
        }

        /// <summary>
        /// 尝试派发事件（无订阅者则返回 false）
        /// </summary>
        public bool TryPost<T>(ref T evt) where T : struct
        {
            if (!HasSubscribers<T>())
            {
                return false;
            }

            Post(ref evt);
            return true;
        }

        /// <summary>
        /// 下一帧派发事件。
        /// 注意：延迟事件会产生装箱，仅建议低频使用；高频请改用同步 Post 或 Timer 驱动的业务逻辑。
        /// </summary>
        public void PostNextFrame<T>(T evt) where T : struct
        {
            PostDelayed(evt, frames: 1);
        }
        
        /// <summary>
        /// 立即派发事件
        /// </summary>
        public void Post<T>(ref T evt) where T : struct
        {
            var eventType = typeof(T); // 事件类型
            
            if (!_subscriptions.TryGetValue(eventType, out var list)) return; // list 为订阅列表
            if (list.Count == 0) return;
            
            // 获取临时列表（从池中取或新建）
            List<EventSubscription> tempHandlers; // 临时处理列表
            if (_tempListsPool.Count > 0)
            {
                tempHandlers = _tempListsPool.Pop();
            }
            else
            {
                tempHandlers = new List<EventSubscription>(8);
            }

            _dispatchingDepth++;
            
            // 复制到临时列表
            tempHandlers.AddRange(list);
            
            try 
            {
                // 遍历执行（使用 for 循环稍微比 foreach 快一点点，且避免 Enumerator 分配，虽然 List.Enumerator 是 struct）
                // 这里用 foreach 保持原样也行，但既然改了就安全第一
                foreach (var sub in tempHandlers)
                {
                    // sub 为当前订阅
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
            }
            finally
            {
                // 清理并归还列表到池中
                tempHandlers.Clear();
                _tempListsPool.Push(tempHandlers);
                _dispatchingDepth--;
            }
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
                var mutableEvt = evt; // 拷贝为可变值
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
        /// ❗ 注意：延迟事件会产生装箱，不建议在高频场景大量使用（建议业务控制在每帧少量触发）
        /// </summary>
        private void ProcessDelayedEvents()
        {
            for (int i = _delayedEvents.Count - 1; i >= 0; i--) // i 为索引（反向遍历便于删除）
            {
                var delayed = _delayedEvents[i]; // 当前延迟事件
                delayed.FramesRemaining--;
                
                if (delayed.FramesRemaining <= 0)
                {
                    // 使用缓存的反射信息，避免每次 GetMethod/MakeGenericMethod
                    var genericMethod = GetOrCreatePostBoxedMethod(delayed.EventType); // 反射方法

                    // 避免每次 new object[1] 产生 GC（延迟事件仍然会装箱，但至少不额外分配参数数组）。
                    _postBoxedInvokeArgs[0] = delayed.EventData;
                    genericMethod.Invoke(this, _postBoxedInvokeArgs);
                    _postBoxedInvokeArgs[0] = null;
                    
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
            if (_postBoxedGenericCache.TryGetValue(eventType, out var cached)) // cached 为已缓存方法
            {
                return cached;
            }
            
            // 缓存基础方法
            if (_postBoxedMethod == null)
            {
                _postBoxedMethod = typeof(EventBus).GetMethod(nameof(PostBoxed), 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }
            
            var genericMethod = _postBoxedMethod.MakeGenericMethod(eventType); // 泛型方法
            _postBoxedGenericCache[eventType] = genericMethod;
            return genericMethod;
        }
        
        /// <summary>
        /// 装箱版本的 Post（仅用于延迟事件）
        /// </summary>
        private void PostBoxed<T>(object evt) where T : struct
        {
            var unboxed = (T)evt; // 反装箱为结构体
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
                // sub 为待移除订阅
                // 使用 EventType 直接定位列表，O(1) 查找 + O(n) 移除
                if (sub.EventType != null && _subscriptions.TryGetValue(sub.EventType, out var list)) // list 为订阅列表
                {
                    list.Remove(sub);
                }
            }
            
            _pendingRemove.Clear();
        }
        
        #endregion
    }
}
