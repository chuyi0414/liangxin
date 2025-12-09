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
        public string PropertyName;
        public object OldValue;
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
        private readonly Dictionary<string, List<PropertyChangedHandler>> _propertyHandlers = new();
        
        // 全局变更监听器
        private readonly List<PropertyChangedHandler> _globalHandlers = new();
        
        // 属性值缓存
        private readonly Dictionary<string, object> _propertyValues = new();
        
        // 是否已销毁
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
            object oldValue = default(T);
            if (_propertyValues.TryGetValue(propertyName, out var cached))
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
            if (_propertyValues.TryGetValue(propertyName, out var value))
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
            var args = new PropertyChangedEventArgs
            {
                PropertyName = propertyName,
                OldValue = oldValue,
                NewValue = newValue
            };
            
            // 调用特定属性监听器
            if (_propertyHandlers.TryGetValue(propertyName, out var handlers))
            {
                foreach (var handler in handlers)
                {
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
            if (!_propertyHandlers.TryGetValue(propertyName, out var handlers))
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
            if (_propertyHandlers.TryGetValue(propertyName, out var handlers))
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

