// ============================================================================
// CYFramework - 运行时对话框（纯代码生成，无需 Prefab）
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;

namespace CYFramework.Core.UI.Components
{
    /// <summary>
    /// 运行时对话框 - 纯代码生成，无需 Prefab
    /// </summary>
    public static class RuntimeDialog
    {
        /// <summary>
        /// 当前对话框实例
        /// </summary>
        private static GameObject _dialogInstance;
        
        /// <summary>
        /// 显示对话框
        /// </summary>
        public static void Show(string title, string content, 
            string confirmText = "确定", 
            string cancelText = "取消",
            Action onConfirm = null, 
            Action onCancel = null)
        {
            // 如果已有对话框，先销毁
            if (_dialogInstance != null)
            {
                UnityEngine.Object.Destroy(_dialogInstance);
            }
            
            // 创建 Canvas
            var canvas = CreateCanvas(); // 画布
            _dialogInstance = canvas.gameObject;
            
            // 创建遮罩
            var mask = CreateMask(canvas.transform); // 遮罩
            
            // 创建对话框面板
            var panel = CreatePanel(canvas.transform); // 面板
            
            // 创建标题
            CreateText(panel, title, 24, FontStyle.Bold, TextAnchor.MiddleCenter, 
                new Vector2(0, 60), new Vector2(280, 40));
            
            // 创建内容
            CreateText(panel, content, 18, FontStyle.Normal, TextAnchor.MiddleCenter, 
                new Vector2(0, 0), new Vector2(280, 60));
            
            // 创建按钮容器
            var buttonContainer = new GameObject("Buttons"); // 按钮容器
            buttonContainer.transform.SetParent(panel, false);
            var rt = buttonContainer.AddComponent<RectTransform>(); // 容器 RectTransform
            rt.anchoredPosition = new Vector2(0, -60);
            rt.sizeDelta = new Vector2(280, 40);
            
            var hlg = buttonContainer.AddComponent<HorizontalLayoutGroup>(); // 水平布局
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            
            // 创建取消按钮（如果有）
            if (!string.IsNullOrEmpty(cancelText))
            {
                CreateButton(buttonContainer.transform, cancelText, new Color(0.7f, 0.7f, 0.7f), () =>
                {
                    Close();
                    onCancel?.Invoke();
                });
            }
            
            // 创建确认按钮
            CreateButton(buttonContainer.transform, confirmText, new Color(0.2f, 0.6f, 1f), () =>
            {
                Close();
                onConfirm?.Invoke();
            });
        }
        
        /// <summary>
        /// 关闭对话框
        /// </summary>
        public static void Close()
        {
            if (_dialogInstance != null)
            {
                UnityEngine.Object.Destroy(_dialogInstance);
                _dialogInstance = null;
            }
        }
        
        #region 私有方法 - 创建 UI 元素
        
        /// <summary>
        /// 创建 Canvas
        /// </summary>
        private static Canvas CreateCanvas()
        {
            var go = new GameObject("RuntimeDialog"); // 根节点
            UnityEngine.Object.DontDestroyOnLoad(go);
            
            var canvas = go.AddComponent<Canvas>(); // 画布
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            
            return canvas;
        }
        
        /// <summary>
        /// 创建遮罩
        /// </summary>
        private static Image CreateMask(Transform parent)
        {
            var go = new GameObject("Mask"); // 遮罩节点
            go.transform.SetParent(parent, false);
            
            var rt = go.AddComponent<RectTransform>(); // 遮罩 RectTransform
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            
            var image = go.AddComponent<Image>(); // 遮罩图
            image.color = new Color(0, 0, 0, 0.5f);
            
            return image;
        }
        
        /// <summary>
        /// 创建面板
        /// </summary>
        private static RectTransform CreatePanel(Transform parent)
        {
            var go = new GameObject("Panel"); // 面板节点
            go.transform.SetParent(parent, false);
            
            var rt = go.AddComponent<RectTransform>(); // 面板 RectTransform
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320, 200);
            
            var image = go.AddComponent<Image>(); // 面板背景
            image.color = Color.white;
            
            return rt;
        }
        
        /// <summary>
        /// 创建文本
        /// </summary>
        private static Text CreateText(Transform parent, string text, int fontSize,
            FontStyle fontStyle, TextAnchor alignment, Vector2 position, Vector2 size)
        {
            var go = new GameObject("Text"); // 文本节点
            go.transform.SetParent(parent, false);
            
            var rt = go.AddComponent<RectTransform>(); // 文本 RectTransform
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            
            var txt = go.AddComponent<Text>(); // 文本组件
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = fontStyle;
            txt.alignment = alignment;
            txt.color = Color.black;
            
            return txt;
        }
        
        /// <summary>
        /// 创建按钮
        /// </summary>
        private static Button CreateButton(Transform parent, string text, Color bgColor, Action onClick)
        {
            var go = new GameObject("Button"); // 按钮节点
            go.transform.SetParent(parent, false);
            
            var rt = go.AddComponent<RectTransform>(); // 按钮 RectTransform
            rt.sizeDelta = new Vector2(100, 36);
            
            var image = go.AddComponent<Image>(); // 按钮背景
            image.color = bgColor;
            
            var btn = go.AddComponent<Button>(); // 按钮组件
            btn.targetGraphic = image;
            btn.onClick.AddListener(() => onClick?.Invoke());
            
            // 按钮文字
            var textGo = new GameObject("Text"); // 文本节点
            textGo.transform.SetParent(go.transform, false);
            
            var textRt = textGo.AddComponent<RectTransform>(); // 文本 RectTransform
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            
            var txt = textGo.AddComponent<Text>(); // 文本组件
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 16;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            
            return btn;
        }
        
        #endregion
    }
}
