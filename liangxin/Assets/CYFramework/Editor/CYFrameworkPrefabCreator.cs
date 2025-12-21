// ============================================================================
// CYFramework 2.2 - 预制体创建工具
// 功能：自动创建框架运行时所需的 GameObject 结构
// ============================================================================

using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CYFramework.Infrastructure;
using CYFramework.Core.Config;
using CYFramework.Core.UI.Components;

namespace CYFramework.Editor
{
    /// <summary>
    /// CYFramework 预制体创建工具
    /// </summary>
    public static class CYFrameworkPrefabCreator
    {
        /// <summary>
        /// 菜单根路径
        /// </summary>
        private const string MENU_ROOT = "CYFramework/";
        /// <summary>
        /// 预制体保存路径
        /// </summary>
        private const string PREFAB_PATH = "Assets/CYFramework/Resources/Prefabs/";
        
        #region 菜单项
        
        [MenuItem(MENU_ROOT + "创建配置文件", false, 50)]
        /// <summary>
        /// 创建框架配置资源
        /// </summary>
        public static void CreateConfigAsset()
        {
            // 配置资源实例
            var config = ScriptableObject.CreateInstance<CYFrameworkConfig>();
            
            // 配置资源路径
            string path = "Assets/CYFramework/Resources/CYFrameworkConfig.asset";
            
            // 确保目录存在
            if (!AssetDatabase.IsValidFolder("Assets/CYFramework/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/CYFramework", "Resources");
            }
            
            // 检查是否已存在
            if (AssetDatabase.LoadAssetAtPath<CYFrameworkConfig>(path) != null)
            {
                if (!EditorUtility.DisplayDialog("覆盖确认", 
                    "配置文件已存在，是否覆盖？", "覆盖", "取消"))
                {
                    return;
                }
            }
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // 选中创建的资源
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            
            UnityEngine.Debug.Log($"[CYFramework] 配置文件已创建: {path}");
        }
        
        [MenuItem(MENU_ROOT + "创建框架预制体/全部创建", false, 100)]
        /// <summary>
        /// 创建所有框架预制体
        /// </summary>
        public static void CreateAllPrefabs()
        {
            EnsurePrefabFolder();
            
            CreateCYFrameworkRoot();
            CreateUIRoot();
            CreateAudioService();
            CreateEntityRoot();
            CreatePoolRoot();
            CreateEventSystem();
            CreateToastPrefab();
            CreateDebugConsole();
            CreateLoadingPanel();
            CreateDialogPrefab();
            
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("[CYFramework] 所有预制体创建完成！");
        }
        
        [MenuItem(MENU_ROOT + "创建框架预制体/CYFramework 根节点", false, 110)]
        /// <summary>
        /// 创建 CYFramework 根节点预制体
        /// </summary>
        public static void CreateCYFrameworkRoot()
        {
            // 根对象
            var go = new GameObject("CYFramework");
            
            // 添加 CYBootstrap 脚本
            go.AddComponent<CYBootstrap>();
            
            // 添加配置器组件
            go.AddComponent<CYConfigurator>();
            
            SavePrefab(go, "CYFramework.prefab");
            UnityEngine.Debug.Log("[CYFramework] CYFramework 预制体已创建（包含 CYBootstrap + CYConfigurator）");
        }
        
        [MenuItem(MENU_ROOT + "创建框架预制体/UIRoot (UI根节点)", false, 120)]
        /// <summary>
        /// 创建 UIRoot 预制体
        /// </summary>
        public static void CreateUIRoot()
        {
            // UI 根节点
            var uiRoot = new GameObject("UIRoot");
            uiRoot.layer = 5; // UI层
            
            // UICamera
            // UI 相机对象
            var uiCameraGo = CreateChild(uiRoot, "UICamera");
            uiCameraGo.layer = 5; // UI层
            // UI 相机组件
            var uiCamera = uiCameraGo.AddComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            uiCamera.orthographic = true;
            uiCamera.depth = 10;
            uiCamera.nearClipPlane = 0.3f;
            uiCamera.farClipPlane = 1000f;
            
            // Canvas
            // 画布对象
            var canvasGo = CreateChild(uiRoot, "Canvas");
            canvasGo.layer = 5; // UI层
            // 画布组件
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.sortingOrder = 0;
            
            // 画布缩放组件
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasGo.AddComponent<GraphicRaycaster>();
            
            // UI 层级
            CreateUILayer(canvasGo, "Background", 0);
            CreateUILayer(canvasGo, "Main", 100);
            CreateUILayer(canvasGo, "Popup", 200);
            CreateUILayer(canvasGo, "Tips", 300);
            CreateUILayer(canvasGo, "Guide", 400);
            CreateUILayer(canvasGo, "Loading", 500);
            CreateUILayer(canvasGo, "System", 600);
            
            // Debug Updater (可选)
            // 调试更新器节点
            var debugUpdater = CreateChild(uiRoot, "[Debug Updater]");
            debugUpdater.layer = 5; // UI层
            
            SavePrefab(uiRoot, "UIRoot.prefab");
            UnityEngine.Debug.Log("[CYFramework] UIRoot 预制体已创建");
        }
        
        [MenuItem(MENU_ROOT + "创建框架预制体/AudioService (音频服务)", false, 130)]
        /// <summary>
        /// 创建音频服务预制体
        /// </summary>
        public static void CreateAudioService()
        {
            // 音频服务根对象
            var go = new GameObject("AudioService");
            
            // BGM 音源
            // BGM 节点
            var bgmGo = CreateChild(go, "BGMSource");
            // BGM 音源组件
            var bgmSource = bgmGo.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.priority = 0;
            
            // SFX 音源池
            // SFX 池根节点
            var sfxPoolGo = CreateChild(go, "SFXPool");
            for (int i = 0; i < 8; i++) // i 为索引
            {
                // SFX 子节点
                var sfxGo = CreateChild(sfxPoolGo, $"SFX_{i}");
                // SFX 音源组件
                var sfxSource = sfxGo.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                sfxSource.priority = 128;
            }
            
            SavePrefab(go, "AudioService.prefab");
            UnityEngine.Debug.Log("[CYFramework] AudioService 预制体已创建");
        }
        
        [MenuItem(MENU_ROOT + "创建框架预制体/EventSystem (事件系统)", false, 140)]
        /// <summary>
        /// 创建事件系统预制体
        /// </summary>
        public static void CreateEventSystem()
        {
            // 事件系统对象
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            
            SavePrefab(go, "EventSystem.prefab");
            UnityEngine.Debug.Log("[CYFramework] EventSystem 预制体已创建");
        }
        
        [MenuItem(MENU_ROOT + "创建框架预制体/EntityRoot (实体根节点)", false, 145)]
        /// <summary>
        /// 创建实体根节点预制体
        /// </summary>
        public static void CreateEntityRoot()
        {
            // 实体根节点
            var go = new GameObject("[Entities]");
            
            // 添加一些预设的实体分组
            CreateChild(go, "Players");
            CreateChild(go, "Enemies");
            CreateChild(go, "NPCs");
            CreateChild(go, "Props");
            CreateChild(go, "Effects");
            
            SavePrefab(go, "EntityRoot.prefab");
            UnityEngine.Debug.Log("[CYFramework] EntityRoot 预制体已创建");
        }
        
        [MenuItem(MENU_ROOT + "创建框架预制体/PoolRoot (对象池根节点)", false, 146)]
        /// <summary>
        /// 创建对象池根节点预制体
        /// </summary>
        public static void CreatePoolRoot()
        {
            // 对象池根节点
            var go = new GameObject("[ObjectPools]");
            go.SetActive(false); // 对象池根节点默认隐藏
            
            SavePrefab(go, "PoolRoot.prefab");
            UnityEngine.Debug.Log("[CYFramework] PoolRoot 预制体已创建");
        }
        
        [MenuItem(MENU_ROOT + "创建框架预制体/Toast 预制体", false, 150)]
        /// <summary>
        /// 创建 Toast 预制体
        /// </summary>
        public static void CreateToastPrefab()
        {
            // Toast Container
            // Toast 根节点
            var toastGo = new GameObject("UIToast");
            // Toast 根节点 RectTransform
            var toastRect = toastGo.AddComponent<RectTransform>(); // 先添加 RectTransform
            // Toast 脚本组件
            var uiToast = toastGo.AddComponent<UIToast>(); // 再添加 UIToast 脚本
            toastRect.anchorMin = new Vector2(0.5f, 0.5f);
            toastRect.anchorMax = new Vector2(0.5f, 0.5f);
            toastRect.pivot = new Vector2(0.5f, 0.5f);
            toastRect.sizeDelta = new Vector2(400, 300);
            
            // Toast Item Template
            // Toast 项模板节点
            var itemGo = CreateChild(toastGo, "ToastItem");
            // Toast 项 RectTransform
            var itemRect = itemGo.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0.5f, 0.5f);
            itemRect.anchorMax = new Vector2(0.5f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = new Vector2(300, 50);
            
            // Background
            // 背景节点
            var bgGo = CreateChild(itemGo, "Background");
            // 背景图片组件
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.8f);
            // 背景 RectTransform
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            // Text
            // 文本节点
            var textGo = CreateChild(itemGo, "Text");
            // 文本组件
            var text = textGo.AddComponent<Text>();
            text.text = "Toast Message";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            // 文本 RectTransform
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);
            
            // CanvasGroup for fade
            itemGo.AddComponent<CanvasGroup>();
            
            // 隐藏模板
            itemGo.SetActive(false);
            
            SavePrefab(toastGo, "UIToast.prefab");
            UnityEngine.Debug.Log("[CYFramework] UIToast 预制体已创建");
        }
        
        [MenuItem(MENU_ROOT + "创建框架预制体/DebugConsole (调试控制台)", false, 160)]
        /// <summary>
        /// 创建 DebugConsole 预制体
        /// </summary>
        public static void CreateDebugConsole()
        {
            // 创建调试控制台根节点
            // 调试控制台根对象
            var consoleGo = new GameObject("DebugConsole");
            // 根节点 RectTransform
            var consoleRect = consoleGo.AddComponent<RectTransform>();
            consoleRect.anchorMin = Vector2.zero;
            consoleRect.anchorMax = Vector2.one;
            consoleRect.offsetMin = Vector2.zero;
            consoleRect.offsetMax = Vector2.zero;
            
            // 创建背景面板
            // 面板节点
            var panelGo = CreateChild(consoleGo, "Panel");
            // 面板 RectTransform
            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0.5f);
            panelRect.anchorMax = new Vector2(1, 1);
            panelRect.offsetMin = new Vector2(10, 10);
            panelRect.offsetMax = new Vector2(-10, -10);
            // 面板图片组件
            var panelImage = panelGo.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.85f);
            
            // 创建日志显示区域
            // 滚动视图节点
            var scrollViewGo = CreateChild(panelGo, "ScrollView");
            // 滚动视图 RectTransform
            var scrollRect = scrollViewGo.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = new Vector2(1, 0.85f);
            scrollRect.offsetMin = new Vector2(5, 5);
            scrollRect.offsetMax = new Vector2(-5, -5);
            // ScrollRect 组件
            var scrollView = scrollViewGo.AddComponent<UnityEngine.UI.ScrollRect>();
            scrollView.horizontal = false;
            scrollView.vertical = true;
            
            // Viewport
            // 视口节点
            var viewportGo = CreateChild(scrollViewGo, "Viewport");
            // 视口 RectTransform
            var viewportRect = viewportGo.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportGo.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            viewportGo.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            scrollView.viewport = viewportRect;
            
            // Content
            // 内容节点
            var contentGo = CreateChild(viewportGo, "Content");
            // 内容 RectTransform
            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);
            // 垂直布局组件
            var layoutGroup = contentGo.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            // 尺寸适配组件
            var contentSizeFitter = contentGo.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            contentSizeFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scrollView.content = contentRect;
            
            // 创建输入区域
            // 输入区域节点
            var inputAreaGo = CreateChild(panelGo, "InputArea");
            // 输入区域 RectTransform
            var inputAreaRect = inputAreaGo.AddComponent<RectTransform>();
            inputAreaRect.anchorMin = new Vector2(0, 0.85f);
            inputAreaRect.anchorMax = Vector2.one;
            inputAreaRect.offsetMin = new Vector2(5, 5);
            inputAreaRect.offsetMax = new Vector2(-5, -5);
            
            // 输入框
            // 输入框节点
            var inputFieldGo = CreateChild(inputAreaGo, "InputField");
            // 输入框 RectTransform
            var inputFieldRect = inputFieldGo.AddComponent<RectTransform>();
            inputFieldRect.anchorMin = Vector2.zero;
            inputFieldRect.anchorMax = new Vector2(0.85f, 1);
            inputFieldRect.offsetMin = Vector2.zero;
            inputFieldRect.offsetMax = new Vector2(-5, 0);
            // 输入框背景图片
            var inputFieldImage = inputFieldGo.AddComponent<Image>();
            inputFieldImage.color = new Color(0.2f, 0.2f, 0.2f, 1);
            // InputField 组件
            var inputField = inputFieldGo.AddComponent<UnityEngine.UI.InputField>();
            
            // 输入框文字
            // 输入框文字节点
            var inputTextGo = CreateChild(inputFieldGo, "Text");
            // 输入框文字 RectTransform
            var inputTextRect = inputTextGo.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = new Vector2(5, 0);
            inputTextRect.offsetMax = new Vector2(-5, 0);
            // 输入框文字组件
            var inputText = inputTextGo.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            inputText.fontSize = 16;
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleLeft;
            inputField.textComponent = inputText;
            
            // 执行按钮
            // 执行按钮节点
            var buttonGo = CreateChild(inputAreaGo, "ExecuteButton");
            // 按钮 RectTransform
            var buttonRect = buttonGo.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.85f, 0);
            buttonRect.anchorMax = Vector2.one;
            buttonRect.offsetMin = new Vector2(5, 0);
            buttonRect.offsetMax = Vector2.zero;
            // 按钮图片
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 0.2f, 1);
            buttonGo.AddComponent<UnityEngine.UI.Button>();
            
            // 按钮文字节点
            var buttonTextGo = CreateChild(buttonGo, "Text");
            // 按钮文字 RectTransform
            var buttonTextRect = buttonTextGo.AddComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;
            // 按钮文字组件
            var buttonText = buttonTextGo.AddComponent<Text>();
            buttonText.text = "执行";
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 16;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
            
            // 默认隐藏
            consoleGo.SetActive(false);
            
            SavePrefab(consoleGo, "DebugConsole.prefab");
            UnityEngine.Debug.Log("[CYFramework] DebugConsole 预制体已创建");
        }
        
        [MenuItem(MENU_ROOT + "创建框架预制体/Loading 界面", false, 170)]
        /// <summary>
        /// 创建 Loading 界面预制体
        /// </summary>
        public static void CreateLoadingPanel()
        {
            // Loading 根节点
            var loadingGo = new GameObject("UILoading");
            // 根节点 RectTransform
            var loadingRect = loadingGo.AddComponent<RectTransform>();
            loadingRect.anchorMin = Vector2.zero;
            loadingRect.anchorMax = Vector2.one;
            loadingRect.offsetMin = Vector2.zero;
            loadingRect.offsetMax = Vector2.zero;
            
            // 背景
            // 背景节点
            var bgGo = CreateChild(loadingGo, "Background");
            // 背景 RectTransform
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            // 背景图片
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1);
            
            // 进度条背景
            // 进度条背景节点
            var progressBgGo = CreateChild(loadingGo, "ProgressBackground");
            // 进度条背景 RectTransform
            var progressBgRect = progressBgGo.AddComponent<RectTransform>();
            progressBgRect.anchorMin = new Vector2(0.2f, 0.45f);
            progressBgRect.anchorMax = new Vector2(0.8f, 0.55f);
            progressBgRect.sizeDelta = Vector2.zero;
            // 进度条背景图片
            var progressBgImage = progressBgGo.AddComponent<Image>();
            progressBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1);
            
            // 进度条填充
            // 进度条填充节点
            var progressFillGo = CreateChild(progressBgGo, "Fill");
            // 进度条填充 RectTransform
            var progressFillRect = progressFillGo.AddComponent<RectTransform>();
            progressFillRect.anchorMin = Vector2.zero;
            progressFillRect.anchorMax = new Vector2(0.5f, 1); // 50% 示例
            progressFillRect.sizeDelta = Vector2.zero;
            // 进度条填充图片
            var progressFillImage = progressFillGo.AddComponent<Image>();
            progressFillImage.color = new Color(0.2f, 0.7f, 0.3f, 1);
            
            // 进度文字
            // 进度文字节点
            var progressTextGo = CreateChild(loadingGo, "ProgressText");
            // 进度文字 RectTransform
            var progressTextRect = progressTextGo.AddComponent<RectTransform>();
            progressTextRect.anchorMin = new Vector2(0.5f, 0.35f);
            progressTextRect.anchorMax = new Vector2(0.5f, 0.45f);
            progressTextRect.sizeDelta = new Vector2(200, 30);
            // 进度文字组件
            var progressText = progressTextGo.AddComponent<Text>();
            progressText.text = "加载中... 50%";
            progressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            progressText.fontSize = 18;
            progressText.color = Color.white;
            progressText.alignment = TextAnchor.MiddleCenter;
            
            // 提示文字
            // 提示文字节点
            var tipsTextGo = CreateChild(loadingGo, "TipsText");
            // 提示文字 RectTransform
            var tipsTextRect = tipsTextGo.AddComponent<RectTransform>();
            tipsTextRect.anchorMin = new Vector2(0.5f, 0.2f);
            tipsTextRect.anchorMax = new Vector2(0.5f, 0.3f);
            tipsTextRect.sizeDelta = new Vector2(400, 40);
            // 提示文字组件
            var tipsText = tipsTextGo.AddComponent<Text>();
            tipsText.text = "Tips: 这里显示加载提示";
            tipsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tipsText.fontSize = 14;
            tipsText.color = new Color(0.7f, 0.7f, 0.7f, 1);
            tipsText.alignment = TextAnchor.MiddleCenter;
            
            SavePrefab(loadingGo, "UILoading.prefab");
            UnityEngine.Debug.Log("[CYFramework] UILoading 预制体已创建");
        }
        
        [MenuItem(MENU_ROOT + "创建框架预制体/Dialog 对话框", false, 175)]
        /// <summary>
        /// 创建 Dialog 预制体
        /// </summary>
        public static void CreateDialogPrefab()
        {
            // Dialog 根节点
            var dialogGo = new GameObject("UIDialog");
            // 根节点 RectTransform
            var dialogRect = dialogGo.AddComponent<RectTransform>();
            dialogRect.anchorMin = Vector2.zero;
            dialogRect.anchorMax = Vector2.one;
            dialogRect.offsetMin = Vector2.zero;
            dialogRect.offsetMax = Vector2.zero;
            
            // 遮罩
            // 遮罩节点
            var maskGo = CreateChild(dialogGo, "Mask");
            // 遮罩 RectTransform
            var maskRect = maskGo.AddComponent<RectTransform>();
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.sizeDelta = Vector2.zero;
            // 遮罩图片
            var maskImage = maskGo.AddComponent<Image>();
            maskImage.color = new Color(0, 0, 0, 0.6f);
            
            // 面板
            // 面板节点
            var panelGo = CreateChild(dialogGo, "Panel");
            // 面板 RectTransform
            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(400, 250);
            // 面板图片
            var panelImage = panelGo.AddComponent<Image>();
            panelImage.color = new Color(0.15f, 0.15f, 0.15f, 1);
            
            // 标题
            // 标题节点
            var titleGo = CreateChild(panelGo, "Title");
            // 标题 RectTransform
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.8f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(20, 0);
            titleRect.offsetMax = new Vector2(-20, -10);
            // 标题文字组件
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "提示";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleCenter;
            
            // 内容
            // 内容节点
            var contentGo = CreateChild(panelGo, "Content");
            // 内容 RectTransform
            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0.3f);
            contentRect.anchorMax = new Vector2(1, 0.8f);
            contentRect.offsetMin = new Vector2(20, 0);
            contentRect.offsetMax = new Vector2(-20, 0);
            // 内容文字组件
            var contentText = contentGo.AddComponent<Text>();
            contentText.text = "这是对话框内容";
            contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            contentText.fontSize = 18;
            contentText.color = Color.white;
            contentText.alignment = TextAnchor.MiddleCenter;
            
            // 按钮区域
            // 按钮区域节点
            var buttonsGo = CreateChild(panelGo, "Buttons");
            // 按钮区域 RectTransform
            var buttonsRect = buttonsGo.AddComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0, 0);
            buttonsRect.anchorMax = new Vector2(1, 0.3f);
            buttonsRect.offsetMin = new Vector2(20, 10);
            buttonsRect.offsetMax = new Vector2(-20, -10);
            // 按钮布局组件
            var buttonsLayout = buttonsGo.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            buttonsLayout.spacing = 20;
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonsLayout.childControlWidth = false;
            buttonsLayout.childControlHeight = false;
            
            // 确认按钮
            CreateDialogButton(buttonsGo, "ConfirmButton", "确认", new Color(0.2f, 0.6f, 0.2f, 1));
            // 取消按钮
            CreateDialogButton(buttonsGo, "CancelButton", "取消", new Color(0.5f, 0.5f, 0.5f, 1));
            
            SavePrefab(dialogGo, "UIDialog.prefab");
            UnityEngine.Debug.Log("[CYFramework] UIDialog 预制体已创建");
        }
        
        /// <summary>
        /// 创建对话框按钮
        /// </summary>
        private static void CreateDialogButton(GameObject parent, string name, string text, Color color)
        {
            // 按钮节点
            var buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(parent.transform, false);
            // 按钮 RectTransform
            var buttonRect = buttonGo.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(120, 40);
            // 按钮图片
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = color;
            buttonGo.AddComponent<UnityEngine.UI.Button>();
            
            // 按钮文字节点
            var textGo = CreateChild(buttonGo, "Text");
            // 按钮文字 RectTransform
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            // 按钮文字组件
            var buttonText = textGo.AddComponent<Text>();
            buttonText.text = text;
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 18;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
        }
        
        #endregion
        
        #region 场景创建
        
        [MenuItem(MENU_ROOT + "在场景中创建框架结构", false, 200)]
        /// <summary>
        /// 在当前场景中创建框架结构
        /// </summary>
        public static void CreateInScene()
        {
            // CYFramework Root
            // 框架根节点
            var cyRoot = new GameObject("CYFramework");
            cyRoot.AddComponent<CYBootstrap>(); // 添加启动脚本
            cyRoot.AddComponent<CYConfigurator>(); // 添加配置器
            Undo.RegisterCreatedObjectUndo(cyRoot, "Create CYFramework");
            
            // AudioService
            // 音频服务根节点
            var audioService = new GameObject("AudioService");
            audioService.transform.SetParent(cyRoot.transform);
            
            // BGM 节点
            var bgmGo = CreateChild(audioService, "BGMSource");
            // BGM 音源组件
            var bgmSource = bgmGo.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            
            // SFX 池根节点
            var sfxPoolGo = CreateChild(audioService, "SFXPool");
            for (int i = 0; i < 4; i++) // i 为索引
            {
                // SFX 子节点
                var sfxGo = CreateChild(sfxPoolGo, $"SFX_{i}");
                // SFX 音源组件
                var sfxSource = sfxGo.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }
            
            // UIRoot
            CreateUIRootInScene(cyRoot.transform);
            
            // [Entities] - 实体根节点
            // 实体根节点
            var entitiesRoot = new GameObject("[Entities]");
            entitiesRoot.transform.SetParent(cyRoot.transform);
            CreateChild(entitiesRoot, "Players");
            CreateChild(entitiesRoot, "Enemies");
            CreateChild(entitiesRoot, "NPCs");
            CreateChild(entitiesRoot, "Props");
            CreateChild(entitiesRoot, "Effects");
            
            // [ObjectPools] - 对象池根节点
            // 对象池根节点
            var poolsRoot = new GameObject("[ObjectPools]");
            poolsRoot.transform.SetParent(cyRoot.transform);
            poolsRoot.SetActive(false);
            
            // [Managers] - 纯代码管理器容器（可选，用于组织）
            // 管理器容器
            var managersRoot = new GameObject("[Managers]");
            managersRoot.transform.SetParent(cyRoot.transform);
            
            // EventSystem (如果场景中没有)
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                // 事件系统节点
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }
            
            Selection.activeGameObject = cyRoot;
            UnityEngine.Debug.Log("[CYFramework] 框架结构已在场景中创建！包含: AudioService, UIRoot, [Entities], [ObjectPools], [Managers]");
        }
        
        /// <summary>
        /// 在场景中创建 UIRoot
        /// </summary>
        private static void CreateUIRootInScene(Transform parent)
        {
            // UI 根节点
            var uiRoot = new GameObject("UIRoot");
            uiRoot.layer = 5; // UI层
            uiRoot.transform.SetParent(parent);
            
            // UICamera
            // UI 相机节点
            var uiCameraGo = CreateChild(uiRoot, "UICamera");
            uiCameraGo.layer = 5; // UI层
            // UI 相机组件
            var uiCamera = uiCameraGo.AddComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.cullingMask = 1 << 5; // UI layer
            uiCamera.orthographic = true;
            uiCamera.depth = 10;
            
            // Canvas
            // 画布节点
            var canvasGo = CreateChild(uiRoot, "Canvas");
            canvasGo.layer = 5; // UI层
            // 画布组件
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            
            // 画布缩放组件
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasGo.AddComponent<GraphicRaycaster>();
            
            // UI Layers
            CreateUILayer(canvasGo, "Background", 0);
            CreateUILayer(canvasGo, "Main", 100);
            CreateUILayer(canvasGo, "Popup", 200);
            CreateUILayer(canvasGo, "Tips", 300);
            CreateUILayer(canvasGo, "Guide", 400);
            CreateUILayer(canvasGo, "Loading", 500);
            CreateUILayer(canvasGo, "System", 600);
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 确保预制体保存目录存在
        /// </summary>
        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/CYFramework/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/CYFramework", "Resources");
            }
            if (!AssetDatabase.IsValidFolder("Assets/CYFramework/Resources/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/CYFramework/Resources", "Prefabs");
            }
        }
        
        /// <summary>
        /// 创建子节点
        /// </summary>
        private static GameObject CreateChild(GameObject parent, string name)
        {
            // 子对象
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
        
        /// <summary>
        /// 创建 UI 层级节点
        /// </summary>
        private static void CreateUILayer(GameObject canvas, string name, int sortingOrder)
        {
            // 层级节点
            var layer = new GameObject(name);
            layer.layer = 5; // UI层
            layer.transform.SetParent(canvas.transform, false);
            
            // RectTransform 组件
            var rect = layer.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            // 添加 Canvas 用于 sorting order
            // 层级 Canvas
            var layerCanvas = layer.AddComponent<Canvas>();
            layerCanvas.overrideSorting = true;
            layerCanvas.sortingOrder = sortingOrder;
            
            layer.AddComponent<GraphicRaycaster>();
        }
        
        /// <summary>
        /// 保存预制体
        /// </summary>
        private static void SavePrefab(GameObject go, string fileName)
        {
            EnsurePrefabFolder();
            
            // 预制体路径
            string path = PREFAB_PATH + fileName;
            
            // 如果已存在，询问是否覆盖
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                if (!EditorUtility.DisplayDialog("覆盖确认", 
                    $"预制体 {fileName} 已存在，是否覆盖？", "覆盖", "取消"))
                {
                    Object.DestroyImmediate(go);
                    return;
                }
            }
            
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
        
        #endregion
    }
}
