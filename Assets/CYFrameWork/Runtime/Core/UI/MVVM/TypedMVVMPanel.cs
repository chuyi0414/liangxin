namespace CYFramework.Core.UI.MVVM
{
    /// <summary>
    /// Typed MVVM 面板基类：用于 TypedViewModel
    /// 说明：
    /// - 用于高频刷新 UI（避免 ViewModel 的 object 装箱）
    /// - 在 OnBindUI 创建 ViewModel，在 OnUnbindUI 自动 Dispose
    /// </summary>
    public abstract class TypedMVVMPanel<TViewModel> : UIPanel where TViewModel : TypedViewModel, new()
    {
        /// <summary>
        /// 当前面板使用的 ViewModel
        /// </summary>
        protected TViewModel ViewModel { get; private set; }

        protected override void OnBindUI()
        {
            base.OnBindUI();

            ViewModel = new TViewModel();
            ViewModel.Initialize();
            OnBindViewModel();
        }

        protected override void OnUnbindUI()
        {
            base.OnUnbindUI();

            OnUnbindViewModel();

            ViewModel?.Dispose();
            ViewModel = null;
        }

        /// <summary>
        /// 子类绑定：在这里订阅属性、绑定 UI 控件事件等
        /// </summary>
        protected virtual void OnBindViewModel() { }

        /// <summary>
        /// 子类解绑：在这里清理 UI 控件事件等（属性订阅通常由 ViewModel.Dispose 自动清理）
        /// </summary>
        protected virtual void OnUnbindViewModel() { }
    }
}
