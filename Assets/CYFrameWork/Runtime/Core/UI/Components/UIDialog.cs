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
        public string Title = "提示";
        public string Content = "";
        public string ConfirmText = "确定";
        public string CancelText = "取消";
        public string InputPlaceholder = "请输入...";
        public string InputDefaultValue = "";
        public DialogType Type = DialogType.Confirm;
        public Action OnConfirm;
        public Action OnCancel;
        public Action<string> OnInputConfirm;
        public bool CloseOnMaskClick = false;
    }
    
    /// <summary>
    /// 通用对话框
    /// </summary>
    [UIPrefab("UI/Panels/UIDialog")]
    public class UIDialog : UIPanel
    {
        [Header("UI 引用")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _contentText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Text _confirmButtonText;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Text _cancelButtonText;
        [SerializeField] private InputField _inputField;
        [SerializeField] private GameObject _inputContainer;
        [SerializeField] private Button _maskButton;
        
        // 配置
        private DialogConfig _config;
        
        // 属性重写
        public override UILayer Layer => UILayer.Popup;
        public override bool IsStackable => true;
        public override bool IsPoolable => true;
        public override bool ShowMask => true;
        
        #region 生命周期
        
        protected override void OnBindUI()
        {
            base.OnBindUI();
            
            _confirmButton?.onClick.AddListener(OnConfirmClicked);
            _cancelButton?.onClick.AddListener(OnCancelClicked);
            _maskButton?.onClick.AddListener(OnMaskClicked);
        }
        
        protected override void OnUnbindUI()
        {
            base.OnUnbindUI();
            
            _confirmButton?.onClick.RemoveListener(OnConfirmClicked);
            _cancelButton?.onClick.RemoveListener(OnCancelClicked);
            _maskButton?.onClick.RemoveListener(OnMaskClicked);
        }
        
        protected override void OnOpen(object userData)
        {
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
                        _inputField.text = _config.InputDefaultValue;
                        _inputField.placeholder.GetComponent<Text>().text = _config.InputPlaceholder;
                    }
                    break;
            }
        }
        
        protected override void OnHide()
        {
            _config = null;
        }
        
        #endregion
        
        #region 事件处理
        
        private void OnConfirmClicked()
        {
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
        
        private void OnCancelClicked()
        {
            _config.OnCancel?.Invoke();
            CloseSelf();
        }
        
        private void OnMaskClicked()
        {
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
            var manager = CYFramework.Infrastructure.ServiceLocator.Get<UIManager>();
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
            var manager = CYFramework.Infrastructure.ServiceLocator.Get<UIManager>();
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
            var manager = CYFramework.Infrastructure.ServiceLocator.Get<UIManager>();
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

