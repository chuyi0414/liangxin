// ============================================================================
// CYFramework 2.2 - Loading 加载界面组件
// 功能：全屏加载遮罩，支持进度条和提示文本
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CYFramework.Core.UI.Components
{
    /// <summary>
    /// Loading 配置
    /// </summary>
    public class LoadingConfig
    {
        /// <summary>
        /// 提示文本
        /// </summary>
        public string Tips = "加载中...";

        /// <summary>
        /// 是否显示进度条
        /// </summary>
        public bool ShowProgress = true;

        /// <summary>
        /// 最小显示时间（防止闪烁）
        /// </summary>
        public float MinDisplayTime = 0.5f;  // 最小显示时间，防止闪烁
    }
    
    /// <summary>
    /// Loading 加载界面
    /// </summary>
    [UIPrefab("UI/Panels/UILoading")]
    public class UILoading : UIPanel
    {
        [Header("UI 引用")]
        /// <summary>
        /// 提示文本
        /// </summary>
        [SerializeField] private Text _tipsText;
        /// <summary>
        /// 进度条
        /// </summary>
        [SerializeField] private Image _progressBar;
        /// <summary>
        /// 进度文本
        /// </summary>
        [SerializeField] private Text _progressText;
        /// <summary>
        /// 进度容器
        /// </summary>
        [SerializeField] private GameObject _progressContainer;
        /// <summary>
        /// 加载图标
        /// </summary>
        [SerializeField] private Image _loadingIcon;
        /// <summary>
        /// 旋转速度
        /// </summary>
        [SerializeField] private float _rotateSpeed = 360f;
        
        // 配置
        /// <summary>
        /// 当前配置
        /// </summary>
        private LoadingConfig _config;
        
        // 状态
        /// <summary>
        /// 当前进度
        /// </summary>
        private float _currentProgress;
        /// <summary>
        /// 目标进度
        /// </summary>
        private float _targetProgress;
        /// <summary>
        /// 显示时长
        /// </summary>
        private float _showTime;
        /// <summary>
        /// 是否处于完成中
        /// </summary>
        private bool _isCompleting;
        /// <summary>
        /// 完成回调
        /// </summary>
        private Action _onComplete;
        
        // 属性重写
        /// <summary>
        /// 所属 UI 层级
        /// </summary>
        public override UILayer Layer => UILayer.Loading;

        /// <summary>
        /// 是否可入栈
        /// </summary>
        public override bool IsStackable => false;

        /// <summary>
        /// 是否可池化
        /// </summary>
        public override bool IsPoolable => true;

        /// <summary>
        /// 是否启用面板动画
        /// </summary>
        public override bool EnableAnimation => false;
        
        // 单例引用
        /// <summary>
        /// 单例实例
        /// </summary>
        private static UILoading _instance;
        
        #region 生命周期
        
        /// <summary>
        /// 打开加载界面
        /// </summary>
        protected override void OnOpen(object userData)
        {
            _instance = this;
            // userData 为配置
            _config = userData as LoadingConfig ?? new LoadingConfig();
            
            // 初始化状态
            _currentProgress = 0f;
            _targetProgress = 0f;
            _showTime = 0f;
            _isCompleting = false;
            _onComplete = null;
            
            // 设置 UI
            if (_tipsText != null)
                _tipsText.text = _config.Tips;
            
            _progressContainer?.SetActive(_config.ShowProgress);
            UpdateProgressUI();
        }
        
        /// <summary>
        /// 隐藏加载界面
        /// </summary>
        protected override void OnHide()
        {
            _instance = null;
            _config = null;
        }
        
        /// <summary>
        /// Unity Update
        /// </summary>
        private void Update()
        {
            // 累计显示时间
            _showTime += Time.unscaledDeltaTime;
            
            // 旋转加载图标
            if (_loadingIcon != null)
            {
                _loadingIcon.transform.Rotate(0, 0, -_rotateSpeed * Time.unscaledDeltaTime);
            }
            
            // 平滑进度
            if (_currentProgress < _targetProgress)
            {
                _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, Time.unscaledDeltaTime * 2f);
                UpdateProgressUI();
            }
            
            // 完成检测
            if (_isCompleting && _currentProgress >= 1f)
            {
                // 确保最小显示时间
                float remainTime = _config.MinDisplayTime - _showTime; // 剩余等待时长
                if (remainTime > 0)
                {
                    StartCoroutine(DelayComplete(remainTime));
                }
                else
                {
                    DoComplete();
                }
                _isCompleting = false;
            }
        }
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 设置进度 (0-1)
        /// </summary>
        public void SetProgress(float progress)
        {
            _targetProgress = Mathf.Clamp01(progress);
        }
        
        /// <summary>
        /// 设置提示文本
        /// </summary>
        public void SetTips(string tips)
        {
            if (_tipsText != null)
                _tipsText.text = tips;
        }
        
        /// <summary>
        /// 完成加载
        /// </summary>
        public void Complete(Action onComplete = null)
        {
            _targetProgress = 1f;
            _isCompleting = true;
            _onComplete = onComplete;
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 刷新进度显示
        /// </summary>
        private void UpdateProgressUI()
        {
            if (_progressBar != null)
            {
                _progressBar.fillAmount = _currentProgress;
            }
            
            if (_progressText != null)
            {
                _progressText.text = $"{Mathf.RoundToInt(_currentProgress * 100)}%";
            }
        }
        
        /// <summary>
        /// 延迟完成协程
        /// </summary>
        private IEnumerator DelayComplete(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            DoComplete();
        }
        
        /// <summary>
        /// 完成加载
        /// </summary>
        private void DoComplete()
        {
            _onComplete?.Invoke();
            CloseSelf();
        }
        
        #endregion
        
        #region 静态快捷方法
        
        /// <summary>
        /// 显示 Loading
        /// </summary>
        public static UILoading Show(string tips = "加载中...", bool showProgress = true)
        {
            var manager = CYFramework.Infrastructure.ServiceLocator.Get<UIManager>(); // UI 管理器
            return manager?.Open<UILoading>(new LoadingConfig
            {
                Tips = tips,
                ShowProgress = showProgress
            });
        }
        
        /// <summary>
        /// 隐藏 Loading
        /// </summary>
        public static void Hide(Action onComplete = null)
        {
            if (_instance != null)
            {
                _instance.Complete(onComplete);
            }
            else
            {
                onComplete?.Invoke();
            }
        }
        
        /// <summary>
        /// 设置当前进度
        /// </summary>
        public static void Progress(float progress)
        {
            _instance?.SetProgress(progress);
        }
        
        /// <summary>
        /// 设置当前提示
        /// </summary>
        public static void Tips(string tips)
        {
            _instance?.SetTips(tips);
        }
        
        /// <summary>
        /// 执行带 Loading 的异步操作
        /// </summary>
        public static IEnumerator WithLoading(IEnumerator operation, string tips = "加载中...")
        {
            Show(tips, true);
            yield return operation;
            Hide();
        }
        
        #endregion
    }
}

