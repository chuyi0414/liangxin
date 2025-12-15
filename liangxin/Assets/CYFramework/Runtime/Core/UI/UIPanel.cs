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
        
        #region 生命周期 - 框架内部调用
        
        /// <summary>
        /// 设置管理器引用
        /// </summary>
        internal void SetManager(UIManager manager)
        {
            Manager = manager;
        }
        
        /// <summary>
        /// 面板初始化（每次打开时调用，包括从对象池取出）
        /// </summary>
        internal void InternalInit(object userData)
        {
            _rectTransform = GetComponent<RectTransform>();
            OnInit(userData);
        }
        
        /// <summary>
        /// 面板打开
        /// </summary>
        internal void InternalOpen(object userData)
        {
            IsOpened = true;
            OnBindUI();
            OnOpen(userData);
            
            if (EnableAnimation)
            {
                PlayOpenAnimation();
            }
        }
        
        /// <summary>
        /// 面板关闭
        /// </summary>
        internal void InternalClose(bool isShutdown, object userData)
        {
            IsOpened = false;
            OnClose(isShutdown, userData);
            OnUnbindUI();
        }
        
        /// <summary>
        /// 面板显示（从隐藏恢复）
        /// </summary>
        internal void InternalShow()
        {
            gameObject.SetActive(true);
            OnShow();
        }
        
        /// <summary>
        /// 面板隐藏（不关闭，只隐藏）
        /// </summary>
        internal void InternalHide()
        {
            OnHide();
            gameObject.SetActive(false);
        }
        
        /// <summary>
        /// 面板刷新（已打开状态下再次 Open）
        /// </summary>
        internal void InternalRefresh(object userData)
        {
            OnRefresh(userData);
        }
        
        /// <summary>
        /// 面板被覆盖（新面板打开）
        /// </summary>
        internal void InternalPause()
        {
            OnPause();
        }
        
        /// <summary>
        /// 面板恢复（覆盖的面板关闭后）
        /// </summary>
        internal void InternalResume()
        {
            OnResume();
        }
        
        /// <summary>
        /// 面板回收到对象池
        /// </summary>
        internal void InternalRecycle()
        {
            OnRecycle();
        }
        
        /// <summary>
        /// 每帧更新
        /// </summary>
        internal void InternalUpdate(float elapseSeconds, float realElapseSeconds)
        {
            OnUpdate(elapseSeconds, realElapseSeconds);
        }
        
        /// <summary>
        /// 延迟更新
        /// </summary>
        internal void InternalLateUpdate(float elapseSeconds, float realElapseSeconds)
        {
            OnLateUpdate(elapseSeconds, realElapseSeconds);
        }
        
        protected virtual void OnDestroy()
        {
            OnUnbindUI();
        }
        
        #endregion
        
        #region 子类重写 - 生命周期
        
        /// <summary>
        /// 窗口初始化时调用（每次打开时调用，包括从对象池取出）
        /// </summary>
        protected virtual void OnInit(object userData) { }
        
        /// <summary>
        /// 窗口打开时调用
        /// </summary>
        protected virtual void OnOpen(object userData) { }
        
        /// <summary>
        /// 窗口关闭时调用
        /// </summary>
        /// <param name="isShutdown">是否是关闭整个 UI 系统</param>
        /// <param name="userData">用户数据</param>
        protected virtual void OnClose(bool isShutdown, object userData) { }
        
        /// <summary>
        /// 窗口显示时调用（从隐藏恢复）
        /// </summary>
        protected virtual void OnShow() { }
        
        /// <summary>
        /// 窗口隐藏时调用
        /// </summary>
        protected virtual void OnHide() { }
        
        /// <summary>
        /// 每帧更新时调用
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（受 TimeScale 影响）</param>
        /// <param name="realElapseSeconds">真实流逝时间</param>
        protected virtual void OnUpdate(float elapseSeconds, float realElapseSeconds) { }
        
        /// <summary>
        /// 延迟更新时调用
        /// </summary>
        protected virtual void OnLateUpdate(float elapseSeconds, float realElapseSeconds) { }
        
        /// <summary>
        /// 窗口回收时调用（对象池回收，下次取出时会重新执行 OnInit）
        /// </summary>
        protected virtual void OnRecycle() { }
        
        /// <summary>
        /// 窗口被其他窗口覆盖时调用
        /// </summary>
        protected virtual void OnPause() { }
        
        /// <summary>
        /// 覆盖的窗口关闭后恢复时调用
        /// </summary>
        protected virtual void OnResume() { }
        
        /// <summary>
        /// 窗口刷新时调用（已打开状态下再次 Open）
        /// </summary>
        protected virtual void OnRefresh(object userData) { }
        
        #endregion
        
        #region 子类重写 - UI 事件绑定
        
        /// <summary>
        /// 绑定 UI 事件（按钮点击等）
        /// </summary>
        protected virtual void OnBindUI() { }
        
        /// <summary>
        /// 解绑 UI 事件
        /// </summary>
        protected virtual void OnUnbindUI() { }
        
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
            // 注意：动画时长来自 UIManagerConfig.DefaultAnimDuration（通过 UIManager.DefaultAnimDuration 暴露）。
            // 当配置为 0 时，表示不播放动画（直接显示最终状态），避免仍然产生一帧插值导致“看起来还有动画”。
            float duration = Manager != null ? Manager.DefaultAnimDuration : 0.15f;
            if (duration <= 0f)
            {
                transform.localScale = Vector3.one;
                return;
            }

            transform.localScale = Vector3.one * 0.8f;
            StartCoroutine(ScaleAnimation(Vector3.one, duration));
        }
        
        /// <summary>
        /// 播放关闭动画
        /// </summary>
        protected virtual void PlayCloseAnimation(Action onComplete)
        {
            // 默认：缩放收缩
            // 当配置为 0 时，直接回调完成（用于“立即关闭”需求，例如切场景/战斗 HUD 高频切换）。
            float duration = Manager != null ? Manager.DefaultAnimDuration : 0.1f;
            if (duration <= 0f)
            {
                transform.localScale = Vector3.one;
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(ScaleAnimation(Vector3.one * 0.8f, duration, onComplete));
        }
        
        /// <summary>
        /// 缩放动画协程
        /// </summary>
        private System.Collections.IEnumerator ScaleAnimation(Vector3 targetScale, float duration, Action onComplete = null)
        {
            // 保护：避免 duration=0 导致除零，并确保“无动画”时不产生一帧延迟。
            if (duration <= 0f)
            {
                transform.localScale = targetScale;
                onComplete?.Invoke();
                yield break;
            }

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
