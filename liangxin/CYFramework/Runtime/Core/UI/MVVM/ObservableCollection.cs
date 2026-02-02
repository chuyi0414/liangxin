// ============================================================================
// CYFramework 2.2 - 可观察集合
// 功能：支持变更通知的集合，用于列表 UI 绑定
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;

namespace CYFramework.Core.UI.MVVM
{
    /// <summary>
    /// 集合变更类型
    /// </summary>
    public enum CollectionChangeType
    {
        Add,
        Remove,
        Replace,
        Clear,
        Reset
    }
    
    /// <summary>
    /// 集合变更事件参数
    /// </summary>
    public struct CollectionChangedEventArgs<T>
    {
        /// <summary>
        /// 变更类型
        /// </summary>
        public CollectionChangeType ChangeType;

        /// <summary>
        /// 变更索引（无索引时为 -1）
        /// </summary>
        public int Index;

        /// <summary>
        /// 旧元素
        /// </summary>
        public T OldItem;

        /// <summary>
        /// 新元素
        /// </summary>
        public T NewItem;
    }
    
    /// <summary>
    /// 集合变更委托
    /// </summary>
    public delegate void CollectionChangedHandler<T>(ref CollectionChangedEventArgs<T> args);
    
    /// <summary>
    /// 可观察列表
    /// 当列表内容变化时发出通知
    /// </summary>
    public class ObservableList<T> : IList<T>, IReadOnlyList<T>
    {
        /// <summary>
        /// 内部列表
        /// </summary>
        private readonly List<T> _list;

        /// <summary>
        /// 变更监听列表
        /// </summary>
        private readonly List<CollectionChangedHandler<T>> _handlers = new();
        
        /// <summary>
        /// 创建空列表
        /// </summary>
        public ObservableList()
        {
            _list = new List<T>();
        }
        
        /// <summary>
        /// 创建指定容量的列表
        /// </summary>
        public ObservableList(int capacity)
        {
            _list = new List<T>(capacity);
        }
        
        /// <summary>
        /// 使用集合初始化列表
        /// </summary>
        public ObservableList(IEnumerable<T> collection)
        {
            _list = new List<T>(collection);
        }
        
        #region 订阅
        
        /// <summary>
        /// 订阅集合变更
        /// </summary>
        public void Subscribe(CollectionChangedHandler<T> handler)
        {
            if (!_handlers.Contains(handler))
            {
                _handlers.Add(handler);
            }
        }
        
        /// <summary>
        /// 取消订阅
        /// </summary>
        public void Unsubscribe(CollectionChangedHandler<T> handler)
        {
            _handlers.Remove(handler);
        }
        
        /// <summary>
        /// 通知变更
        /// </summary>
        private void NotifyChanged(CollectionChangeType type, int index, T oldItem, T newItem)
        {
            if (_handlers.Count == 0) return;
            
            var args = new CollectionChangedEventArgs<T> // 变更参数
            {
                ChangeType = type,
                Index = index,
                OldItem = oldItem,
                NewItem = newItem
            };
            
            foreach (var handler in _handlers)
            {
                // handler 为当前监听器
                handler(ref args);
            }
        }
        
        #endregion
        
        #region IList<T> 实现
        
        /// <summary>
        /// 索引访问
        /// </summary>
        public T this[int index]
        {
            get => _list[index];
            set
            {
                var oldItem = _list[index]; // 旧元素
                _list[index] = value;
                NotifyChanged(CollectionChangeType.Replace, index, oldItem, value);
            }
        }
        
        /// <summary>
        /// 元素数量
        /// </summary>
        public int Count => _list.Count;
        
        /// <summary>
        /// 是否只读
        /// </summary>
        public bool IsReadOnly => false;
        
        /// <summary>
        /// 添加元素
        /// </summary>
        public void Add(T item)
        {
            _list.Add(item);
            NotifyChanged(CollectionChangeType.Add, _list.Count - 1, default, item);
        }
        
        /// <summary>
        /// 插入元素
        /// </summary>
        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
            NotifyChanged(CollectionChangeType.Add, index, default, item);
        }
        
        /// <summary>
        /// 移除元素
        /// </summary>
        public bool Remove(T item)
        {
            int index = _list.IndexOf(item); // 目标索引
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 移除指定索引元素
        /// </summary>
        public void RemoveAt(int index)
        {
            var item = _list[index]; // 被移除元素
            _list.RemoveAt(index);
            NotifyChanged(CollectionChangeType.Remove, index, item, default);
        }
        
        /// <summary>
        /// 清空列表
        /// </summary>
        public void Clear()
        {
            _list.Clear();
            NotifyChanged(CollectionChangeType.Clear, -1, default, default);
        }
        
        /// <summary>
        /// 是否包含元素
        /// </summary>
        public bool Contains(T item) => _list.Contains(item);
        
        /// <summary>
        /// 获取元素索引
        /// </summary>
        public int IndexOf(T item) => _list.IndexOf(item);
        
        /// <summary>
        /// 拷贝到数组
        /// </summary>
        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
        
        /// <summary>
        /// 获取枚举器
        /// </summary>
        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        
        #endregion
        
        #region 扩展方法
        
        /// <summary>
        /// 批量添加（只发送一次通知）
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            _list.AddRange(items);
            NotifyChanged(CollectionChangeType.Reset, -1, default, default);
        }
        
        /// <summary>
        /// 替换所有内容
        /// </summary>
        public void ReplaceAll(IEnumerable<T> items)
        {
            _list.Clear();
            _list.AddRange(items);
            NotifyChanged(CollectionChangeType.Reset, -1, default, default);
        }
        
        /// <summary>
        /// 排序
        /// </summary>
        public void Sort(Comparison<T> comparison)
        {
            _list.Sort(comparison);
            NotifyChanged(CollectionChangeType.Reset, -1, default, default);
        }
        
        #endregion
    }
    
    /// <summary>
    /// 可观察字典
    /// </summary>
    public class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        /// <summary>
        /// 内部字典
        /// </summary>
        private readonly Dictionary<TKey, TValue> _dict;
        
        /// <summary>
        /// 字典变更事件
        /// </summary>
        public event Action OnChanged;
        
        /// <summary>
        /// 创建空字典
        /// </summary>
        public ObservableDictionary()
        {
            _dict = new Dictionary<TKey, TValue>();
        }
        
        /// <summary>
        /// 创建指定容量的字典
        /// </summary>
        public ObservableDictionary(int capacity)
        {
            _dict = new Dictionary<TKey, TValue>(capacity);
        }
        
        /// <summary>
        /// 通知字典变更
        /// </summary>
        private void NotifyChanged()
        {
            OnChanged?.Invoke();
        }
        
        #region IDictionary<TKey, TValue> 实现
        
        /// <summary>
        /// 键索引访问
        /// </summary>
        public TValue this[TKey key]
        {
            get => _dict[key];
            set
            {
                _dict[key] = value;
                NotifyChanged();
            }
        }
        
        /// <summary>
        /// 键集合
        /// </summary>
        public ICollection<TKey> Keys => _dict.Keys;
        
        /// <summary>
        /// 值集合
        /// </summary>
        public ICollection<TValue> Values => _dict.Values;
        
        /// <summary>
        /// 元素数量
        /// </summary>
        public int Count => _dict.Count;
        
        /// <summary>
        /// 是否只读
        /// </summary>
        public bool IsReadOnly => false;
        
        /// <summary>
        /// 添加键值对
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            _dict.Add(key, value);
            NotifyChanged();
        }
        
        /// <summary>
        /// 添加键值对
        /// </summary>
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((IDictionary<TKey, TValue>)_dict).Add(item);
            NotifyChanged();
        }
        
        /// <summary>
        /// 移除键
        /// </summary>
        public bool Remove(TKey key)
        {
            if (_dict.Remove(key))
            {
                NotifyChanged();
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 移除键值对
        /// </summary>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (((IDictionary<TKey, TValue>)_dict).Remove(item))
            {
                NotifyChanged();
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 清空字典
        /// </summary>
        public void Clear()
        {
            _dict.Clear();
            NotifyChanged();
        }
        
        /// <summary>
        /// 是否包含键
        /// </summary>
        public bool ContainsKey(TKey key) => _dict.ContainsKey(key);
        
        /// <summary>
        /// 是否包含键值对
        /// </summary>
        public bool Contains(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_dict).Contains(item);
        
        /// <summary>
        /// 尝试获取值
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value) => _dict.TryGetValue(key, out value);
        
        /// <summary>
        /// 拷贝到数组
        /// </summary>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((IDictionary<TKey, TValue>)_dict).CopyTo(array, arrayIndex);
        
        /// <summary>
        /// 获取枚举器
        /// </summary>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dict.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        
        #endregion
    }
}

