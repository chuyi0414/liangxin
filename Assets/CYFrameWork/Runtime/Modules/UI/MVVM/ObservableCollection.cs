// ============================================================================
// CYFramework 2.2 - 可观察集合
// 功能：支持变更通知的集合，用于列表 UI 绑定
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;

namespace CYFramework.Modules.UI.MVVM
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
        public CollectionChangeType ChangeType;
        public int Index;
        public T OldItem;
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
        private readonly List<T> _list;
        private readonly List<CollectionChangedHandler<T>> _handlers = new();
        
        public ObservableList()
        {
            _list = new List<T>();
        }
        
        public ObservableList(int capacity)
        {
            _list = new List<T>(capacity);
        }
        
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
            
            var args = new CollectionChangedEventArgs<T>
            {
                ChangeType = type,
                Index = index,
                OldItem = oldItem,
                NewItem = newItem
            };
            
            foreach (var handler in _handlers)
            {
                handler(ref args);
            }
        }
        
        #endregion
        
        #region IList<T> 实现
        
        public T this[int index]
        {
            get => _list[index];
            set
            {
                var oldItem = _list[index];
                _list[index] = value;
                NotifyChanged(CollectionChangeType.Replace, index, oldItem, value);
            }
        }
        
        public int Count => _list.Count;
        
        public bool IsReadOnly => false;
        
        public void Add(T item)
        {
            _list.Add(item);
            NotifyChanged(CollectionChangeType.Add, _list.Count - 1, default, item);
        }
        
        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
            NotifyChanged(CollectionChangeType.Add, index, default, item);
        }
        
        public bool Remove(T item)
        {
            int index = _list.IndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }
        
        public void RemoveAt(int index)
        {
            var item = _list[index];
            _list.RemoveAt(index);
            NotifyChanged(CollectionChangeType.Remove, index, item, default);
        }
        
        public void Clear()
        {
            _list.Clear();
            NotifyChanged(CollectionChangeType.Clear, -1, default, default);
        }
        
        public bool Contains(T item) => _list.Contains(item);
        
        public int IndexOf(T item) => _list.IndexOf(item);
        
        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
        
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
        private readonly Dictionary<TKey, TValue> _dict;
        
        /// <summary>
        /// 字典变更事件
        /// </summary>
        public event Action OnChanged;
        
        public ObservableDictionary()
        {
            _dict = new Dictionary<TKey, TValue>();
        }
        
        public ObservableDictionary(int capacity)
        {
            _dict = new Dictionary<TKey, TValue>(capacity);
        }
        
        private void NotifyChanged()
        {
            OnChanged?.Invoke();
        }
        
        #region IDictionary<TKey, TValue> 实现
        
        public TValue this[TKey key]
        {
            get => _dict[key];
            set
            {
                _dict[key] = value;
                NotifyChanged();
            }
        }
        
        public ICollection<TKey> Keys => _dict.Keys;
        
        public ICollection<TValue> Values => _dict.Values;
        
        public int Count => _dict.Count;
        
        public bool IsReadOnly => false;
        
        public void Add(TKey key, TValue value)
        {
            _dict.Add(key, value);
            NotifyChanged();
        }
        
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((IDictionary<TKey, TValue>)_dict).Add(item);
            NotifyChanged();
        }
        
        public bool Remove(TKey key)
        {
            if (_dict.Remove(key))
            {
                NotifyChanged();
                return true;
            }
            return false;
        }
        
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (((IDictionary<TKey, TValue>)_dict).Remove(item))
            {
                NotifyChanged();
                return true;
            }
            return false;
        }
        
        public void Clear()
        {
            _dict.Clear();
            NotifyChanged();
        }
        
        public bool ContainsKey(TKey key) => _dict.ContainsKey(key);
        
        public bool Contains(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_dict).Contains(item);
        
        public bool TryGetValue(TKey key, out TValue value) => _dict.TryGetValue(key, out value);
        
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((IDictionary<TKey, TValue>)_dict).CopyTo(array, arrayIndex);
        
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dict.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        
        #endregion
    }
}
