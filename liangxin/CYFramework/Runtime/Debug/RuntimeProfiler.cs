// ============================================================================
// CYFramework 2.2 - 运行时 Profiler 面板
// 文档位置：8.1 运行时 Profiler 面板
// 功能：FPS/帧时间、内存占用、DrawCall、对象池状态、网络状态
// ============================================================================

using System.Collections.Generic;
using System.Text;
using CYFramework.Core.Config;
using CYFramework.Core.Network;
using CYFramework.Core.Pool;
using CYFramework.Infrastructure;
using UnityEngine;
using UnityEngine.Profiling;

namespace CYFramework.Debug
{
    /// <summary>
    /// 运行时 Profiler 面板
    /// Development Build 可见
    /// </summary>
    public class RuntimeProfiler : MonoBehaviour
    {
        [Header("显示设置")]
        /// <summary>
        /// 启动时是否显示
        /// </summary>
        [SerializeField] private bool _showOnStart = true;
        /// <summary>
        /// 显示切换按键
        /// </summary>
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
        /// <summary>
        /// 目标帧率
        /// </summary>
        [SerializeField] private int _targetFPS = 60;

        // 配置开关（来自 CYFrameworkConfig.Debug）
        /// <summary>
        /// 是否显示 FPS
        /// </summary>
        private bool _showFPS = true;
        /// <summary>
        /// 是否显示内存信息
        /// </summary>
        private bool _showMemory = true;
        /// <summary>
        /// 是否启用面板
        /// </summary>
        private bool _enableProfiler = true;
        
        // 是否显示
        /// <summary>
        /// 当前是否可见
        /// </summary>
        private bool _isVisible;
        
        // FPS 计算
        /// <summary>
        /// 当前 FPS
        /// </summary>
        private float _fps;
        /// <summary>
        /// 当前帧耗时（ms）
        /// </summary>
        private float _frameTime;
        /// <summary>
        /// FPS 更新计时
        /// </summary>
        private float _fpsUpdateTimer;
        /// <summary>
        /// 帧计数
        /// </summary>
        private int _frameCount;
        /// <summary>
        /// FPS 历史曲线
        /// </summary>
        private readonly float[] _fpsHistory = new float[60];
        /// <summary>
        /// FPS 历史索引
        /// </summary>
        private int _fpsHistoryIndex;
        
        // 内存
        /// <summary>
        /// Mono 已用内存
        /// </summary>
        private long _monoUsed;
        /// <summary>
        /// Mono 总内存
        /// </summary>
        private long _monoTotal;
        /// <summary>
        /// 原生已用内存
        /// </summary>
        private long _nativeUsed;
        /// <summary>
        /// 纹理内存
        /// </summary>
        private long _textureMemory;
        
        // 渲染
        /// <summary>
        /// DrawCall 数量
        /// </summary>
        private int _drawCalls;
        /// <summary>
        /// Batch 数量
        /// </summary>
        private int _batches;
        /// <summary>
        /// 三角形数量
        /// </summary>
        private int _triangles;
        
        // 对象池
        /// <summary>
        /// 对象池管理器
        /// </summary>
        private PoolManager _poolManager;
        
        // 网络
        /// <summary>
        /// 网络服务
        /// </summary>
        private NetworkService _networkService;
        
        // UI
        /// <summary>
        /// 面板样式
        /// </summary>
        private GUIStyle _boxStyle;
        /// <summary>
        /// 文本样式
        /// </summary>
        private GUIStyle _labelStyle;
        /// <summary>
        /// 标题样式
        /// </summary>
        private GUIStyle _headerStyle;
        /// <summary>
        /// 文本拼接缓冲
        /// </summary>
        private readonly StringBuilder _sb = new(512);
        
        /// <summary>
        /// Unity Awake
        /// </summary>
        private void Awake()
        {
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            // Release 版本隐藏
            gameObject.SetActive(false);
            return;
#endif

            // 从 CYConfigurator 读取 Debug 配置
            // 配置中心
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                // 调试配置
                var config = configurator.GetConfig<DebugToolsConfig>();
                if (config != null)
                {
                    _showFPS = config.ShowFPS;
                    _showMemory = config.ShowMemory;
                    _enableProfiler = _showFPS || _showMemory; // 至少显示一项才有意义
                }
            }

            if (!_enableProfiler)
            {
                gameObject.SetActive(false);
                return;
            }
            
            _isVisible = _showOnStart;
        }
        
        /// <summary>
        /// Unity Start
        /// </summary>
        private void Start()
        {
            Application.targetFrameRate = _targetFPS;
            
            // 获取服务
            ServiceLocator.TryGet<PoolManager>(out _poolManager);
            ServiceLocator.TryGet<NetworkService>(out _networkService);
        }
        
        /// <summary>
        /// Unity Update
        /// </summary>
        private void Update()
        {
            // 切换显示
            if (Input.GetKeyDown(_toggleKey))
            {
                _isVisible = !_isVisible;
            }
            
            if (!_isVisible) return;
            
            // 更新 FPS
            UpdateFPS();
            
            // 更新内存（每秒一次）
            _fpsUpdateTimer += Time.unscaledDeltaTime;
            if (_fpsUpdateTimer >= 1f)
            {
                _fpsUpdateTimer = 0;
                UpdateMemory();
                UpdateRendering();
            }
        }
        
        /// <summary>
        /// Unity OnGUI
        /// </summary>
        private void OnGUI()
        {
            if (!_isVisible) return;
            
            InitStyles();
            
            // 主面板
            // 面板宽度
            float panelWidth = 280;
            // 面板高度
            float panelHeight = 320;
            // 面板矩形
            Rect panelRect = new Rect(10, 10, panelWidth, panelHeight);
            
            GUI.Box(panelRect, "", _boxStyle);
            
            GUILayout.BeginArea(new Rect(panelRect.x + 10, panelRect.y + 10, panelRect.width - 20, panelRect.height - 20));
            
            // 标题
            GUILayout.Label("📊 CYFramework Profiler", _headerStyle);
            GUILayout.Space(5);
            
            // FPS
            if (_showFPS)
            {
                DrawFPSSection();
            }
            
            // 内存
            if (_showMemory)
            {
                DrawMemorySection();
            }
            
            // 渲染
            DrawRenderingSection();
            
            // 对象池
            DrawPoolSection();
            
            // 网络
            DrawNetworkSection();
            
            GUILayout.EndArea();
            
            // 绘制 FPS 曲线
            if (_showFPS)
            {
                DrawFPSGraph(new Rect(panelRect.x, panelRect.yMax + 5, panelWidth, 50));
            }
        }
        
        #region 更新数据
        
        /// <summary>
        /// 更新 FPS 统计
        /// </summary>
        private void UpdateFPS()
        {
            _frameCount++;
            // 帧间隔
            float elapsed = Time.unscaledDeltaTime;
            _frameTime = elapsed * 1000f;
            
            if (_frameCount >= 10)
            {
                _fps = _frameCount / Time.unscaledTime;
                _frameCount = 0;
                
                // 记录历史
                _fpsHistory[_fpsHistoryIndex] = _fps;
                _fpsHistoryIndex = (_fpsHistoryIndex + 1) % _fpsHistory.Length;
            }
            
            _fps = 1f / Mathf.Max(elapsed, 0.001f);
        }
        
        /// <summary>
        /// 更新内存统计
        /// </summary>
        private void UpdateMemory()
        {
            _monoUsed = Profiler.GetMonoUsedSizeLong();
            _monoTotal = Profiler.GetMonoHeapSizeLong();
            _nativeUsed = Profiler.GetTotalAllocatedMemoryLong();
            
            // 纹理内存估算
            _textureMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();
        }
        
        /// <summary>
        /// 更新渲染统计
        /// </summary>
        private void UpdateRendering()
        {
#if UNITY_EDITOR
            // 编辑器下可以获取更多信息
            _drawCalls = UnityEditor.UnityStats.drawCalls;
            _batches = UnityEditor.UnityStats.batches;
            _triangles = UnityEditor.UnityStats.triangles;
#else
            // Runtime 下信息有限
            _drawCalls = 0;
            _batches = 0;
            _triangles = 0;
#endif
        }
        
        #endregion
        
        #region 绘制 UI
        
        /// <summary>
        /// 初始化 UI 样式
        /// </summary>
        private void InitStyles()
        {
            if (_boxStyle != null) return;
            
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTexture(2, 2, new Color(0, 0, 0, 0.8f));
            
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 12;
            _labelStyle.normal.textColor = Color.white;
            
            _headerStyle = new GUIStyle(_labelStyle);
            _headerStyle.fontSize = 14;
            _headerStyle.fontStyle = FontStyle.Bold;
        }
        
        /// <summary>
        /// 绘制 FPS 区域
        /// </summary>
        private void DrawFPSSection()
        {
            // FPS 颜色
            Color fpsColor = _fps >= 55 ? Color.green : _fps >= 30 ? Color.yellow : Color.red;
            
            _sb.Clear();
            _sb.Append("<color=#");
            _sb.Append(ColorUtility.ToHtmlStringRGB(fpsColor));
            _sb.Append(">FPS: ");
            _sb.Append(_fps.ToString("F1"));
            _sb.Append("</color> | Frame: ");
            _sb.Append(_frameTime.ToString("F2"));
            _sb.Append("ms");
            
            GUILayout.Label(_sb.ToString(), _labelStyle);
        }
        
        /// <summary>
        /// 绘制内存区域
        /// </summary>
        private void DrawMemorySection()
        {
            GUILayout.Space(5);
            GUILayout.Label("📦 内存", _labelStyle);
            
            _sb.Clear();
            _sb.Append("  Mono: ");
            _sb.Append(FormatBytes(_monoUsed));
            _sb.Append(" / ");
            _sb.Append(FormatBytes(_monoTotal));
            GUILayout.Label(_sb.ToString(), _labelStyle);
            
            _sb.Clear();
            _sb.Append("  Native: ");
            _sb.Append(FormatBytes(_nativeUsed));
            GUILayout.Label(_sb.ToString(), _labelStyle);
            
            _sb.Clear();
            _sb.Append("  Graphics: ");
            _sb.Append(FormatBytes(_textureMemory));
            GUILayout.Label(_sb.ToString(), _labelStyle);
        }
        
        /// <summary>
        /// 绘制渲染区域
        /// </summary>
        private void DrawRenderingSection()
        {
            GUILayout.Space(5);
            GUILayout.Label("🎨 渲染", _labelStyle);
            
            _sb.Clear();
            _sb.Append("  DrawCall: ");
            _sb.Append(_drawCalls);
            _sb.Append(" | Batches: ");
            _sb.Append(_batches);
            GUILayout.Label(_sb.ToString(), _labelStyle);
        }
        
        /// <summary>
        /// 绘制对象池区域
        /// </summary>
        private void DrawPoolSection()
        {
            if (_poolManager == null) return;
            
            GUILayout.Space(5);
            GUILayout.Label("🏊 对象池", _labelStyle);
            GUILayout.Label("  (详情见 PoolManager)", _labelStyle);
        }
        
        /// <summary>
        /// 绘制网络区域
        /// </summary>
        private void DrawNetworkSection()
        {
            if (_networkService == null) return;
            
            GUILayout.Space(5);
            GUILayout.Label("🌐 网络", _labelStyle);
            
            // 状态文本
            string stateText = _networkService.IsConnected ? "<color=#00FF00>已连接</color>" : "<color=#FF0000>断开</color>";
            GUILayout.Label($"  状态: {stateText}", _labelStyle);
        }
        
        /// <summary>
        /// 绘制 FPS 曲线图
        /// </summary>
        private void DrawFPSGraph(Rect rect)
        {
            GUI.Box(rect, "", _boxStyle);
            
            // 曲线宽度
            float graphWidth = rect.width - 10;
            // 曲线高度
            float graphHeight = rect.height - 10;
            // 柱状条宽度
            float barWidth = graphWidth / _fpsHistory.Length;
            
            // i 为索引
            for (int i = 0; i < _fpsHistory.Length; i++)
            {
                // 历史索引
                int idx = (_fpsHistoryIndex + i) % _fpsHistory.Length;
                // 当前 FPS
                float fps = _fpsHistory[idx];
                // 归一化 FPS
                float normalizedFps = Mathf.Clamp01(fps / _targetFPS);
                
                // 柱状颜色
                Color barColor = fps >= 55 ? Color.green : fps >= 30 ? Color.yellow : Color.red;
                
                // 柱状矩形
                Rect barRect = new Rect(
                    rect.x + 5 + i * barWidth,
                    rect.y + 5 + graphHeight * (1 - normalizedFps),
                    barWidth - 1,
                    graphHeight * normalizedFps
                );
                
                GUI.DrawTexture(barRect, MakeTexture(1, 1, barColor));
            }
        }
        
        #endregion
        
        #region 工具方法
        
        /// <summary>
        /// 格式化字节数
        /// </summary>
        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
            return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
        }
        
        /// <summary>
        /// 纹理缓存
        /// </summary>
        private static readonly Dictionary<Color, Texture2D> _textureCache = new();
        
        /// <summary>
        /// 生成纯色纹理（带缓存）
        /// </summary>
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            // 缓存纹理
            if (_textureCache.TryGetValue(color, out var cached))
            {
                return cached;
            }
            
            // 像素数组
            Color[] pixels = new Color[width * height];
            // i 为索引
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            // 新建纹理
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            
            _textureCache[color] = texture;
            return texture;
        }
        
        #endregion
    }
}
