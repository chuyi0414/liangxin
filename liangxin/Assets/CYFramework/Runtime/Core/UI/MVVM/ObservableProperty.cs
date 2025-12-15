using System;
using System.Collections.Generic;

namespace CYFramework.Core.UI.MVVM
{
    /// <summary>
    /// 无装箱的可观察属性
    /// </summary>
    public sealed class ObservableProperty<T>
    {
        /// <summary>
        /// 属性变更事件参数（使用泛型 T，避免 object 装箱）
        /// </summary>
        public struct ChangedEventArgs
        {
            public string PropertyName;
            public T OldValue;
            public T NewValue;
        }

        /// <summary>
        /// 属性变更回调（ref 传参减少拷贝）
        /// </summary>
        public delegate void ChangedHandler(ref ChangedEventArgs args);

        private readonly List<ChangedHandler> _handlers = new();
        private T _value;

        /// <summary>
        /// 属性名（用于调试/日志/统一标识）
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// 创建一个可观察属性
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="defaultValue">默认值</param>
        public ObservableProperty(string propertyName, T defaultValue = default)
        {
            PropertyName = propertyName;
            _value = defaultValue;
        }

        /// <summary>
        /// 当前值（赋值时会触发变更通知）
        /// </summary>
        public T Value
        {
            get => _value;
            set => Set(value);
        }

        /// <summary>
        /// 设置值（若新旧值相同则不派发事件）
        /// </summary>
        public void Set(T value)
        {
            if (EqualityComparer<T>.Default.Equals(_value, value))
            {
                return;
            }

            var args = new ChangedEventArgs
            {
                PropertyName = PropertyName,
                OldValue = _value,
                NewValue = value
            };

            _value = value;

            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i](ref args);
            }
        }

        /// <summary>
        /// 订阅属性变更
        /// </summary>
        public void Subscribe(ChangedHandler handler)
        {
            if (!_handlers.Contains(handler))
            {
                _handlers.Add(handler);
            }
        }

        /// <summary>
        /// 取消订阅属性变更
        /// </summary>
        public void Unsubscribe(ChangedHandler handler)
        {
            _handlers.Remove(handler);
        }

        /// <summary>
        /// 清空所有订阅者
        /// </summary>
        public void ClearSubscribers()
        {
            _handlers.Clear();
        }
    }
}
