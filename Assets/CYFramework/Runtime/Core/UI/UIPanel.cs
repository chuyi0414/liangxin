// ============================================================================
// CYFramework - UI 面板基类
// 所有 UI 面板都应继承此类
// ============================================================================

using UnityEngine;

namespace CYFramework.Runtime.Core.UI
{
    /// <summary>
    /// UI 面板状态
    /// </summary>
    public enum UIPanelState
    {
        /// <summary>
        /// 未初始化
        /// </summary>
        None,

        /// <summary>
        /// 已加载但未显示
        /// </summary>
        Loaded,

        /// <summary>
        /// 显示中
        /// </summary>
        Visible,

        /// <summary>
        /// 隐藏中
        /// </summary>
        Hidden,

        /// <summary>
        /// 已销毁
        /// </summary>
        Destroyed
    }

    /// <summary>
    /// UI 层级
    /// 数值越大越在上层
    /// </summary>
    public enum UILayer
    {
        /// <summary>
        /// 背景层（最底层）
        /// </summary>
        Background = 0,

        /// <summary>
        /// 主界面层
        /// </summary>
        Main = 100,

        /// <summary>
        /// 弹窗层
        /// </summary>
        Popup = 200,

        /// <summary>
        /// 提示层
        /// </summary>
        Toast = 300,

        /// <summary>
        /// 加载层
        /// </summary>
        Loading = 400,

        /// <summary>
        /// 系统层（最顶层，如网络断开提示）
        /// </summary>
        System = 500
    }

    /// <summary>
    /// UI 面板基类
    /// </summary>
    public abstract class UIPanel : MonoBehaviour
    {
        /// <summary>
        /// 面板名称（默认使用类名）
        /// </summary>
        public virtual string PanelName => GetType().Name;

        /// <summary>
        /// 面板层级
        /// </summary>
        public virtual UILayer Layer => UILayer.Main;

        /// <summary>
        /// 是否缓存（关闭后不销毁）
        /// </summary>
        public virtual bool IsCached => true;

        /// <summary>
        /// 当前状态
        /// </summary>
        public UIPanelState State { get; internal set; } = UIPanelState.None;

        /// <summary>
        /// 面板的 RectTransform
        /// </summary>
        public RectTransform RectTransform => transform as RectTransform;

        // ====================================================================
        // 生命周期（子类重写）
        // ====================================================================

        /// <summary>
        /// 面板加载完成时调用（只调用一次）
        /// 用于初始化组件引用
        /// </summary>
        public virtual void OnLoad()
        {
            Log.D("UIPanel", $"{PanelName} OnLoad");
        }

        /// <summary>
        /// 面板打开时调用
        /// </summary>
        /// <param name="param">传入的参数</param>
        public virtual void OnOpen(object param = null)
        {
            Log.D("UIPanel", $"{PanelName} OnOpen");
        }

        /// <summary>
        /// 面板显示时调用（每次显示都会调用）
        /// </summary>
        public virtual void OnShow()
        {
            Log.D("UIPanel", $"{PanelName} OnShow");
        }

        /// <summary>
        /// 面板隐藏时调用
        /// </summary>
        public virtual void OnHide()
        {
            Log.D("UIPanel", $"{PanelName} OnHide");
        }

        /// <summary>
        /// 面板关闭时调用
        /// </summary>
        public virtual void OnClose()
        {
            Log.D("UIPanel", $"{PanelName} OnClose");
        }

        /// <summary>
        /// 面板销毁时调用
        /// </summary>
        public virtual void OnUnload()
        {
            Log.D("UIPanel", $"{PanelName} OnUnload");
        }

        /// <summary>
        /// 面板刷新（外部数据变化时调用）
        /// </summary>
        public virtual void OnRefresh()
        {
        }

        // ====================================================================
        // 便捷方法
        // ====================================================================

        /// <summary>
        /// 关闭当前面板
        /// </summary>
        protected void CloseSelf()
        {
            CYFrameworkEntry.Instance?.UI?.ClosePanel(PanelName);
        }

        /// <summary>
        /// 隐藏当前面板
        /// </summary>
        protected void HideSelf()
        {
            CYFrameworkEntry.Instance?.UI?.HidePanel(PanelName);
        }

        /// <summary>
        /// 打开其他面板
        /// </summary>
        protected void OpenPanel<T>(object param = null) where T : UIPanel
        {
            CYFrameworkEntry.Instance?.UI?.OpenPanel<T>(param);
        }

        /// <summary>
        /// 播放按钮点击音效
        /// </summary>
        protected void PlayClickSound()
        {
            CYFrameworkEntry.Instance?.UI?.PlayClickSound();
        }
    }
}
