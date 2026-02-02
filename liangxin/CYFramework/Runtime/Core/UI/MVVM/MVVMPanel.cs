// ============================================================================
// CYFramework 2.2 - MVVM 面板基类
// 功能：结合 ViewModel 的 UI 面板，自动数据绑定
// ============================================================================

using System;
using UnityEngine;

namespace CYFramework.Core.UI.MVVM
{
    /// <summary>
    /// MVVM 面板基类
    /// 泛型参数为 ViewModel 类型
    /// </summary>
    /// <typeparam name="TViewModel">ViewModel 类型</typeparam>
    public abstract class MVVMPanel<TViewModel> : UIPanel where TViewModel : ViewModel, new()
    {
        /// <summary>
        /// ViewModel 实例
        /// </summary>
        protected TViewModel ViewModel { get; private set; }
        
        /// <summary>
        /// 绑定 UI
        /// </summary>
        protected override void OnBindUI()
        {
            base.OnBindUI();
            
            // 创建 ViewModel
            ViewModel = new TViewModel();
            ViewModel.Initialize();
            
            // 订阅所有属性变更
            ViewModel.SubscribeAll(OnPropertyChanged);
            
            // 子类绑定
            OnBindViewModel();
        }
        
        /// <summary>
        /// 解绑 UI
        /// </summary>
        protected override void OnUnbindUI()
        {
            base.OnUnbindUI();
            
            // 解绑子类
            OnUnbindViewModel();
            
            // 销毁 ViewModel
            if (ViewModel != null)
            {
                ViewModel.Dispose();
                ViewModel = null;
            }
        }
        
        /// <summary>
        /// 属性变更回调
        /// </summary>
        /// <param name="args">属性变更参数</param>
        private void OnPropertyChanged(ref PropertyChangedEventArgs args)
        {
            OnViewModelPropertyChanged(args.PropertyName, args.OldValue, args.NewValue);
        }
        
        #region 子类实现
        
        /// <summary>
        /// 绑定 ViewModel（设置数据绑定）
        /// </summary>
        protected virtual void OnBindViewModel() { }
        
        /// <summary>
        /// 解绑 ViewModel
        /// </summary>
        protected virtual void OnUnbindViewModel() { }
        
        /// <summary>
        /// ViewModel 属性变更时调用
        /// 子类重写此方法来响应数据变化
        /// </summary>
        /// <param name="propertyName">变更的属性名</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        protected virtual void OnViewModelPropertyChanged(string propertyName, object oldValue, object newValue) { }
        
        #endregion
    }
}

