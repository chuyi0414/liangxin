// ============================================================================
// CYFramework 2.2 - 通用对话框组件
// 功能：确认框、提示框、输入框等通用对话框
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;

namespace CYFramework.Core.UI.Components
{
    /// <summary>
    /// 对话框类型
    /// </summary>
    public enum DialogType
    {
        /// <summary>
        /// 仅确认按钮
        /// </summary>
        Alert,
        
        /// <summary>
        /// 确认 + 取消按钮
        /// </summary>
        Confirm,
        
        /// <summary>
        /// 带输入框
        /// </summary>
        Input
    }
    
    /// <summary>
    /// 对话框配置
    /// </summary>
    public class DialogConfig
    {
        /// <summary>
        /// 标题文本
        /// </summary>
        public string Title = "提示";

        /// <summary>
        /// 内容文本
        /// </summary>
        public string Content = "";

        /// <summary>
        /// 确认按钮文本
        /// </summary>
        public string ConfirmText = "确定";

        /// <summary>
        /// 取消按钮文本
        /// </summary>
        public string CancelText = "取消";

        /// <summary>
        /// 输入框占位符
        /// </summary>
        public string InputPlaceholder = "请输入...";

        /// <summary>
        /// 输入框默认值
        /// </summary>
        public string InputDefaultValue = "";

        /// <summary>
        /// 对话框类型
        /// </summary>
        public DialogType Type = DialogType.Confirm;

        /// <summary>
        /// 确认回调
        /// </summary>
        public Action OnConfirm;

        /// <summary>
        /// 取消回调
        /// </summary>
        public Action OnCancel;

        /// <summary>
        /// 输入确认回调
        /// </summary>
        public Action<string> OnInputConfirm;

        /// <summary>
        /// 是否允许点击遮罩关闭
        /// </summary>
        public bool CloseOnMaskClick = false;
    }
    
    /// <summary>
    /// 通用对话框
    /// </summary>
    [UIPrefab("UI/Panels/UIDialog")]
    public class UIDialog : UIPanel
    {
        [Header("UI 引用")]
        /// <summary>
        /// 标题文本
        /// </summary>
        [SerializeField] private Text _titleText;
        /// <summary>
        /// 内容文本
        /// </summary>
        [SerializeField] private Text _contentText;
        /// <summary>
        /// 确认按钮
        /// </summary>
        [SerializeField] private Button _confirmButton;
        /// <summary>
        /// 确认按钮文本
        /// </summary>
        [SerializeField] private Text _confirmButtonText;
        /// <summary>
        /// 取消按钮
        /// </summary>
        [SerializeField] private Button _cancelButton;
        /// <summary>
        /// 取消按钮文本
        /// </summary>
        [SerializeField] private Text _cancelButtonText;
        /// <summary>
        /// 输入框
        /// </summary>
        [SerializeField] private InputField _inputField;
        /// <summary>
        /// 输入框容器
        /// </summary>
        [SerializeField] private GameObject _inputContainer;
        /// <summary>
        /// 遮罩按钮
        /// </summary>
        [SerializeField] private Button _maskButton;
        
        // 配置
        /// <summary>
        /// 当前对话框配置
        /// </summary>
        private DialogConfig _config;
        
        // 属性重写
        /// <summary>
        /// 所属 UI 层级
        /// </summary>
        public override UILayer Layer => UILayer.Popup;

        /// <summary>
        /// 是否可入栈
        /// </summary>
        public override bool IsStackable => true;

        /// <summary>
        /// 是否可池化
        /// </summary>
        public override bool IsPoolable => true;

        /// <summary>
        /// 是否显示遮罩
        /// </summary>
        public override bool ShowMask => true;
        
        #region 生命周期
        
        /// <summary>
        /// 绑定 UI
        /// </summary>
        protected override void OnBindUI()
        {
            base.OnBindUI();
            
            // 绑定按钮事件
            _confirmButton?.onClick.AddListener(OnConfirmClicked);
            _cancelButton?.onClick.AddListener(OnCancelClicked);
            _maskButton?.onClick.AddListener(OnMaskClicked);
        }
        
        /// <summary>
        /// 解绑 UI
        /// </summary>
        protected override void OnUnbindUI()
        {
            base.OnUnbindUI();
            
            // 解绑按钮事件
            _confirmButton?.onClick.RemoveListener(OnConfirmClicked);
            _cancelButton?.onClick.RemoveListener(OnCancelClicked);
            _maskButton?.onClick.RemoveListener(OnMaskClicked);
        }
        
        /// <summary>
        /// 打开对话框
        /// </summary>
        protected override void OnOpen(object userData)
        {
            // userData 为对话框配置
            _config = userData as DialogConfig ?? new DialogConfig();
            
            // 设置标题和内容
            if (_titleText != null)
                _titleText.text = _config.Title;
            
            if (_contentText != null)
                _contentText.text = _config.Content;
            
            // 设置按钮文本
            if (_confirmButtonText != null)
                _confirmButtonText.text = _config.ConfirmText;
            
            if (_cancelButtonText != null)
                _cancelButtonText.text = _config.CancelText;
            
            // 根据类型显示/隐藏元素
            switch (_config.Type)
            {
                case DialogType.Alert:
                    _cancelButton?.gameObject.SetActive(false);
                    _inputContainer?.SetActive(false);
                    break;
                    
                case DialogType.Confirm:
                    _cancelButton?.gameObject.SetActive(true);
                    _inputContainer?.SetActive(false);
                    break;
                    
                case DialogType.Input:
                    _cancelButton?.gameObject.SetActive(true);
                    _inputContainer?.SetActive(true);
                    if (_inputField != null)
                    {
                        // 设置输入框默认值与占位符
                        _inputField.text = _config.InputDefaultValue;
                        _inputField.placeholder.GetComponent<Text>().text = _config.InputPlaceholder;
                    }
                    break;
            }
        }
        
        /// <summary>
        /// 隐藏对话框
        /// </summary>
        protected override void OnHide()
        {
            _config = null;
        }
        
        #endregion
        
        #region 事件处理
        
        /// <summary>
        /// 确认按钮回调
        /// </summary>
        private void OnConfirmClicked()
        {
            // Input 类型需要回传输入内容
            if (_config.Type == DialogType.Input)
            {
                _config.OnInputConfirm?.Invoke(_inputField?.text ?? "");
            }
            else
            {
                _config.OnConfirm?.Invoke();
            }
            
            CloseSelf();
        }
        
        /// <summary>
        /// 取消按钮回调
        /// </summary>
        private void OnCancelClicked()
        {
            _config.OnCancel?.Invoke();
            CloseSelf();
        }
        
        /// <summary>
        /// 点击遮罩回调
        /// </summary>
        private void OnMaskClicked()
        {
            // 遮罩点击关闭需显式开启
            if (_config.CloseOnMaskClick)
            {
                OnCancelClicked();
            }
        }
        
        #endregion
        
        #region 静态快捷方法
        
        /// <summary>
        /// 显示提示框（仅确认按钮）
        /// </summary>
        public static void Alert(string content, string title = "提示", Action onConfirm = null)
        {
            var manager = CYFramework.Infrastructure.ServiceLocator.Get<UIManager>(); // UI 管理器
            manager?.Open<UIDialog>(new DialogConfig
            {
                Type = DialogType.Alert,
                Title = title,
                Content = content,
                OnConfirm = onConfirm
            });
        }
        
        /// <summary>
        /// 显示确认框
        /// </summary>
        public static void Confirm(string content, Action onConfirm, Action onCancel = null, string title = "确认")
        {
            var manager = CYFramework.Infrastructure.ServiceLocator.Get<UIManager>(); // UI 管理器
            manager?.Open<UIDialog>(new DialogConfig
            {
                Type = DialogType.Confirm,
                Title = title,
                Content = content,
                OnConfirm = onConfirm,
                OnCancel = onCancel
            });
        }
        
        /// <summary>
        /// 显示输入框
        /// </summary>
        public static void Input(string content, Action<string> onConfirm, string defaultValue = "", string title = "输入")
        {
            var manager = CYFramework.Infrastructure.ServiceLocator.Get<UIManager>(); // UI 管理器
            manager?.Open<UIDialog>(new DialogConfig
            {
                Type = DialogType.Input,
                Title = title,
                Content = content,
                InputDefaultValue = defaultValue,
                OnInputConfirm = onConfirm
            });
        }
        
        #endregion
    }
}

