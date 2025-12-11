// ============================================================================
// CYFramework 2.2 - UI 管理器
// 功能：统一管理 UI 面板的加载、显示、隐藏、层级、对象池
// ============================================================================

using System;
using System.Collections.Generic;
using CYFramework.Core.Config;
using CYFramework.Core.Pool;
using CYFramework.Core.Resource;
using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Core.UI
{
    /// <summary>
    /// UI 层级定义
    /// </summary>
    public enum UILayer
    {
        /// <summary>
        /// 背景层（最底层，如主界面背景）
        /// </summary>
        Background = 0,
        
        /// <summary>
        /// 主界面层（主要游戏 UI）
        /// </summary>
        Main = 100,
        
        /// <summary>
        /// 弹窗层（普通弹窗）
        /// </summary>
        Popup = 200,
        
        /// <summary>
        /// 提示层（Toast、Tips）
        /// </summary>
        Tips = 300,
        
        /// <summary>
        /// 引导层（新手引导遮罩）
        /// </summary>
        Guide = 400,
        
        /// <summary>
        /// 加载层（全屏 Loading）
        /// </summary>
        Loading = 500,
        
        /// <summary>
        /// 系统层（最顶层，系统级弹窗）
        /// </summary>
        System = 600
    }
    
    /// <summary>
    /// UI 配置
    /// </summary>
    [Serializable]
    public class UIConfig
    {
        /// <summary>
        /// UI 预制体路径前缀
        /// </summary>
        public string PrefabPathPrefix = "UI/Panels/";
        
        /// <summary>
        /// 是否启用对象池
        /// </summary>
        public bool EnablePool = true;
        
        /// <summary>
        /// 对象池默认容量（每种面板类型最多缓存数量）
        /// </summary>
        public int PoolCapacity = 1;
        
        /// <summary>
        /// 默认淡入淡出时间
        /// </summary>
        public float DefaultFadeDuration = 0.2f;
    }
    
    /// <summary>
    /// UI 管理器
    /// 负责 UI 面板的生命周期管理
    /// </summary>
    public class UIManager : IInitializable, IUpdateable, ILateUpdateable, IDisposableEx
    {
        // 配置
        private UIConfig _config;
        
        // 根节点
        private Transform _uiRoot;
        private Camera _uiCamera;
        private Canvas _rootCanvas;
        
        // 层级容器
        private readonly Dictionary<UILayer, Transform> _layerContainers = new();
        
        // 自定义层级容器
        private readonly Dictionary<string, Transform> _customLayers = new();
        
        // 已打开的面板
        private readonly Dictionary<Type, UIPanel> _openedPanels = new();
        
        // 面板栈（用于返回逻辑）
        private readonly Stack<UIPanel> _panelStack = new();
        
        // 缓存的面板（对象池）
        private readonly Dictionary<Type, Queue<UIPanel>> _panelPool = new();
        
        // 预加载的预制体
        private readonly Dictionary<string, GameObject> _prefabCache = new();
        
        // 资源加载器
        private IResourceLoader _resourceLoader;
        
        public int InitOrder => 50;
        public int UpdateOrder => 100;      // UI 在实体之后更新
        public int LateUpdateOrder => 100;
        public int DisposeOrder => 50;
        
        #region 生命周期
        
        public void Initialize()
        {
            _config = new UIConfig();
            _resourceLoader = ServiceLocator.Get<IResourceLoader>();
            
            // 从 CYConfigurator 读取配置
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                // 读取资源路径配置
                var resourceConfig = configurator.GetConfig<ResourceLoaderConfig>();
                if (resourceConfig != null)
                {
                    _config.PrefabPathPrefix = resourceConfig.UIPanelPath;
                }
                
                // 读取 UI 管理器配置
                var externalConfig = configurator.GetConfig<UIManagerConfig>();
                if (externalConfig != null)
                {
                    _config.EnablePool = externalConfig.EnablePanelPool;
                    _config.PoolCapacity = externalConfig.PanelPoolCapacity;
                    _config.DefaultFadeDuration = externalConfig.DefaultAnimDuration;
                    CYLog.Debug("[UIManager] 使用 CYConfigurator 配置");
                }
            }
            
            // 创建 UI 根节点
            CreateUIRoot();
            
            // 创建配置中的自定义层级
            if (configurator != null)
            {
                var uiConfig = configurator.GetConfig<UIManagerConfig>();
                if (uiConfig?.CustomLayers != null)
                {
                    foreach (var layer in uiConfig.CustomLayers)
                    {
                        if (!string.IsNullOrEmpty(layer.Name))
                        {
                            CreateLayer(layer.Name, layer.SortOrder);
                        }
                    }
                }
            }
            
            CYLog.Info("[UIManager] 初始化完成");
        }
        
        public void OnUpdate(float deltaTime)
        {
            // 驱动所有已打开面板的 Update
            float realDeltaTime = Time.unscaledDeltaTime;
            foreach (var panel in _openedPanels.Values)
            {
                if (panel != null && panel.IsOpened)
                {
                    panel.InternalUpdate(deltaTime, realDeltaTime);
                }
            }
        }
        
        public void OnLateUpdate(float deltaTime)
        {
            // 驱动所有已打开面板的 LateUpdate
            float realDeltaTime = Time.unscaledDeltaTime;
            foreach (var panel in _openedPanels.Values)
            {
                if (panel != null && panel.IsOpened)
                {
                    panel.InternalLateUpdate(deltaTime, realDeltaTime);
                }
            }
        }
        
        public void Dispose()
        {
            // 关闭所有面板（标记为系统关闭）
            CloseAll(isShutdown: true);
            
            // 清理缓存
            _prefabCache.Clear();
            _panelPool.Clear();
            
            if (_uiRoot != null)
            {
                UnityEngine.Object.Destroy(_uiRoot.gameObject);
            }
            
            CYLog.Info("[UIManager] 已销毁");
        }
        
        #endregion
        
        #region 公共 API
        
        /// <summary>
        /// 打开面板
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        /// <param name="data">传递给面板的数据</param>
        /// <returns>面板实例，可直接操作</returns>
        public T Open<T>(object data = null) where T : UIPanel
        {
            var panelType = typeof(T);
            
            // 检查是否已打开
            if (_openedPanels.TryGetValue(panelType, out var existingPanel))
            {
                CYLog.Debug($"[UIManager] 面板已打开，刷新: {panelType.Name}");
                existingPanel.InternalRefresh(data);
                return existingPanel as T;
            }
            
            // 获取或创建面板
            var panel = GetOrCreatePanel<T>();
            if (panel == null)
            {
                CYLog.Error($"[UIManager] 创建面板失败: {panelType.Name}");
                return null;
            }
            
            // 设置层级
            var layer = panel.Layer;
            if (_layerContainers.TryGetValue(layer, out var container))
            {
                panel.transform.SetParent(container, false);
            }
            
            // 暂停当前栈顶面板
            if (panel.IsStackable && _panelStack.Count > 0)
            {
                var topPanel = _panelStack.Peek();
                if (topPanel != null && topPanel != panel)
                {
                    topPanel.InternalPause();
                }
            }
            
            // 激活并初始化
            panel.gameObject.SetActive(true);
            panel.InternalInit(data);
            panel.InternalOpen(data);
            
            // 记录
            _openedPanels[panelType] = panel;
            
            // 如果是可堆叠面板，压入栈
            if (panel.IsStackable)
            {
                _panelStack.Push(panel);
            }
            
            CYLog.Debug($"[UIManager] 打开面板: {panelType.Name}");
            
            return panel;
        }
        
        /// <summary>
        /// 打开面板（强类型数据，避免装箱）
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        /// <typeparam name="TData">数据类型（推荐使用 struct）</typeparam>
        /// <param name="data">传递给面板的数据</param>
        /// <returns>面板实例，可直接操作</returns>
        public T Open<T, TData>(TData data) where T : UIPanel where TData : struct
        {
            // TData 约束为 struct，避免装箱
            return Open<T>(data);
        }
        
        /// <summary>
        /// 关闭面板
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        public void Close<T>() where T : UIPanel
        {
            Close(typeof(T));
        }
        
        /// <summary>
        /// 关闭面板（按类型）
        /// </summary>
        public void Close(Type panelType)
        {
            if (!_openedPanels.TryGetValue(panelType, out var panel))
            {
                return;
            }
            
            ClosePanel(panel);
        }
        
        /// <summary>
        /// 关闭面板实例
        /// </summary>
        public void Close(UIPanel panel)
        {
            if (panel == null) return;
            ClosePanel(panel);
        }
        
        /// <summary>
        /// 返回上一个面板
        /// </summary>
        public void Back()
        {
            if (_panelStack.Count == 0)
            {
                CYLog.Debug("[UIManager] 面板栈为空，无法返回");
                return;
            }
            
            var topPanel = _panelStack.Pop();
            ClosePanel(topPanel);
        }
        
        /// <summary>
        /// 关闭所有面板
        /// </summary>
        public void CloseAll(bool isShutdown = false)
        {
            var panelsToClose = new List<UIPanel>(_openedPanels.Values);
            
            foreach (var panel in panelsToClose)
            {
                ClosePanel(panel, isShutdown);
            }
            
            _panelStack.Clear();
        }
        
        /// <summary>
        /// 关闭指定层级的所有面板
        /// </summary>
        public void CloseLayer(UILayer layer)
        {
            var panelsToClose = new List<UIPanel>();
            
            foreach (var panel in _openedPanels.Values)
            {
                if (panel.Layer == layer)
                {
                    panelsToClose.Add(panel);
                }
            }
            
            foreach (var panel in panelsToClose)
            {
                ClosePanel(panel);
            }
        }
        
        /// <summary>
        /// 获取已打开的面板
        /// </summary>
        public T Get<T>() where T : UIPanel
        {
            if (_openedPanels.TryGetValue(typeof(T), out var panel))
            {
                return panel as T;
            }
            return null;
        }
        
        /// <summary>
        /// 检查面板是否已打开
        /// </summary>
        public bool IsOpened<T>() where T : UIPanel
        {
            return _openedPanels.ContainsKey(typeof(T));
        }
        
        /// <summary>
        /// 预加载面板
        /// </summary>
        public void Preload<T>() where T : UIPanel
        {
            var panelType = typeof(T);
            var path = GetPrefabPath(panelType);
            
            if (!_prefabCache.ContainsKey(path))
            {
                var prefab = _resourceLoader.Load<GameObject>(path);
                if (prefab != null)
                {
                    _prefabCache[path] = prefab;
                    CYLog.Debug($"[UIManager] 预加载面板: {panelType.Name}");
                }
            }
        }
        
        /// <summary>
        /// 显示 Toast 提示
        /// </summary>
        public void ShowToast(string message, float duration = 2f)
        {
            // 使用 UIToast 组件
            if (Components.UIToast.Instance != null)
            {
                Components.UIToast.Show(message, duration);
            }
            else
            {
                // 回退到日志输出
                CYLog.Info($"[Toast] {message}");
            }
        }
        
        /// <summary>
        /// 显示成功提示
        /// </summary>
        public void ShowSuccess(string message)
        {
            if (Components.UIToast.Instance != null)
            {
                Components.UIToast.ShowSuccess(message);
            }
            else
            {
                CYLog.Info($"[Toast-Success] {message}");
            }
        }
        
        /// <summary>
        /// 显示错误提示
        /// </summary>
        public void ShowError(string message)
        {
            if (Components.UIToast.Instance != null)
            {
                Components.UIToast.ShowError(message);
            }
            else
            {
                CYLog.Warning($"[Toast-Error] {message}");
            }
        }
        
        /// <summary>
        /// 显示警告提示
        /// </summary>
        public void ShowWarning(string message)
        {
            if (Components.UIToast.Instance != null)
            {
                Components.UIToast.ShowWarning(message);
            }
            else
            {
                CYLog.Warning($"[Toast-Warning] {message}");
            }
        }
        
        /// <summary>
        /// 显示确认对话框
        /// </summary>
        public void ShowConfirm(string title, string content, Action onConfirm, Action onCancel = null)
        {
            var config = new Components.DialogConfig
            {
                Title = title,
                Content = content,
                Type = Components.DialogType.Confirm,
                OnConfirm = onConfirm,
                OnCancel = onCancel
            };
            Open<Components.UIDialog>(config);
        }
        
        /// <summary>
        /// 显示提示框（仅确认按钮）
        /// </summary>
        public void ShowAlert(string title, string content, Action onConfirm = null)
        {
            var config = new Components.DialogConfig
            {
                Title = title,
                Content = content,
                Type = Components.DialogType.Alert,
                OnConfirm = onConfirm
            };
            Open<Components.UIDialog>(config);
        }
        
        #endregion
        
        #region 自定义层级 API
        
        /// <summary>
        /// 创建自定义 UI 层级
        /// </summary>
        /// <param name="layerName">层级名称</param>
        /// <param name="sortOrder">排序顺序（越大越靠前）</param>
        /// <returns>层级 Transform</returns>
        public Transform CreateLayer(string layerName, int sortOrder = 0)
        {
            if (_customLayers.TryGetValue(layerName, out var existing))
            {
                CYLog.Warning($"[UIManager] 层级已存在: {layerName}");
                return existing;
            }
            
            var layerGo = new GameObject(layerName);
            layerGo.layer = LayerMask.NameToLayer("UI");
            
            var rectTransform = layerGo.AddComponent<RectTransform>();
            rectTransform.SetParent(_rootCanvas.transform, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // 设置排序顺序
            var canvas = layerGo.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortOrder;
            layerGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            _customLayers[layerName] = rectTransform;
            CYLog.Debug($"[UIManager] 创建自定义层级: {layerName}, SortOrder: {sortOrder}");
            return rectTransform;
        }
        
        /// <summary>
        /// 批量创建自定义层级
        /// </summary>
        public void CreateLayers(params (string name, int sortOrder)[] layers)
        {
            foreach (var (name, sortOrder) in layers)
            {
                CreateLayer(name, sortOrder);
            }
        }
        
        /// <summary>
        /// 获取自定义层级容器
        /// </summary>
        public Transform GetLayerContainer(string layerName)
        {
            if (_customLayers.TryGetValue(layerName, out var container))
            {
                return container;
            }
            
            // 不存在则创建
            return CreateLayer(layerName, 0);
        }
        
        /// <summary>
        /// 获取预设层级容器
        /// </summary>
        public Transform GetLayerContainer(UILayer layer)
        {
            return _layerContainers.TryGetValue(layer, out var container) ? container : null;
        }
        
        /// <summary>
        /// 检查自定义层级是否存在
        /// </summary>
        public bool HasLayer(string layerName)
        {
            return _customLayers.ContainsKey(layerName);
        }
        
        /// <summary>
        /// 获取所有自定义层级名称
        /// </summary>
        public string[] GetAllCustomLayerNames()
        {
            var names = new string[_customLayers.Count];
            _customLayers.Keys.CopyTo(names, 0);
            return names;
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 创建或获取 UI 根节点
        /// </summary>
        private void CreateUIRoot()
        {
            // 先尝试查找场景中已存在的 UIRoot
            var existingRoot = GameObject.Find("UIRoot");
            if (existingRoot != null)
            {
                _uiRoot = existingRoot.transform;
                UnityEngine.Object.DontDestroyOnLoad(existingRoot);
                
                // 查找已存在的组件
                _uiCamera = existingRoot.GetComponentInChildren<Camera>();
                _rootCanvas = existingRoot.GetComponentInChildren<Canvas>();
                
                // 查找层级容器
                if (_rootCanvas != null)
                {
                    foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
                    {
                        var layerTransform = _rootCanvas.transform.Find(layer.ToString());
                        if (layerTransform != null)
                        {
                            _layerContainers[layer] = layerTransform;
                        }
                        else
                        {
                            // 如果层级不存在，创建它
                            _layerContainers[layer] = CreateLayerContainer(layer);
                        }
                    }
                }
                
                CYLog.Debug("[UIManager] 使用场景中已存在的 UIRoot");
                return;
            }
            
            // 创建根对象
            var rootGo = new GameObject("UIRoot");
            UnityEngine.Object.DontDestroyOnLoad(rootGo);
            _uiRoot = rootGo.transform;
            
            // 创建 UI 相机
            var cameraGo = new GameObject("UICamera");
            cameraGo.transform.SetParent(_uiRoot);
            _uiCamera = cameraGo.AddComponent<Camera>();
            _uiCamera.clearFlags = CameraClearFlags.Depth;
            _uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            _uiCamera.orthographic = true;
            _uiCamera.depth = 100;
            
            // 创建根 Canvas
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(_uiRoot);
            canvasGo.layer = LayerMask.NameToLayer("UI");
            
            _rootCanvas = canvasGo.AddComponent<Canvas>();
            _rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            _rootCanvas.worldCamera = _uiCamera;
            _rootCanvas.sortingOrder = 0;
            
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // 创建层级容器
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                _layerContainers[layer] = CreateLayerContainer(layer);
            }
            
            CYLog.Debug("[UIManager] UI 根节点创建完成");
        }
        
        /// <summary>
        /// 创建层级容器
        /// </summary>
        private Transform CreateLayerContainer(UILayer layer)
        {
            var layerGo = new GameObject(layer.ToString());
            layerGo.layer = LayerMask.NameToLayer("UI");
            
            var rectTransform = layerGo.AddComponent<RectTransform>();
            rectTransform.SetParent(_rootCanvas.transform, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            return layerGo.transform;
        }
        
        /// <summary>
        /// 获取或创建面板
        /// </summary>
        private T GetOrCreatePanel<T>() where T : UIPanel
        {
            var panelType = typeof(T);
            
            // 尝试从对象池获取
            if (_config.EnablePool && _panelPool.TryGetValue(panelType, out var pool) && pool.Count > 0)
            {
                return pool.Dequeue() as T;
            }
            
            // 加载预制体
            var path = GetPrefabPath(panelType);
            GameObject prefab;
            
            if (!_prefabCache.TryGetValue(path, out prefab))
            {
                prefab = _resourceLoader.Load<GameObject>(path);
                if (prefab == null)
                {
                    CYLog.Error($"[UIManager] 找不到预制体: {path}");
                    return null;
                }
                _prefabCache[path] = prefab;
            }
            
            // 实例化
            var go = UnityEngine.Object.Instantiate(prefab);
            var panel = go.GetComponent<T>();
            
            if (panel == null)
            {
                panel = go.AddComponent<T>();
            }
            
            panel.SetManager(this);
            
            return panel;
        }
        
        /// <summary>
        /// 关闭面板
        /// </summary>
        private void ClosePanel(UIPanel panel, bool isShutdown = false)
        {
            var panelType = panel.GetType();
            
            // 调用关闭回调
            panel.InternalClose(isShutdown, null);
            
            // 从记录中移除
            _openedPanels.Remove(panelType);
            
            // 对象池回收或销毁
            if (_config.EnablePool && panel.IsPoolable)
            {
                panel.InternalRecycle();
                panel.gameObject.SetActive(false);
                
                if (!_panelPool.TryGetValue(panelType, out var pool))
                {
                    pool = new Queue<UIPanel>();
                    _panelPool[panelType] = pool;
                }
                
                if (pool.Count < _config.PoolCapacity)
                {
                    pool.Enqueue(panel);
                }
                else
                {
                    UnityEngine.Object.Destroy(panel.gameObject);
                }
            }
            else
            {
                UnityEngine.Object.Destroy(panel.gameObject);
            }
            
            // 恢复栈顶面板
            if (_panelStack.Count > 0)
            {
                var topPanel = _panelStack.Peek();
                if (topPanel != null && topPanel.IsOpened)
                {
                    topPanel.InternalResume();
                }
            }
            
            CYLog.Debug($"[UIManager] 关闭面板: {panelType.Name}");
        }
        
        /// <summary>
        /// 获取预制体路径
        /// </summary>
        private string GetPrefabPath(Type panelType)
        {
            // 检查是否有 UIPrefab 特性
            var attr = Attribute.GetCustomAttribute(panelType, typeof(UIPrefabAttribute)) as UIPrefabAttribute;
            if (attr != null && !string.IsNullOrEmpty(attr.Path))
            {
                return attr.Path;
            }
            
            // 默认路径：UI/Panels/面板类名
            return $"{_config.PrefabPathPrefix}{panelType.Name}";
        }
        
        #endregion
    }
}

