// ============================================================================
// CYFramework 2.2 - MVVM ViewModel 基类
// 功能：数据绑定、属性变更通知
// ============================================================================

using System;
using System.Collections.Generic;
using CYFramework.Infrastructure;

namespace CYFramework.Core.UI.MVVM
{
    /// <summary>
    /// 属性变更事件参数
    /// </summary>
    public struct PropertyChangedEventArgs
    {
        /// <summary>
        /// 属性名
        /// </summary>
        public string PropertyName;

        /// <summary>
        /// 旧值
        /// </summary>
        public object OldValue;

        /// <summary>
        /// 新值
        /// </summary>
        public object NewValue;
    }
    
    /// <summary>
    /// 属性变更委托
    /// </summary>
    public delegate void PropertyChangedHandler(ref PropertyChangedEventArgs args);
    
    /// <summary>
    /// ViewModel 基类
    /// 实现属性变更通知，支持数据绑定
    /// </summary>
    public abstract class ViewModel : IDisposable
    {
        // 属性变更监听器
        /// <summary>
        /// 属性监听器：属性名 -> 处理器列表
        /// </summary>
        private readonly Dictionary<string, List<PropertyChangedHandler>> _propertyHandlers = new();
        
        // 全局变更监听器
        /// <summary>
        /// 全局监听器列表
        /// </summary>
        private readonly List<PropertyChangedHandler> _globalHandlers = new();
        
        // 属性值缓存
        /// <summary>
        /// 属性值缓存（注意装箱）
        /// </summary>
        private readonly Dictionary<string, object> _propertyValues = new();
        // ⚠️ 性能提示：字典存储 value 为 object，值类型会装箱。
        // 不适合高频刷新场景（如每帧血条），更适合低频 UI 交互/配置数据。
        
        // 是否已销毁
        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;
        
        #region 属性变更通知
        
        /// <summary>
        /// 设置属性值并通知变更
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="propertyName">属性名</param>
        /// <param name="value">新值</param>
        /// <returns>是否发生变更</returns>
        protected bool SetProperty<T>(string propertyName, T value)
        {
            // 获取旧值
            object oldValue = default(T); // 旧值缓存
            if (_propertyValues.TryGetValue(propertyName, out var cached)) // cached 为已缓存值
            {
                oldValue = cached;
            }
            
            // 比较值
            if (EqualityComparer<T>.Default.Equals((T)oldValue, value))
            {
                return false;
            }
            
            // 更新缓存
            _propertyValues[propertyName] = value;
            
            // 通知变更
            NotifyPropertyChanged(propertyName, oldValue, value);
            
            return true;
        }
        
        /// <summary>
        /// 获取属性值
        /// </summary>
        protected T GetProperty<T>(string propertyName, T defaultValue = default)
        {
            if (_propertyValues.TryGetValue(propertyName, out var value)) // value 为缓存值
            {
                return (T)value;
            }
            return defaultValue;
        }
        
        /// <summary>
        /// 通知属性变更
        /// </summary>
        protected void NotifyPropertyChanged(string propertyName, object oldValue, object newValue)
        {
            var args = new PropertyChangedEventArgs // 变更事件参数
            {
                PropertyName = propertyName,
                OldValue = oldValue,
                NewValue = newValue
            };
            
            // 调用特定属性监听器
            if (_propertyHandlers.TryGetValue(propertyName, out var handlers)) // handlers 为属性监听列表
            {
                foreach (var handler in handlers)
                {
                    // handler 为当前监听器
                    try
                    {
                        handler(ref args);
                    }
                    catch (Exception ex)
                    {
                        CYLog.Error($"[ViewModel] 属性变更处理异常: {propertyName}", ex);
                    }
                }
            }
            
            // 调用全局监听器
            foreach (var handler in _globalHandlers)
            {
                // handler 为当前监听器
                try
                {
                    handler(ref args);
                }
                catch (Exception ex)
                {
                    CYLog.Error($"[ViewModel] 全局变更处理异常: {propertyName}", ex);
                }
            }
        }
        
        #endregion
        
        #region 订阅/取消订阅
        
        /// <summary>
        /// 订阅属性变更
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="handler">处理器</param>
        public void Subscribe(string propertyName, PropertyChangedHandler handler)
        {
            if (!_propertyHandlers.TryGetValue(propertyName, out var handlers)) // handlers 为属性监听列表
            {
                handlers = new List<PropertyChangedHandler>();
                _propertyHandlers[propertyName] = handlers;
            }
            
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }
        
        /// <summary>
        /// 取消订阅属性变更
        /// </summary>
        public void Unsubscribe(string propertyName, PropertyChangedHandler handler)
        {
            if (_propertyHandlers.TryGetValue(propertyName, out var handlers)) // handlers 为属性监听列表
            {
                handlers.Remove(handler);
            }
        }
        
        /// <summary>
        /// 订阅所有属性变更
        /// </summary>
        public void SubscribeAll(PropertyChangedHandler handler)
        {
            if (!_globalHandlers.Contains(handler))
            {
                _globalHandlers.Add(handler);
            }
        }
        
        /// <summary>
        /// 取消订阅所有属性变更
        /// </summary>
        public void UnsubscribeAll(PropertyChangedHandler handler)
        {
            _globalHandlers.Remove(handler);
        }
        
        /// <summary>
        /// 清除所有订阅
        /// </summary>
        public void ClearSubscriptions()
        {
            _propertyHandlers.Clear();
            _globalHandlers.Clear();
        }
        
        #endregion
        
        #region 生命周期
        
        /// <summary>
        /// 初始化 ViewModel
        /// </summary>
        public virtual void Initialize() { }
        
        /// <summary>
        /// 销毁 ViewModel
        /// </summary>
        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            ClearSubscriptions();
            _propertyValues.Clear();
        }
        
        #endregion
    }
}

