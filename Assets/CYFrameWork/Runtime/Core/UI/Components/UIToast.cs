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
        public string Content;
        public float Duration;
        public Color Color;
    }
    
    /// <summary>
    /// Toast 提示管理器
    /// </summary>
    public class UIToast : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private GameObject _toastPrefab;
        [SerializeField] private Transform _container;
        [SerializeField] private int _maxToasts = 5;
        [SerializeField] private float _defaultDuration = 2f;
        [SerializeField] private float _fadeInDuration = 0.2f;
        [SerializeField] private float _fadeOutDuration = 0.3f;
        [SerializeField] private float _moveUpDistance = 50f;
        
        // 对象池
        private readonly Queue<ToastItem> _pool = new();
        
        // 当前显示的 Toast
        private readonly List<ToastItem> _activeToasts = new();
        
        // 待显示队列
        private readonly Queue<ToastMessage> _pendingMessages = new();
        
        // 单例
        private static UIToast _instance;
        public static UIToast Instance => _instance;
        
        #region 生命周期
        
        private void Awake()
        {
            _instance = this;
            
            if (_container == null)
            {
                _container = transform;
            }
        }
        
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
                RecycleToast(toast);
            }
            _activeToasts.Clear();
            _pendingMessages.Clear();
        }
        
        #endregion
        
        #region 私有方法
        
        private void ShowToast(string content, float duration, Color? color = null)
        {
            var message = new ToastMessage
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
        
        private void DisplayToast(ToastMessage message)
        {
            var toast = GetOrCreateToast();
            toast.Setup(message.Content, message.Color);
            toast.gameObject.SetActive(true);
            _activeToasts.Add(toast);
            
            StartCoroutine(ToastLifecycle(toast, message.Duration));
        }
        
        private IEnumerator ToastLifecycle(ToastItem toast, float duration)
        {
            var canvasGroup = toast.CanvasGroup;
            var rectTransform = toast.RectTransform;
            
            // 初始状态
            canvasGroup.alpha = 0f;
            Vector2 startPos = rectTransform.anchoredPosition;
            Vector2 endPos = startPos + Vector2.up * _moveUpDistance;
            
            // 淡入
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _fadeInDuration;
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
                float t = elapsed / _fadeOutDuration;
                canvasGroup.alpha = 1f - t;
                yield return null;
            }
            
            // 回收
            _activeToasts.Remove(toast);
            RecycleToast(toast);
            
            // 显示队列中的下一个
            if (_pendingMessages.Count > 0)
            {
                DisplayToast(_pendingMessages.Dequeue());
            }
        }
        
        private ToastItem GetOrCreateToast()
        {
            if (_pool.Count > 0)
            {
                return _pool.Dequeue();
            }
            
            var go = Instantiate(_toastPrefab, _container);
            return go.GetComponent<ToastItem>() ?? go.AddComponent<ToastItem>();
        }
        
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
        [SerializeField] private Text _text;
        [SerializeField] private Image _background;
        
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
        
        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null)
                {
                    _canvasGroup = GetComponent<CanvasGroup>();
                    if (_canvasGroup == null)
                        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
                return _canvasGroup;
            }
        }
        private CanvasGroup _canvasGroup;
        
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

