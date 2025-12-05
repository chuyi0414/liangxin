// ============================================================================
// CYFramework - UI 模块
// 管理所有 UI 面板的生命周期
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CYFramework.Runtime.Core.UI
{
    /// <summary>
    /// UI 模块
    /// 负责 UI 面板的加载、打开、关闭、层级管理
    /// </summary>
    public class UIModule : IModule
    {
        public int Priority => 25;
        public bool NeedUpdate => false;

        // UI 根节点
        private GameObject _uiRoot;
        private Canvas _canvas;
        private CanvasScaler _canvasScaler;

        // 各层级的容器
        private Dictionary<UILayer, RectTransform> _layerContainers;

        // 已加载的面板（面板名 -> 面板实例）
        private Dictionary<string, UIPanel> _loadedPanels;

        // 面板栈（用于返回上一个面板）
        private Stack<string> _panelStack;

        // 面板预制体路径前缀
        private string _panelPathPrefix = "UI/Panels/";

        // UI 点击音效路径
        private string _clickSoundPath = "";

        public void Initialize()
        {
            _loadedPanels = new Dictionary<string, UIPanel>();
            _panelStack = new Stack<string>();
            _layerContainers = new Dictionary<UILayer, RectTransform>();

            // 创建 UI 根节点
            CreateUIRoot();

            Log.I("UIModule", "初始化完成");
        }

        public void Update(float deltaTime) { }

        public void Shutdown()
        {
            // 关闭所有面板
            CloseAllPanels();

            // 销毁 UI 根节点
            if (_uiRoot != null)
            {
                UnityEngine.Object.Destroy(_uiRoot);
                _uiRoot = null;
            }

            Log.I("UIModule", "已关闭");
        }

        // ====================================================================
        // 初始化
        // ====================================================================

        /// <summary>
        /// 创建 UI 根节点
        /// </summary>
        private void CreateUIRoot()
        {
            // 创建根物体
            _uiRoot = new GameObject("UIRoot");
            UnityEngine.Object.DontDestroyOnLoad(_uiRoot);

            // 添加 Canvas
            _canvas = _uiRoot.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 0;

            // 添加 CanvasScaler
            _canvasScaler = _uiRoot.AddComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920, 1080);
            _canvasScaler.matchWidthOrHeight = 0.5f;

            // 添加 GraphicRaycaster
            _uiRoot.AddComponent<GraphicRaycaster>();

            // 创建各层级容器
            CreateLayerContainers();
        }

        /// <summary>
        /// 创建层级容器
        /// </summary>
        private void CreateLayerContainers()
        {
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                GameObject layerObj = new GameObject(layer.ToString());
                layerObj.transform.SetParent(_uiRoot.transform, false);

                RectTransform rect = layerObj.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                _layerContainers[layer] = rect;
            }
        }

        // ====================================================================
        // 面板操作
        // ====================================================================

        /// <summary>
        /// 设置面板预制体路径前缀
        /// </summary>
        public void SetPanelPathPrefix(string prefix)
        {
            _panelPathPrefix = prefix;
        }

        /// <summary>
        /// 设置 UI 点击音效路径
        /// </summary>
        public void SetClickSoundPath(string path)
        {
            _clickSoundPath = path;
        }

        /// <summary>
        /// 获取点击音效路径
        /// </summary>
        public string ClickSoundPath => _clickSoundPath;

        /// <summary>
        /// 播放 UI 点击音效
        /// </summary>
        public void PlayClickSound()
        {
            if (!string.IsNullOrEmpty(_clickSoundPath))
            {
                CYFrameworkEntry.Instance?.Sound?.PlaySFX(_clickSoundPath);
            }
        }

        /// <summary>
        /// 打开面板（泛型版本）
        /// </summary>
        public T OpenPanel<T>(object param = null) where T : UIPanel
        {
            string panelName = typeof(T).Name;
            return OpenPanel(panelName, param) as T;
        }

        /// <summary>
        /// 打开面板
        /// </summary>
        public UIPanel OpenPanel(string panelName, object param = null)
        {
            // 检查是否已加载
            if (_loadedPanels.TryGetValue(panelName, out UIPanel panel))
            {
                // 已加载，直接显示
                ShowPanelInternal(panel, param);
            }
            else
            {
                // 未加载，先加载
                panel = LoadPanel(panelName);
                if (panel != null)
                {
                    ShowPanelInternal(panel, param);
                }
            }

            // 加入面板栈
            if (panel != null && !_panelStack.Contains(panelName))
            {
                _panelStack.Push(panelName);
            }

            return panel;
        }

        /// <summary>
        /// 加载面板
        /// </summary>
        private UIPanel LoadPanel(string panelName)
        {
            string path = _panelPathPrefix + panelName;
            
            // 使用资源模块加载
            GameObject prefab = CYFrameworkEntry.Instance?.Resource?.Load<GameObject>(path);
            if (prefab == null)
            {
                Log.E("UIModule", $"加载面板失败: {path}");
                return null;
            }

            // 获取面板组件
            UIPanel panelComponent = prefab.GetComponent<UIPanel>();
            if (panelComponent == null)
            {
                Log.E("UIModule", $"面板预制体缺少 UIPanel 组件: {panelName}");
                return null;
            }

            // 实例化
            UILayer layer = panelComponent.Layer;
            RectTransform container = _layerContainers[layer];

            GameObject panelObj = UnityEngine.Object.Instantiate(prefab, container);
            panelObj.name = panelName;

            UIPanel panel = panelObj.GetComponent<UIPanel>();
            panel.State = UIPanelState.Loaded;

            // 调用 OnLoad
            panel.OnLoad();

            // 缓存
            _loadedPanels[panelName] = panel;

            Log.I("UIModule", $"面板已加载: {panelName}");
            return panel;
        }

        /// <summary>
        /// 显示面板
        /// </summary>
        private void ShowPanelInternal(UIPanel panel, object param)
        {
            if (panel == null) return;

            // 设置为活动状态
            panel.gameObject.SetActive(true);

            // 移到最上层
            panel.transform.SetAsLastSibling();

            // 调用生命周期
            if (panel.State == UIPanelState.Loaded || panel.State == UIPanelState.Hidden)
            {
                panel.OnOpen(param);
            }
            panel.OnShow();

            panel.State = UIPanelState.Visible;
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void HidePanel<T>() where T : UIPanel
        {
            HidePanel(typeof(T).Name);
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void HidePanel(string panelName)
        {
            if (!_loadedPanels.TryGetValue(panelName, out UIPanel panel))
                return;

            if (panel.State != UIPanelState.Visible)
                return;

            panel.OnHide();
            panel.gameObject.SetActive(false);
            panel.State = UIPanelState.Hidden;

            Log.I("UIModule", $"面板已隐藏: {panelName}");
        }

        /// <summary>
        /// 关闭面板
        /// </summary>
        public void ClosePanel<T>() where T : UIPanel
        {
            ClosePanel(typeof(T).Name);
        }

        /// <summary>
        /// 关闭面板
        /// </summary>
        public void ClosePanel(string panelName)
        {
            if (!_loadedPanels.TryGetValue(panelName, out UIPanel panel))
                return;

            // 调用生命周期
            if (panel.State == UIPanelState.Visible)
            {
                panel.OnHide();
            }
            panel.OnClose();

            // 从栈中移除
            RemoveFromStack(panelName);

            // 根据是否缓存决定销毁还是隐藏
            if (panel.IsCached)
            {
                panel.gameObject.SetActive(false);
                panel.State = UIPanelState.Hidden;
            }
            else
            {
                panel.OnUnload();
                panel.State = UIPanelState.Destroyed;
                _loadedPanels.Remove(panelName);
                UnityEngine.Object.Destroy(panel.gameObject);
            }

            Log.I("UIModule", $"面板已关闭: {panelName}");
        }

        /// <summary>
        /// 关闭所有面板
        /// </summary>
        public void CloseAllPanels()
        {
            List<string> panelNames = new List<string>(_loadedPanels.Keys);
            foreach (string name in panelNames)
            {
                ClosePanel(name);
            }
            _panelStack.Clear();
        }

        /// <summary>
        /// 返回上一个面板
        /// </summary>
        public void GoBack()
        {
            if (_panelStack.Count <= 1)
            {
                Log.W("UIModule", "没有可返回的面板");
                return;
            }

            // 关闭当前面板
            string current = _panelStack.Pop();
            ClosePanel(current);

            // 显示上一个面板
            if (_panelStack.Count > 0)
            {
                string previous = _panelStack.Peek();
                if (_loadedPanels.TryGetValue(previous, out UIPanel panel))
                {
                    ShowPanelInternal(panel, null);
                }
            }
        }

        // ====================================================================
        // 查询
        // ====================================================================

        /// <summary>
        /// 获取面板
        /// </summary>
        public T GetPanel<T>() where T : UIPanel
        {
            string panelName = typeof(T).Name;
            if (_loadedPanels.TryGetValue(panelName, out UIPanel panel))
            {
                return panel as T;
            }
            return null;
        }

        /// <summary>
        /// 面板是否打开
        /// </summary>
        public bool IsPanelOpen<T>() where T : UIPanel
        {
            return IsPanelOpen(typeof(T).Name);
        }

        /// <summary>
        /// 面板是否打开
        /// </summary>
        public bool IsPanelOpen(string panelName)
        {
            if (_loadedPanels.TryGetValue(panelName, out UIPanel panel))
            {
                return panel.State == UIPanelState.Visible;
            }
            return false;
        }

        /// <summary>
        /// 获取当前顶部面板
        /// </summary>
        public UIPanel GetTopPanel()
        {
            if (_panelStack.Count == 0) return null;
            string topName = _panelStack.Peek();
            _loadedPanels.TryGetValue(topName, out UIPanel panel);
            return panel;
        }

        /// <summary>
        /// 获取 Canvas
        /// </summary>
        public Canvas Canvas => _canvas;

        /// <summary>
        /// 获取层级容器
        /// </summary>
        public RectTransform GetLayerContainer(UILayer layer)
        {
            _layerContainers.TryGetValue(layer, out RectTransform container);
            return container;
        }

        // ====================================================================
        // 辅助
        // ====================================================================

        private void RemoveFromStack(string panelName)
        {
            Stack<string> temp = new Stack<string>();
            while (_panelStack.Count > 0)
            {
                string name = _panelStack.Pop();
                if (name != panelName)
                {
                    temp.Push(name);
                }
            }
            while (temp.Count > 0)
            {
                _panelStack.Push(temp.Pop());
            }
        }
    }
}
