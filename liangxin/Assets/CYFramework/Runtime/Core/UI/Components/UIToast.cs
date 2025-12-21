// ============================================================================
// CYFramework 2.2 - Toast 提示组件
// 功能：轻量级消息提示，自动消失
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CYFramework.Core.UI.Components
{
    /// <summary>
    /// Toast 消息数据
    /// </summary>
    public struct ToastMessage
    {
        /// <summary>
        /// 文本内容
        /// </summary>
        public string Content;

        /// <summary>
        /// 持续时间（秒）
        /// </summary>
        public float Duration;

        /// <summary>
        /// 文本颜色
        /// </summary>
        public Color Color;
    }
    
    /// <summary>
    /// Toast 提示管理器
    /// </summary>
    public class UIToast : MonoBehaviour
    {
        [Header("配置")]
        /// <summary>
        /// Toast 预制体
        /// </summary>
        [SerializeField] private GameObject _toastPrefab;
        /// <summary>
        /// 容器
        /// </summary>
        [SerializeField] private Transform _container;
        /// <summary>
        /// 最大同时显示数量
        /// </summary>
        [SerializeField] private int _maxToasts = 5;
        /// <summary>
        /// 默认持续时长
        /// </summary>
        [SerializeField] private float _defaultDuration = 2f;
        /// <summary>
        /// 淡入时长
        /// </summary>
        [SerializeField] private float _fadeInDuration = 0.2f;
        /// <summary>
        /// 淡出时长
        /// </summary>
        [SerializeField] private float _fadeOutDuration = 0.3f;
        /// <summary>
        /// 上移动画距离
        /// </summary>
        [SerializeField] private float _moveUpDistance = 50f;
        
        // 对象池
        /// <summary>
        /// Toast 对象池
        /// </summary>
        private readonly Queue<ToastItem> _pool = new();
        
        // 当前显示的 Toast
        /// <summary>
        /// 当前显示列表
        /// </summary>
        private readonly List<ToastItem> _activeToasts = new();
        
        // 待显示队列
        /// <summary>
        /// 待显示消息队列
        /// </summary>
        private readonly Queue<ToastMessage> _pendingMessages = new();
        
        // 单例
        /// <summary>
        /// 单例实例
        /// </summary>
        private static UIToast _instance;

        /// <summary>
        /// 单例访问
        /// </summary>
        public static UIToast Instance => _instance;
        
        #region 生命周期
        
        /// <summary>
        /// Unity Awake
        /// </summary>
        private void Awake()
        {
            _instance = this;
            
            if (_container == null)
            {
                // 默认容器为自身
                _container = transform;
            }
        }
        
        /// <summary>
        /// Unity OnDestroy
        /// </summary>
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        #endregion
        
        #region 公共 API

        /// <summary>
        /// 应用框架配置（由 <see cref="UIManager"/> 在初始化时下发）。
        /// </summary>
        /// <remarks>
        /// - 只覆盖“最大并发数量/默认时长”这两个高频需求，淡入淡出等细节仍由预制体侧调整。
        /// - 该方法不会产生 GC；不要在 Update 高频调用。
        /// </remarks>
        public void ApplyConfig(int maxToasts, float defaultDuration)
        {
            _maxToasts = Mathf.Clamp(maxToasts, 1, 99);
            _defaultDuration = Mathf.Max(0f, defaultDuration);
        }
        
        /// <summary>
        /// 显示 Toast
        /// </summary>
        public static void Show(string content, float duration = 0f)
        {
            if (_instance == null) return;
            _instance.ShowToast(content, duration);
        }
        
        /// <summary>
        /// 显示成功提示
        /// </summary>
        public static void ShowSuccess(string content)
        {
            if (_instance == null) return;
            _instance.ShowToast(content, 0f, new Color(0.2f, 0.8f, 0.2f));
        }
        
        /// <summary>
        /// 显示错误提示
        /// </summary>
        public static void ShowError(string content)
        {
            if (_instance == null) return;
            _instance.ShowToast(content, 0f, new Color(0.9f, 0.2f, 0.2f));
        }
        
        /// <summary>
        /// 显示警告提示
        /// </summary>
        public static void ShowWarning(string content)
        {
            if (_instance == null) return;
            _instance.ShowToast(content, 0f, new Color(0.9f, 0.7f, 0.1f));
        }
        
        /// <summary>
        /// 清除所有 Toast
        /// </summary>
        public void ClearAll()
        {
            foreach (var toast in _activeToasts)
            {
                // toast 为当前显示的项
                RecycleToast(toast);
            }
            _activeToasts.Clear();
            _pendingMessages.Clear();
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 显示 Toast
        /// </summary>
        private void ShowToast(string content, float duration, Color? color = null)
        {
            var message = new ToastMessage // 消息数据
            {
                Content = content,
                Duration = duration > 0 ? duration : _defaultDuration,
                Color = color ?? Color.white
            };
            
            // 如果当前显示数量已满，加入队列
            if (_activeToasts.Count >= _maxToasts)
            {
                _pendingMessages.Enqueue(message);
                return;
            }
            
            DisplayToast(message);
        }
        
        /// <summary>
        /// 实例化并显示 Toast
        /// </summary>
        private void DisplayToast(ToastMessage message)
        {
            var toast = GetOrCreateToast(); // 取出或创建 Toast
            toast.Setup(message.Content, message.Color);
            toast.gameObject.SetActive(true);
            _activeToasts.Add(toast);
            
            StartCoroutine(ToastLifecycle(toast, message.Duration));
        }
        
        /// <summary>
        /// Toast 生命周期流程
        /// </summary>
        private IEnumerator ToastLifecycle(ToastItem toast, float duration)
        {
            var canvasGroup = toast.CanvasGroup; // 透明度控制
            var rectTransform = toast.RectTransform; // 位置控制
            
            // 初始状态
            canvasGroup.alpha = 0f;
            Vector2 startPos = rectTransform.anchoredPosition; // 初始位置
            Vector2 endPos = startPos + Vector2.up * _moveUpDistance; // 目标位置
            
            // 淡入
            float elapsed = 0f; // 计时
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _fadeInDuration; // 归一化时间
                canvasGroup.alpha = t;
                rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, UIExtensions.EaseOutCubic(t));
                yield return null;
            }
            canvasGroup.alpha = 1f;
            rectTransform.anchoredPosition = endPos;
            
            // 等待
            yield return new WaitForSecondsRealtime(duration);
            
            // 淡出
            elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _fadeOutDuration; // 归一化时间
                canvasGroup.alpha = 1f - t;
                yield return null;
            }
            
            // 回收
            _activeToasts.Remove(toast);
            RecycleToast(toast);
            
            // 显示队列中的下一个
            if (_pendingMessages.Count > 0)
            {
                // 从队列取出下一条
                DisplayToast(_pendingMessages.Dequeue());
            }
        }
        
        /// <summary>
        /// 获取或创建 Toast 项
        /// </summary>
        private ToastItem GetOrCreateToast()
        {
            if (_pool.Count > 0)
            {
                return _pool.Dequeue();
            }
            
            var go = Instantiate(_toastPrefab, _container); // 新建实例
            return go.GetComponent<ToastItem>() ?? go.AddComponent<ToastItem>();
        }
        
        /// <summary>
        /// 回收 Toast 项
        /// </summary>
        private void RecycleToast(ToastItem toast)
        {
            toast.gameObject.SetActive(false);
            toast.RectTransform.anchoredPosition = Vector2.zero;
            _pool.Enqueue(toast);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Toast 项
    /// </summary>
    public class ToastItem : MonoBehaviour
    {
        /// <summary>
        /// 文本组件
        /// </summary>
        [SerializeField] private Text _text;
        /// <summary>
        /// 背景图
        /// </summary>
        [SerializeField] private Image _background;
        
        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                {
                    // 缓存 RectTransform
                    _rectTransform = GetComponent<RectTransform>();
                }
                return _rectTransform;
            }
        }
        /// <summary>
        /// 缓存的 RectTransform
        /// </summary>
        private RectTransform _rectTransform;
        
        /// <summary>
        /// CanvasGroup 组件
        /// </summary>
        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null)
                {
                    _canvasGroup = GetComponent<CanvasGroup>();
                    if (_canvasGroup == null)
                    {
                        // 没有 CanvasGroup 时动态补齐
                        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                    }
                }
                return _canvasGroup;
            }
        }
        /// <summary>
        /// 缓存的 CanvasGroup
        /// </summary>
        private CanvasGroup _canvasGroup;
        
        /// <summary>
        /// 初始化文本与颜色
        /// </summary>
        public void Setup(string content, Color color)
        {
            if (_text != null)
            {
                _text.text = content;
                _text.color = color;
            }
        }
    }
}
