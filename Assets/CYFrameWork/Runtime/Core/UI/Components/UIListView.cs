// ============================================================================
// CYFramework 2.2 - 列表视图组件
// 功能：可复用的列表组件，支持对象池和虚拟化
// ============================================================================

using System;
using System.Collections.Generic;
using CYFramework.Core.UI.MVVM;
using UnityEngine;

namespace CYFramework.Core.UI.Components
{
    /// <summary>
    /// 列表项基类
    /// </summary>
    public abstract class UIListItem : MonoBehaviour
    {
        /// <summary>
        /// 数据索引
        /// </summary>
        public int Index { get; private set; }
        
        /// <summary>
        /// RectTransform
        /// </summary>
        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                    _rectTransform = GetComponent<RectTransform>();
                return _rectTransform;
            }
        }
        private RectTransform _rectTransform;
        
        /// <summary>
        /// 设置索引
        /// </summary>
        internal void SetIndex(int index)
        {
            Index = index;
        }
        
        /// <summary>
        /// 设置数据
        /// </summary>
        public abstract void SetData(object data);
        
        /// <summary>
        /// 重置状态
        /// </summary>
        public virtual void Reset() { }
    }
    
    /// <summary>
    /// 泛型列表项
    /// </summary>
    public abstract class UIListItem<T> : UIListItem
    {
        /// <summary>
        /// 当前数据
        /// </summary>
        protected T Data { get; private set; }
        
        public override void SetData(object data)
        {
            Data = (T)data;
            OnDataChanged(Data);
        }
        
        /// <summary>
        /// 数据变更时调用
        /// </summary>
        protected abstract void OnDataChanged(T data);
    }
    
    /// <summary>
    /// 列表视图
    /// 支持对象池复用
    /// </summary>
    public class UIListView : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private GameObject _itemPrefab;
        [SerializeField] private Transform _content;
        [SerializeField] private int _poolCapacity = 20;
        
        // 对象池
        private readonly Queue<UIListItem> _pool = new();
        
        // 当前显示的项
        private readonly List<UIListItem> _activeItems = new();
        
        // 数据源
        private IList<object> _dataSource;
        
        // 点击事件
        public event Action<int, object> OnItemClicked;
        
        #region 生命周期
        
        private void Awake()
        {
            if (_content == null)
            {
                _content = transform;
            }
        }
        
        private void OnDestroy()
        {
            ClearPool();
        }
        
        #endregion
        
        #region 公共 API
        
        /// <summary>
        /// 设置数据源
        /// </summary>
        public void SetData<T>(IList<T> data)
        {
            // 转换为 object 列表
            var objectList = new List<object>(data.Count);
            foreach (var item in data)
            {
                objectList.Add(item);
            }
            _dataSource = objectList;
            
            Refresh();
        }
        
        /// <summary>
        /// 绑定可观察列表
        /// </summary>
        public void BindObservableList<T>(ObservableList<T> list)
        {
            SetData(list);
            
            // 订阅变更
            list.Subscribe((ref CollectionChangedEventArgs<T> args) =>
            {
                switch (args.ChangeType)
                {
                    case CollectionChangeType.Add:
                        InsertItem(args.Index, args.NewItem);
                        break;
                    case CollectionChangeType.Remove:
                        RemoveItem(args.Index);
                        break;
                    case CollectionChangeType.Replace:
                        UpdateItem(args.Index, args.NewItem);
                        break;
                    case CollectionChangeType.Clear:
                    case CollectionChangeType.Reset:
                        Refresh();
                        break;
                }
            });
        }
        
        /// <summary>
        /// 刷新列表
        /// </summary>
        public void Refresh()
        {
            // 回收所有项
            foreach (var item in _activeItems)
            {
                RecycleItem(item);
            }
            _activeItems.Clear();
            
            if (_dataSource == null) return;
            
            // 创建新项
            for (int i = 0; i < _dataSource.Count; i++)
            {
                var item = GetOrCreateItem();
                item.SetIndex(i);
                item.SetData(_dataSource[i]);
                item.transform.SetParent(_content, false);
                _activeItems.Add(item);
            }
        }
        
        /// <summary>
        /// 更新指定项
        /// </summary>
        public void UpdateItem(int index, object data)
        {
            if (index >= 0 && index < _activeItems.Count)
            {
                _activeItems[index].SetData(data);
            }
        }
        
        /// <summary>
        /// 插入项
        /// </summary>
        public void InsertItem(int index, object data)
        {
            var item = GetOrCreateItem();
            item.SetIndex(index);
            item.SetData(data);
            item.transform.SetParent(_content, false);
            item.transform.SetSiblingIndex(index);
            _activeItems.Insert(index, item);
            
            // 更新后续项索引
            for (int i = index + 1; i < _activeItems.Count; i++)
            {
                _activeItems[i].SetIndex(i);
            }
        }
        
        /// <summary>
        /// 移除项
        /// </summary>
        public void RemoveItem(int index)
        {
            if (index < 0 || index >= _activeItems.Count) return;
            
            var item = _activeItems[index];
            _activeItems.RemoveAt(index);
            RecycleItem(item);
            
            // 更新后续项索引
            for (int i = index; i < _activeItems.Count; i++)
            {
                _activeItems[i].SetIndex(i);
            }
        }
        
        /// <summary>
        /// 清空列表
        /// </summary>
        public void Clear()
        {
            foreach (var item in _activeItems)
            {
                RecycleItem(item);
            }
            _activeItems.Clear();
            _dataSource = null;
        }
        
        /// <summary>
        /// 获取指定索引的项
        /// </summary>
        public UIListItem GetItem(int index)
        {
            if (index >= 0 && index < _activeItems.Count)
            {
                return _activeItems[index];
            }
            return null;
        }
        
        /// <summary>
        /// 滚动到指定索引
        /// </summary>
        public void ScrollToIndex(int index)
        {
            // TODO: 实现滚动逻辑
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 获取或创建项
        /// </summary>
        private UIListItem GetOrCreateItem()
        {
            UIListItem item;
            
            if (_pool.Count > 0)
            {
                item = _pool.Dequeue();
                item.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(_itemPrefab);
                item = go.GetComponent<UIListItem>();
                
                // 绑定点击事件
                var clickHandler = go.GetComponent<UnityEngine.UI.Button>();
                if (clickHandler != null)
                {
                    var capturedItem = item;
                    clickHandler.onClick.AddListener(() =>
                    {
                        OnItemClicked?.Invoke(capturedItem.Index, _dataSource[capturedItem.Index]);
                    });
                }
            }
            
            item.Reset();
            return item;
        }
        
        /// <summary>
        /// 回收项到对象池
        /// </summary>
        private void RecycleItem(UIListItem item)
        {
            if (_pool.Count < _poolCapacity)
            {
                item.gameObject.SetActive(false);
                item.transform.SetParent(transform, false);
                _pool.Enqueue(item);
            }
            else
            {
                Destroy(item.gameObject);
            }
        }
        
        /// <summary>
        /// 清空对象池
        /// </summary>
        private void ClearPool()
        {
            while (_pool.Count > 0)
            {
                var item = _pool.Dequeue();
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }
        }
        
        #endregion
    }
}

