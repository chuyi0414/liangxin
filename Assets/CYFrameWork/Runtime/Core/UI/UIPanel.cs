// ============================================================================
// CYFramework 2.2 - UI 面板基类
// 功能：所有 UI 面板的基类，定义生命周期和通用功能
// ============================================================================

using System;
using UnityEngine;

namespace CYFramework.Core.UI
{
    /// <summary>
    /// UI 预制体路径特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class UIPrefabAttribute : Attribute
    {
        public string Path { get; }
        
        public UIPrefabAttribute(string path)
        {
            Path = path;
        }
    }
    
    /// <summary>
    /// UI 面板基类
    /// 所有 UI 面板都应继承此类
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class UIPanel : MonoBehaviour
    {
        #region 属性
        
        /// <summary>
        /// UI 层级
        /// </summary>
        public virtual UILayer Layer => UILayer.Main;
        
        /// <summary>
        /// 是否可堆叠（支持返回操作）
        /// </summary>
        public virtual bool IsStackable => true;
        
        /// <summary>
        /// 是否可对象池复用
        /// </summary>
        public virtual bool IsPoolable => true;
        
        /// <summary>
        /// 是否显示遮罩
        /// </summary>
        public virtual bool ShowMask => false;
        
        /// <summary>
        /// 点击遮罩是否关闭
        /// </summary>
        public virtual bool CloseOnMaskClick => true;
        
        /// <summary>
        /// 是否启用动画
        /// </summary>
        public virtual bool EnableAnimation => true;
        
        /// <summary>
        /// RectTransform 组件
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
        /// CanvasGroup 组件（用于淡入淡出）
        /// </summary>
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
        
        /// <summary>
        /// UI 管理器引用
        /// </summary>
        protected UIManager Manager { get; private set; }
        
        /// <summary>
        /// 面板是否已打开
        /// </summary>
        public bool IsOpened { get; private set; }
        
        #endregion
        
        #region 生命周期
        
        /// <summary>
        /// 设置管理器引用
        /// </summary>
        internal void SetManager(UIManager manager)
        {
            Manager = manager;
        }
        
        /// <summary>
        /// 面板打开时调用
        /// </summary>
        /// <param name="data">传入的数据</param>
        public void OnOpen(object data)
        {
            IsOpened = true;
            
            // 绑定 UI 事件
            OnBindUI();
            
            // 子类实现
            OnShow(data);
            
            // 播放打开动画
            if (EnableAnimation)
            {
                PlayOpenAnimation();
            }
        }
        
        /// <summary>
        /// 面板刷新时调用（已打开状态下再次 Open）
        /// </summary>
        public void OnRefresh(object data)
        {
            OnShow(data);
        }
        
        /// <summary>
        /// 面板关闭时调用
        /// </summary>
        public void OnClose()
        {
            IsOpened = false;
            
            // 解绑 UI 事件
            OnUnbindUI();
            
            // 子类实现
            OnHide();
        }
        
        /// <summary>
        /// Unity Awake
        /// </summary>
        protected virtual void Awake()
        {
            // 缓存组件
            _rectTransform = GetComponent<RectTransform>();
        }
        
        /// <summary>
        /// Unity OnDestroy
        /// </summary>
        protected virtual void OnDestroy()
        {
            OnUnbindUI();
        }
        
        #endregion
        
        #region 子类实现
        
        /// <summary>
        /// 绑定 UI 事件（按钮点击等）
        /// </summary>
        protected virtual void OnBindUI() { }
        
        /// <summary>
        /// 解绑 UI 事件
        /// </summary>
        protected virtual void OnUnbindUI() { }
        
        /// <summary>
        /// 面板显示
        /// </summary>
        /// <param name="data">传入的数据</param>
        protected abstract void OnShow(object data);
        
        /// <summary>
        /// 面板隐藏
        /// </summary>
        protected virtual void OnHide() { }
        
        /// <summary>
        /// 每帧更新（动画、计时器等）
        /// 由 UIManager 自动驱动
        /// </summary>
        protected internal virtual void OnUpdate(float deltaTime) { }
        
        /// <summary>
        /// 延迟更新（跟随、位置调整等）
        /// 由 UIManager 自动驱动
        /// </summary>
        protected internal virtual void OnLateUpdate(float deltaTime) { }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 关闭自身
        /// </summary>
        public void CloseSelf()
        {
            Manager?.Close(this);
        }
        
        /// <summary>
        /// 返回上一个面板
        /// </summary>
        public void Back()
        {
            Manager?.Back();
        }
        
        /// <summary>
        /// 设置可交互性
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            CanvasGroup.interactable = interactable;
            CanvasGroup.blocksRaycasts = interactable;
        }
        
        /// <summary>
        /// 设置透明度
        /// </summary>
        public void SetAlpha(float alpha)
        {
            CanvasGroup.alpha = alpha;
        }
        
        #endregion
        
        #region 动画
        
        /// <summary>
        /// 播放打开动画
        /// </summary>
        protected virtual void PlayOpenAnimation()
        {
            // 默认：缩放弹出
            transform.localScale = Vector3.one * 0.8f;
            StartCoroutine(ScaleAnimation(Vector3.one, 0.15f));
        }
        
        /// <summary>
        /// 播放关闭动画
        /// </summary>
        protected virtual void PlayCloseAnimation(Action onComplete)
        {
            // 默认：缩放收缩
            StartCoroutine(ScaleAnimation(Vector3.one * 0.8f, 0.1f, onComplete));
        }
        
        /// <summary>
        /// 缩放动画协程
        /// </summary>
        private System.Collections.IEnumerator ScaleAnimation(Vector3 targetScale, float duration, Action onComplete = null)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                // 使用缓动曲线
                t = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }
            
            transform.localScale = targetScale;
            onComplete?.Invoke();
        }
        
        #endregion
    }
}

