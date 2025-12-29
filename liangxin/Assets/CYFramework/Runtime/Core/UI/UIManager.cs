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

        /// <summary>
        /// Toast 默认显示时长（秒）
        /// </summary>
        public float ToastDuration = 2f;

        /// <summary>
        /// Toast 同时显示的最大数量
        /// </summary>
        public int MaxToastCount = 3;
    }
    
    /// <summary>
    /// UI 管理器
    /// 负责 UI 面板的生命周期管理
    /// </summary>
    public class UIManager : IInitializable, IUpdateable, ILateUpdateable, IDisposableEx
    {
        /// <summary>
        /// UI 管理器运行时配置
        /// </summary>
        private UIConfig _config;
        
        /// <summary>
        /// UI 根节点（UIRoot）
        /// </summary>
        private Transform _uiRoot;
        /// <summary>
        /// UI 相机（UICamera）
        /// </summary>
        private Camera _uiCamera;
        /// <summary>
        /// 根 Canvas（所有 UI 层级容器的父级）
        /// </summary>
        private Canvas _rootCanvas;
        
        /// <summary>
        /// 预设层级容器（UILayer 对应 Transform）
        /// </summary>
        private readonly Dictionary<UILayer, Transform> _layerContainers = new();
        
        /// <summary>
        /// 自定义层级容器（层名 -> Transform）
        /// </summary>
        private readonly Dictionary<string, Transform> _customLayers = new();
        
        /// <summary>
        /// 已打开的面板（类型 -> 实例）
        /// </summary>
        private readonly Dictionary<Type, UIPanel> _openedPanels = new();
        
        /// <summary>
        /// 面板栈（用于返回/恢复逻辑）
        /// </summary>
        private readonly Stack<UIPanel> _panelStack = new();

        /// <summary>
        /// 面板栈整理用临时缓冲（避免频繁分配）
        /// </summary>
        private readonly List<UIPanel> _stackBuffer = new();
        
        /// <summary>
        /// 更新循环用临时列表（避免遍历时集合被修改导致异常）
        /// </summary>
        private readonly List<UIPanel> _updateBuffer = new();
        
        /// <summary>
        /// 面板对象池（类型 -> 队列）
        /// </summary>
        private readonly Dictionary<Type, Queue<UIPanel>> _panelPool = new();

        /// <summary>
        /// 回收前的兄弟顺序缓存，保证从对象池取回后恢复原层级顺序
        /// </summary>
        private readonly Dictionary<UIPanel, int> _panelSiblingIndex = new();

        /// <summary>
        /// 预加载的预制体缓存（路径 -> 预制体）
        /// </summary>
        private readonly Dictionary<string, GameObject> _prefabCache = new();

        /// <summary>
        /// UIPrefab 元信息（路径/层级）
        /// </summary>
        private struct UIPrefabMeta
        {
            public string Path;
            public string LayerName;
            public int SortOrder;
        }

        /// <summary>
        /// UIPrefab 元信息缓存（类型 -> 元信息）
        /// </summary>
        private readonly Dictionary<Type, UIPrefabMeta> _prefabMetaCache = new();

        /// <summary>
        /// UI 对象池根节点（统一存放回收的 UI 面板）
        /// </summary>
        private Transform _uiPoolRoot;
        /// <summary>
        /// UI 对象池根节点是否由 UIManager 运行时创建
        /// </summary>
        private bool _poolRootCreatedByManager;   // 仅在运行时创建时标记，便于退出时销毁
        
        /// <summary>
        /// 资源加载器（由 ServiceLocator 提供）
        /// </summary>
        private IResourceLoader _resourceLoader;
        
        /// <summary>
        /// 初始化顺序
        /// </summary>
        public int InitOrder => 50;
        /// <summary>
        /// Update 顺序（UI 在实体之后更新）
        /// </summary>
        public int UpdateOrder => 100;      // UI 在实体之后更新
        /// <summary>
        /// LateUpdate 顺序
        /// </summary>
        public int LateUpdateOrder => 100;
        /// <summary>
        /// 销毁顺序
        /// </summary>
        public int DisposeOrder => 50;
        
        #region 生命周期
        
        /// <summary>
        /// 初始化 UI 系统
        /// </summary>
        public void Initialize()
        {
            _config = new UIConfig();
            _resourceLoader = ServiceLocator.Get<IResourceLoader>();
            
            // 从 CYConfigurator 读取配置
            // 配置入口实例
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                // 读取资源路径配置
                // 资源加载器配置
                var resourceConfig = configurator.GetConfig<ResourceLoaderConfig>();
                if (resourceConfig != null)
                {
                    _config.PrefabPathPrefix = resourceConfig.UIPanelPath;
                }
                
                // 读取 UI 管理器配置
                // UI 管理器配置
                var externalConfig = configurator.GetConfig<UIManagerConfig>();
                if (externalConfig != null)
                {
                    _config.EnablePool = externalConfig.EnablePanelPool;
                    _config.PoolCapacity = externalConfig.PanelPoolCapacity;
                    _config.DefaultFadeDuration = externalConfig.DefaultAnimDuration;
                    _config.ToastDuration = externalConfig.ToastDuration;
                    _config.MaxToastCount = externalConfig.MaxToastCount;
                    CYLog.Debug("[UIManager] 使用 CYConfigurator 配置");
                    CYLog.Info($"[UIManager] DefaultAnimDuration={_config.DefaultFadeDuration}, EnableAnimationDefault={_config.DefaultFadeDuration > 0f}");
                }
            }
            
            // 创建 UI 根节点
            CreateUIRoot();
            // 创建/获取 UI 对象池根节点，确保关闭入池时移到 [ObjectPools]/UI 下便于区分隐藏与回收
            _uiPoolRoot = GetOrCreateUIPoolRoot();

            // 将 Toast 配置下发到 UIToast 组件（若存在）
            if (Components.UIToast.Instance != null)
            {
                Components.UIToast.Instance.ApplyConfig(_config.MaxToastCount, _config.ToastDuration);
            }
            
            // 创建配置中的自定义层级
            if (configurator != null)
            {
                // UI 配置（用于自定义层级）
                var uiConfig = configurator.GetConfig<UIManagerConfig>();
                if (uiConfig?.CustomLayers != null)
                {
                    // 遍历自定义层级配置
                    foreach (var layer in uiConfig.CustomLayers)
                    {
                        // 当前层级配置项
                        if (!string.IsNullOrEmpty(layer.Name))
                        {
                            CreateLayer(layer.Name, layer.SortOrder);
                        }
                    }
                }
            }
            
            CYLog.Info("[UIManager] 初始化完成");
        }
        
        /// <summary>
        /// 驱动 UI 面板 Update
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            // 驱动所有已打开面板的 Update
            // 使用临时列表避免遍历时集合被修改导致 InvalidOperationException
            // 真实时间增量（不受 Time.timeScale 影响）
            float realDeltaTime = Time.unscaledDeltaTime;
            
            _updateBuffer.Clear();
            _updateBuffer.AddRange(_openedPanels.Values);
            
            // 遍历索引
            for (int i = 0; i < _updateBuffer.Count; i++)
            {
                // 当前面板
                var panel = _updateBuffer[i];
                if (panel != null && panel.IsOpened)
                {
                    panel.InternalUpdate(deltaTime, realDeltaTime);
                }
            }
        }
        
        /// <summary>
        /// 驱动 UI 面板 LateUpdate
        /// </summary>
        public void OnLateUpdate(float deltaTime)
        {
            // 驱动所有已打开面板的 LateUpdate
            // 使用临时列表避免遍历时集合被修改导致 InvalidOperationException
            // 真实时间增量（不受 Time.timeScale 影响）
            float realDeltaTime = Time.unscaledDeltaTime;
            
            _updateBuffer.Clear();
            _updateBuffer.AddRange(_openedPanels.Values);
            
            // 遍历索引
            for (int i = 0; i < _updateBuffer.Count; i++)
            {
                // 当前面板
                var panel = _updateBuffer[i];
                if (panel != null && panel.IsOpened)
                {
                    panel.InternalLateUpdate(deltaTime, realDeltaTime);
                }
            }
        }
        
        /// <summary>
        /// 销毁 UI 系统并释放资源
        /// </summary>
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

            // 若对象池根节点由 UIManager 运行时创建，为避免退出场景残留，将其销毁
            if (_poolRootCreatedByManager && _uiPoolRoot != null)
            {
                UnityEngine.Object.Destroy(_uiPoolRoot.gameObject);
                _uiPoolRoot = null;
                _poolRootCreatedByManager = false;
            }
            
            CYLog.Info("[UIManager] 已销毁");
        }
        
        #endregion
        
        #region 公共 API

        /// <summary>
        /// 默认面板动画时长（秒）。
        /// </summary>
        /// <remarks>
        /// - 来自 <see cref="UIManagerConfig.DefaultAnimDuration"/>。
        /// - 该值会被 <see cref="UIPanel"/> 的默认打开/关闭动画使用（面板可自行重写动画逻辑）。
        /// </remarks>
        public float DefaultAnimDuration => _config != null ? _config.DefaultFadeDuration : 0.2f;
        
        /// <summary>
        /// 打开面板
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        /// <param name="data">传递给面板的数据</param>
        /// <returns>面板实例，可直接操作</returns>
        public T Open<T>(object data = null) where T : UIPanel
            => OpenPanelCore<T>(data, null, -1, beforeLifecycle: null, onRefresh: null);

        /// <summary>
        /// 打开面板并强制指定 UILayer（覆盖面板自身的 <see cref="UIPanel.Layer"/>）。
        /// </summary>
        /// <remarks>
        /// - 用于“同一个面板根据业务场景放到不同层”的需求，例如把某个面板临时抬到 System 层。
        /// - siblingIndex 仅在同一层容器内部生效：值越大越靠上（越后渲染）。
        /// - 不建议在 Update 高频调用；打开/关闭属于低频行为。
        /// </remarks>
        /// <typeparam name="T">面板类型</typeparam>
        /// <param name="layer">目标 UILayer</param>
        /// <param name="data">传递给面板的数据</param>
        /// <param name="siblingIndex">同层内的顺序；小于 0 则保持/恢复面板的历史顺序</param>
        public T OpenOnLayer<T>(UILayer layer, object data = null, int siblingIndex = -1) where T : UIPanel
        {
            // 目标层级容器
            if (!_layerContainers.TryGetValue(layer, out var container))
            {
                CYLog.Warning($"[UIManager] 未找到 UILayer 容器: {layer}");
                return null;
            }
            
            return OpenPanelCore<T>(data, container, siblingIndex, beforeLifecycle: null, onRefresh: null, logContext: $" (Layer={layer})");
        }

        /// <summary>
        /// 打开面板并放入自定义层（独立 Canvas.sortingOrder 控制）。
        /// </summary>
        /// <remarks>
        /// - 自定义层的本质是：UIRoot/Canvas 下新建一个子 Canvas，并通过 sortingOrder 控制大层级。
        /// - siblingIndex 仅影响该自定义层容器内部顺序。
        /// </remarks>
        /// <typeparam name="T">面板类型</typeparam>
        /// <param name="layerName">自定义层名</param>
        /// <param name="sortOrder">Canvas.sortingOrder（仅在层不存在时创建会使用该值；已存在则不会强制改）</param>
        /// <param name="data">传递给面板的数据</param>
        /// <param name="siblingIndex">同层内的顺序；小于 0 则保持/恢复面板的历史顺序</param>
        public T OpenOnCustomLayer<T>(string layerName, int sortOrder, object data = null, int siblingIndex = -1)
            where T : UIPanel
        {
            if (string.IsNullOrEmpty(layerName))
            {
                CYLog.Warning("[UIManager] OpenOnCustomLayer 失败：layerName 为空");
                return null;
            }

            // 自定义层级容器（不存在则创建）
            var container = _customLayers.TryGetValue(layerName, out var existing)
                ? existing
                : CreateLayer(layerName, sortOrder);

            return OpenPanelCore<T>(data, container, siblingIndex, beforeLifecycle: null, onRefresh: null, logContext: $" (CustomLayer={layerName})");
        }
        
        /// <summary>
        /// 打开面板（强类型用户数据，无装箱）。
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        /// <typeparam name="TData">数据类型</typeparam>
        /// <param name="data">传递给面板的数据</param>
        /// <returns>面板实例，可直接操作</returns>
        /// <remarks>面板需实现 <see cref="IUserDataReceiver{TData}"/>，在 <c>SetUserData</c> 中接收数据。</remarks>
        public T Open<T, TData>(in TData data)
            where T : UIPanel, IUserDataReceiver<TData>
        {
            // 复制一份数据，避免直接捕获 in 参数
            var payload = data;
            return OpenPanelCore<T>(
                userData: null,
                overrideContainer: null,
                siblingIndex: -1,
                beforeLifecycle: panel => panel.SetUserData(in payload),
                onRefresh: panel => panel.SetUserData(in payload),
                logContext: " (Typed)");
        }
        
        /// <summary>
        /// 指定 UILayer 打开强类型面板（无装箱）。
        /// </summary>
        public T OpenOnLayer<T, TData>(UILayer layer, in TData data, int siblingIndex = -1)
            where T : UIPanel, IUserDataReceiver<TData>
        {
            // 目标层级容器
            if (!_layerContainers.TryGetValue(layer, out var container))
            {
                CYLog.Warning($"[UIManager] 未找到 UILayer 容器: {layer}");
                return null;
            }
            
            // 复制一份数据，避免直接捕获 in 参数
            var payload = data;
            return OpenPanelCore<T>(
                userData: null,
                overrideContainer: container,
                siblingIndex: siblingIndex,
                beforeLifecycle: panel => panel.SetUserData(in payload),
                onRefresh: panel => panel.SetUserData(in payload),
                logContext: $" (Layer={layer}, Typed)");
        }
        
        /// <summary>
        /// 指定自定义层打开强类型面板（无装箱）。
        /// </summary>
        public T OpenOnCustomLayer<T, TData>(string layerName, int sortOrder, in TData data, int siblingIndex = -1)
            where T : UIPanel, IUserDataReceiver<TData>
        {
            if (string.IsNullOrEmpty(layerName))
            {
                CYLog.Warning("[UIManager] OpenOnCustomLayer 失败：layerName 为空");
                return null;
            }

            // 自定义层级容器（不存在则创建）
            var container = _customLayers.TryGetValue(layerName, out var existing)
                ? existing
                : CreateLayer(layerName, sortOrder);

            // 复制一份数据，避免直接捕获 in 参数
            var payload = data;
            return OpenPanelCore<T>(
                userData: null,
                overrideContainer: container,
                siblingIndex: siblingIndex,
                beforeLifecycle: panel => panel.SetUserData(in payload),
                onRefresh: panel => panel.SetUserData(in payload),
                logContext: $" (CustomLayer={layerName}, Typed)");
        }
        
        /// <summary>
        /// 面板打开核心流程（复用/创建、挂载层级、生命周期触发）
        /// </summary>
        private T OpenPanelCore<T>(
            object userData,
            Transform overrideContainer,
            int siblingIndex,
            Action<T> beforeLifecycle,
            Action<T> onRefresh,
            string logContext = null) where T : UIPanel
        {
            logContext ??= string.Empty;
            // 面板类型缓存
            var panelType = typeof(T);
            
            // 已存在的面板实例
            if (_openedPanels.TryGetValue(panelType, out var existingPanel))
            {
                // 强转后的面板类型
                var typedPanel = existingPanel as T;
                onRefresh?.Invoke(typedPanel);
                existingPanel.InternalRefresh(userData);
                CYLog.Debug($"[UIManager] 面板已打开，刷新: {panelType.Name}{logContext}");
                return typedPanel;
            }

            // 新建或取出面板实例
            var panel = GetOrCreatePanel<T>();
            if (panel == null)
            {
                CYLog.Error($"[UIManager] 创建面板失败: {panelType.Name}{logContext}");
                return null;
            }

            // 目标容器（若传入覆盖容器则优先）
            Transform container = overrideContainer;
            if (container == null)
            {
                var meta = GetPrefabMeta(panelType);
                if (!string.IsNullOrEmpty(meta.LayerName))
                {
                    container = GetOrCreateCustomLayer(meta.LayerName, meta.SortOrder);
                }
                else
                {
                    container = GetLayerContainer(panel.Layer);
                }
            }
            if (container != null)
            {
                panel.transform.SetParent(container, false);
                if (siblingIndex >= 0)
                {
                    // 当前层级子节点数量
                    var childCount = container.childCount;
                    panel.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, Math.Max(0, childCount - 1)));
                }
                else
                {
                    RestoreSiblingIndex(panel);
                }
            }
            else
            {
                CYLog.Warning($"[UIManager] 未找到 UILayer 容器: {panel.Layer}");
            }

            if (panel.IsStackable && _panelStack.Count > 0)
            {
                // 当前栈顶面板
                var topPanel = _panelStack.Peek();
                if (topPanel != null && topPanel != panel)
                {
                    topPanel.InternalPause();
                }
            }
            
            panel.gameObject.SetActive(true);
            beforeLifecycle?.Invoke(panel);
            panel.InternalInit(userData);
            panel.InternalOpen(userData);
            
            _openedPanels[panelType] = panel;
            if (panel.IsStackable)
            {
                _panelStack.Push(panel);
            }
            
            CYLog.Debug($"[UIManager] 打开面板: {panelType.Name}{logContext}");
            return panel;
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
        /// <param name="panelType">面板类型</param>
        public void Close(Type panelType)
        {
            // 目标面板实例
            if (!_openedPanels.TryGetValue(panelType, out var panel))
            {
                return;
            }
            
            ClosePanel(panel);
        }
        
        /// <summary>
        /// 关闭面板实例
        /// </summary>
        /// <param name="panel">面板实例</param>
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
            // 清理栈顶无效项（null / 已关闭），避免 Back 到失效面板
            CleanupStackTop();

            if (_panelStack.Count == 0)
            {
                CYLog.Debug("[UIManager] 面板栈为空，无法返回");
                return;
            }
            
            // 当前栈顶面板
            var topPanel = _panelStack.Peek();
            ClosePanel(topPanel);
        }
        
        /// <summary>
        /// 关闭所有面板
        /// </summary>
        public void CloseAll(bool isShutdown = false)
        {
            // 复制一份列表，避免遍历中修改集合
            var panelsToClose = new List<UIPanel>(_openedPanels.Values);
            
            // 逐个关闭
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
            // 需关闭的面板集合
            var panelsToClose = new List<UIPanel>();
            
            // 遍历当前已打开面板
            foreach (var panel in _openedPanels.Values)
            {
                if (panel.Layer == layer)
                {
                    panelsToClose.Add(panel);
                }
            }
            
            // 逐个关闭
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
            // 目标面板实例
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
        /// 检查面板是否已打开（按类型）
        /// </summary>
        public bool IsOpened(Type panelType)
        {
            return _openedPanels.ContainsKey(panelType);
        }

        /// <summary>
        /// 是否存在面板（IsOpened 的别名）
        /// </summary>
        public bool Has<T>() where T : UIPanel
        {
            return IsOpened<T>();
        }

        /// <summary>
        /// 是否存在面板（IsOpened 的别名，按类型）
        /// </summary>
        public bool Has(Type panelType)
        {
            return IsOpened(panelType);
        }

        /// <summary>
        /// 尝试获取已打开的面板（泛型版）
        /// </summary>
        public bool TryGet<T>(out T panel) where T : UIPanel
        {
            // 已打开的面板实例
            if (_openedPanels.TryGetValue(typeof(T), out var p))
            {
                panel = p as T;
                return panel != null;
            }

            panel = null;
            return false;
        }

        /// <summary>
        /// 尝试获取已打开的面板（按类型）
        /// </summary>
        public bool TryGet(Type panelType, out UIPanel panel)
        {
            return _openedPanels.TryGetValue(panelType, out panel);
        }

        /// <summary>
        /// 获取已打开的面板（按类型，不存在则返回 null）
        /// </summary>
        public UIPanel Get(Type panelType)
        {
            // 已打开的面板实例
            return _openedPanels.TryGetValue(panelType, out var panel) ? panel : null;
        }

        /// <summary>
        /// 打开面板：如果已打开则直接返回（不触发 Refresh）
        /// </summary>
        public T OpenIfNotOpened<T>(object data = null) where T : UIPanel
        {
            // 已打开的面板实例
            if (_openedPanels.TryGetValue(typeof(T), out var existingPanel))
            {
                return existingPanel as T;
            }

            return Open<T>(data);
        }

        /// <summary>
        /// 尝试打开面板，返回是否成功
        /// </summary>
        public bool TryOpen<T>(out T panel, object data = null) where T : UIPanel
        {
            panel = Open<T>(data);
            return panel != null;
        }

        /// <summary>
        /// 刷新面板（仅当已打开时才会执行 InternalRefresh）
        /// </summary>
        public bool Refresh<T>(object data = null) where T : UIPanel
        {
            // 已打开的面板实例
            if (_openedPanels.TryGetValue(typeof(T), out var panel))
            {
                panel.InternalRefresh(data);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 刷新面板（按类型，仅当已打开时才会执行 InternalRefresh）
        /// </summary>
        public bool Refresh(Type panelType, object data = null)
        {
            // 已打开的面板实例
            if (_openedPanels.TryGetValue(panelType, out var panel))
            {
                panel.InternalRefresh(data);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 切换面板：已打开则关闭，未打开则打开
        /// </summary>
        public bool Toggle<T>(object data = null) where T : UIPanel
        {
            if (IsOpened<T>())
            {
                Close<T>();
                return false;
            }

            return Open<T>(data) != null;
        }

        /// <summary>
        /// 获取或打开面板（等价于 Open；已打开时会走 Open 的刷新逻辑）
        /// </summary>
        public T GetOrOpen<T>(object data = null) where T : UIPanel
        {
            return Open<T>(data);
        }

        /// <summary>
        /// 若已打开则关闭，并返回是否发生了关闭
        /// </summary>
        public bool CloseIfOpened<T>() where T : UIPanel
        {
            // 已打开的面板实例
            if (!_openedPanels.TryGetValue(typeof(T), out var panel))
            {
                return false;
            }

            ClosePanel(panel);
            return true;
        }

        /// <summary>
        /// 若已打开则关闭（按类型），并返回是否发生了关闭
        /// </summary>
        public bool CloseIfOpened(Type panelType)
        {
            // 已打开的面板实例
            if (!_openedPanels.TryGetValue(panelType, out var panel))
            {
                return false;
            }

            ClosePanel(panel);
            return true;
        }

        /// <summary>
        /// 若该实例当前由 UIManager 管理并处于打开状态，则关闭它
        /// </summary>
        public bool CloseIfOpened(UIPanel panel)
        {
            if (panel == null)
            {
                return false;
            }

            // 面板类型
            var panelType = panel.GetType();
            // 已打开且被管理的面板实例
            if (!_openedPanels.TryGetValue(panelType, out var openedPanel) || openedPanel != panel)
            {
                return false;
            }

            ClosePanel(panel);
            return true;
        }

        /// <summary>
        /// 批量关闭指定类型的面板
        /// </summary>
        public void ClosePanels(params Type[] panelTypes)
        {
            if (panelTypes == null)
            {
                return;
            }

            // 遍历索引
            for (int i = 0; i < panelTypes.Length; i++)
            {
                Close(panelTypes[i]);
            }
        }

        /// <summary>
        /// 批量关闭指定层级的所有面板
        /// </summary>
        public void CloseLayers(params UILayer[] layers)
        {
            if (layers == null)
            {
                return;
            }

            // 遍历索引
            for (int i = 0; i < layers.Length; i++)
            {
                CloseLayer(layers[i]);
            }
        }

        /// <summary>
        /// 是否可以 Back（面板栈中是否存在可返回的有效面板）
        /// </summary>
        public bool CanBack
        {
            get
            {
                CleanupStackTop();
                return _panelStack.Count > 0;
            }
        }

        /// <summary>
        /// 尝试 Back，成功返回 true
        /// </summary>
        public bool TryBack()
        {
            if (!CanBack)
            {
                return false;
            }

            Back();
            return true;
        }

        /// <summary>
        /// 异步打开面板（回调版）
        /// <para>1. 若已打开：会先 Refresh，然后立刻回调</para>
        /// <para>2. 若资源已缓存/或池中有实例：会同步 Open，然后立刻回调</para>
        /// <para>3. 否则：先异步加载预制体，再 Open，最后回调</para>
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        /// <param name="onOpened">打开完成后的回调（参数为面板实例，失败为 null）</param>
        /// <param name="data">传递给面板的数据</param>
        public void OpenAsync<T>(Action<T> onOpened, object data = null) where T : UIPanel
        {
            // 面板类型
            var panelType = typeof(T);

            // 已打开的面板实例
            if (_openedPanels.TryGetValue(panelType, out var existingPanel))
            {
                existingPanel.InternalRefresh(data);
                onOpened?.Invoke(existingPanel as T);
                return;
            }

            // 是否存在可复用的池中实例
            bool hasPooledInstance = false;
            // 对应类型的对象池
            if (_config.EnablePool && _panelPool.TryGetValue(panelType, out var pool) && pool.Count > 0)
            {
                hasPooledInstance = true;
            }

            // 预制体路径
            var path = GetPrefabPath(panelType);
            if (hasPooledInstance || _prefabCache.ContainsKey(path))
            {
                // 已有资源或池中实例，直接同步打开
                // 打开的面板实例
                var panel = Open<T>(data);
                onOpened?.Invoke(panel);
                return;
            }

            _resourceLoader.LoadAsync<GameObject>(path, prefab =>
            {
                if (prefab == null)
                {
                    CYLog.Error($"[UIManager] 找不到预制体: {path}");
                    onOpened?.Invoke(null);
                    return;
                }

                _prefabCache[path] = prefab;
                // 异步加载完成后打开面板
                var panel = Open<T>(data);
                onOpened?.Invoke(panel);
            });
        }
        
        /// <summary>
        /// 预加载面板
        /// </summary>
        public void Preload<T>() where T : UIPanel
        {
            // 面板类型
            var panelType = typeof(T);
            // 预制体路径
            var path = GetPrefabPath(panelType);
            
            if (!_prefabCache.ContainsKey(path))
            {
                // 预制体缓存
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
        public void ShowToast(string message, float duration = 0f)
        {
            // 使用 UIToast 组件
            if (Components.UIToast.Instance != null)
            {
                Components.UIToast.Show(message, duration > 0f ? duration : _config.ToastDuration);
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
            // 对话框配置
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
            // 对话框配置
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
            // 已存在的层级容器
            if (_customLayers.TryGetValue(layerName, out var existing))
            {
                CYLog.Warning($"[UIManager] 层级已存在: {layerName}");
                return existing;
            }
            
            // 层级根对象
            var layerGo = new GameObject(layerName);
            layerGo.layer = LayerMask.NameToLayer("UI");
            
            // 层级 RectTransform
            var rectTransform = layerGo.AddComponent<RectTransform>();
            rectTransform.SetParent(_rootCanvas.transform, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // 设置排序顺序
            // 层级 Canvas
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
            // 批量创建配置项
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
            // 已存在的自定义层级容器
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
            // 预设层级容器
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
            // 层级名称数组
            var names = new string[_customLayers.Count];
            _customLayers.Keys.CopyTo(names, 0);
            return names;
        }
        
        #endregion
        
        #region 私有方法

        /// <summary>
        /// 获取或创建 UI 对象池根节点，将回收的面板统一挂在 [ObjectPools]/UI 下，避免与正常隐藏的面板混淆
        /// </summary>
        /// <summary>
        /// 获取或创建 UI 对象池根节点，将回收的面板统一挂在 [UIPools] 下
        /// </summary>
        private Transform GetOrCreateUIPoolRoot()
        {
            if (_uiPoolRoot != null)
            {
                return _uiPoolRoot;
            }

            // 创建独立的 UI 回收池根节点，不再依赖通用的 [ObjectPools]
            // UI 回收池根对象
            var poolGo = new GameObject("[UIPools]");
            UnityEngine.Object.DontDestroyOnLoad(poolGo);
            poolGo.SetActive(false);
            
            _uiPoolRoot = poolGo.transform;
            // 标记为由 Manager 创建，用于 Dispose 时清理
            _poolRootCreatedByManager = true; 
            
            return _uiPoolRoot;
        }

        /// <summary>
        /// 从缓存恢复面板的兄弟顺序，确保复用后层级不乱序
        /// </summary>
        private void RestoreSiblingIndex(UIPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            // 缓存的兄弟索引
            if (_panelSiblingIndex.TryGetValue(panel, out var index))
            {
                panel.transform.SetSiblingIndex(index);
                _panelSiblingIndex.Remove(panel);
            }
        }
        
        /// <summary>
        /// 创建或获取 UI 根节点
        /// </summary>
        private void CreateUIRoot()
        {
            // 先尝试查找场景中已存在的 UIRoot
            // 场景中已有的 UIRoot
            var existingRoot = GameObject.Find("UIRoot");
            if (existingRoot != null)
            {
                _uiRoot = existingRoot.transform;

                // DontDestroyOnLoad 只能作用于根节点（root GameObject）。
                // 如果 UIRoot 不是根节点（例如作为某个场景物体的子物体），直接调用会报 Unity 警告且不会生效。
                // 这里统一对其根节点执行，确保跨场景常驻。
                // UIRoot 对应的根对象
                var rootObject = existingRoot.transform.root != null ? existingRoot.transform.root.gameObject : existingRoot;
                UnityEngine.Object.DontDestroyOnLoad(rootObject);
                
                // 查找已存在的组件
                _uiCamera = existingRoot.GetComponentInChildren<Camera>();
                _rootCanvas = existingRoot.GetComponentInChildren<Canvas>();
                
                // 查找层级容器
                if (_rootCanvas != null)
                {
                    // 遍历预设层级，尝试查找/创建容器
                    foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
                    {
                        // 该层级的 Transform
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
            // UI 根对象
            var rootGo = new GameObject("UIRoot");
            UnityEngine.Object.DontDestroyOnLoad(rootGo);
            _uiRoot = rootGo.transform;
            
            // 创建 UI 相机
            // UI 相机对象
            var cameraGo = new GameObject("UICamera");
            cameraGo.transform.SetParent(_uiRoot);
            _uiCamera = cameraGo.AddComponent<Camera>();
            _uiCamera.clearFlags = CameraClearFlags.Depth;
            _uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            _uiCamera.orthographic = true;
            _uiCamera.depth = 100;
            
            // 创建根 Canvas
            // 根 Canvas 对象
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(_uiRoot);
            canvasGo.layer = LayerMask.NameToLayer("UI");
            
            _rootCanvas = canvasGo.AddComponent<Canvas>();
            _rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            _rootCanvas.worldCamera = _uiCamera;
            _rootCanvas.sortingOrder = 0;
            
            // Canvas 缩放器
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // 创建层级容器
            // 遍历预设层级，创建容器
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
            // 层级对象
            var layerGo = new GameObject(layer.ToString());
            layerGo.layer = LayerMask.NameToLayer("UI");
            
            // 层级 RectTransform
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
            // 面板类型
            var panelType = typeof(T);
            
            // 尝试从对象池获取
            // 对应类型的对象池
            if (_config.EnablePool && _panelPool.TryGetValue(panelType, out var pool) && pool.Count > 0)
            {
                return pool.Dequeue() as T;
            }
            
            // 加载预制体
            // 预制体路径
            var path = GetPrefabPath(panelType);
            // 预制体缓存
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
            // 预制体实例
            var go = UnityEngine.Object.Instantiate(prefab);
            // 面板组件
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
            // 面板类型
            var panelType = panel.GetType();

            // 只有关闭的是“当前栈顶”时，才需要在关闭后 Resume 新栈顶（返回到上一个面板）
            // 是否需要恢复新栈顶
            bool shouldResume = false;
            if (panel.IsStackable)
            {
                // 先清理栈顶的无效项，保证 Peek 是有效面板
                CleanupStackTop();
                // 判断本次关闭是否为栈顶（只有栈顶才触发 Resume）
                shouldResume = _panelStack.Count > 0 && _panelStack.Peek() == panel;
                // 将该面板从栈中移除，保持栈状态一致
                RemoveFromStack(panel);
            }
            
            // 记录关闭前的兄弟顺序，复用时可恢复到原层级位置
            if (_config.EnablePool && panel.IsPoolable)
            {
                _panelSiblingIndex[panel] = panel.transform.GetSiblingIndex();
            }
            
            // 调用关闭回调
            panel.InternalClose(isShutdown, null);
            
            // 从记录中移除
            _openedPanels.Remove(panelType);
            
            // 对象池回收或销毁
            // 如果是系统关闭 (isShutdown)，则直接销毁，不再进池，避免在退出时产生多余的挂载操作
            if (!isShutdown && _config.EnablePool && panel.IsPoolable)
            {
                panel.InternalRecycle();
                // 回收到 UI 池根节点，层级上与“隐藏”区分，便于调试
                // 回收用的父节点
                var poolParent = _uiPoolRoot != null ? _uiPoolRoot : GetOrCreateUIPoolRoot();
                panel.transform.SetParent(poolParent, false);
                panel.gameObject.SetActive(false);
                
                // 对应面板类型的对象池
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
                    _panelSiblingIndex.Remove(panel);
                    UnityEngine.Object.Destroy(panel.gameObject);
                }
            }
            else
            {
                _panelSiblingIndex.Remove(panel);
                UnityEngine.Object.Destroy(panel.gameObject);
            }
            
            // 关闭栈顶后，恢复新的栈顶面板（触发 OnResume）
            if (!isShutdown && shouldResume)
            {
                CleanupStackTop();
                if (_panelStack.Count > 0)
                {
                    // 新的栈顶面板
                    var topPanel = _panelStack.Peek();
                    if (topPanel != null && topPanel.IsOpened)
                    {
                        topPanel.InternalResume();
                    }
                }
            }
            
            CYLog.Debug($"[UIManager] 关闭面板: {panelType.Name}");
        }
        
        /// <summary>
        /// 获取 UIPrefab 元信息（路径/层级）
        /// </summary>
        private UIPrefabMeta GetPrefabMeta(Type panelType)
        {
            if (_prefabMetaCache.TryGetValue(panelType, out var cached))
            {
                return cached;
            }

            // 检查是否有 UIPrefab 特性
            var attr = Attribute.GetCustomAttribute(panelType, typeof(UIPrefabAttribute)) as UIPrefabAttribute;
            var meta = new UIPrefabMeta
            {
                Path = (attr != null && !string.IsNullOrEmpty(attr.Path))
                    ? attr.Path
                    : $"{_config.PrefabPathPrefix}{panelType.Name}",
                LayerName = attr?.LayerName ?? string.Empty,
                SortOrder = attr?.SortOrder ?? 0
            };

            _prefabMetaCache[panelType] = meta;
            return meta;
        }

        /// <summary>
        /// 获取预制体路径
        /// </summary>
        private string GetPrefabPath(Type panelType)
        {
            return GetPrefabMeta(panelType).Path;
        }

        /// <summary>
        /// 获取或创建自定义层容器
        /// </summary>
        private Transform GetOrCreateCustomLayer(string layerName, int sortOrder)
        {
            if (string.IsNullOrEmpty(layerName))
            {
                return null;
            }

            if (_customLayers.TryGetValue(layerName, out var existing))
            {
                return existing;
            }

            return CreateLayer(layerName, sortOrder);
        }

        /// <summary>
        /// 清理面板栈顶的无效项（null / 已关闭）
        /// 目的：保证 Peek/Back/Resume 操作拿到的始终是有效面板
        /// </summary>
        private void CleanupStackTop()
        {
            while (_panelStack.Count > 0)
            {
                // 当前栈顶面板
                var top = _panelStack.Peek();
                if (top == null || !top.IsOpened)
                {
                    // 栈顶已经无效，丢弃
                    _panelStack.Pop();
                    continue;
                }

                // 栈顶有效，结束清理
                break;
            }
        }

        /// <summary>
        /// 从面板栈中移除指定面板（并清理无效项），保持原有顺序不变
        /// 说明：Stack 只能 Pop/Push，所以用 _stackBuffer 做一次中转
        /// </summary>
        private void RemoveFromStack(UIPanel panel)
        {
            if (_panelStack.Count == 0)
            {
                return;
            }

            _stackBuffer.Clear();

            while (_panelStack.Count > 0)
            {
                // 取出栈顶面板
                var p = _panelStack.Pop();
                if (p == null || !p.IsOpened)
                {
                    // 丢弃无效项
                    continue;
                }

                if (p == panel)
                {
                    // 跳过目标面板（相当于移除）
                    continue;
                }

                // 暂存其它有效面板
                _stackBuffer.Add(p);
            }

            // 逆序 push 回去，恢复原来的栈顺序
            // 逆序索引
            for (int i = _stackBuffer.Count - 1; i >= 0; i--)
            {
                _panelStack.Push(_stackBuffer[i]);
            }
        }
        
        #endregion
    }
}
