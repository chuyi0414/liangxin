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
        [SerializeField] private bool _showOnStart = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
        [SerializeField] private int _targetFPS = 60;

        // 配置开关（来自 CYFrameworkConfig.Debug）
        private bool _showFPS = true;
        private bool _showMemory = true;
        private bool _enableProfiler = true;
        
        // 是否显示
        private bool _isVisible;
        
        // FPS 计算
        private float _fps;
        private float _frameTime;
        private float _fpsUpdateTimer;
        private int _frameCount;
        private readonly float[] _fpsHistory = new float[60];
        private int _fpsHistoryIndex;
        
        // 内存
        private long _monoUsed;
        private long _monoTotal;
        private long _nativeUsed;
        private long _textureMemory;
        
        // 渲染
        private int _drawCalls;
        private int _batches;
        private int _triangles;
        
        // 对象池
        private PoolManager _poolManager;
        
        // 网络
        private NetworkService _networkService;
        
        // UI
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private readonly StringBuilder _sb = new(512);
        
        private void Awake()
        {
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            // Release 版本隐藏
            gameObject.SetActive(false);
            return;
#endif

            // 从 CYConfigurator 读取 Debug 配置
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
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
        
        private void Start()
        {
            Application.targetFrameRate = _targetFPS;
            
            // 获取服务
            ServiceLocator.TryGet<PoolManager>(out _poolManager);
            ServiceLocator.TryGet<NetworkService>(out _networkService);
        }
        
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
        
        private void OnGUI()
        {
            if (!_isVisible) return;
            
            InitStyles();
            
            // 主面板
            float panelWidth = 280;
            float panelHeight = 320;
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
        
        private void UpdateFPS()
        {
            _frameCount++;
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
        
        private void UpdateMemory()
        {
            _monoUsed = Profiler.GetMonoUsedSizeLong();
            _monoTotal = Profiler.GetMonoHeapSizeLong();
            _nativeUsed = Profiler.GetTotalAllocatedMemoryLong();
            
            // 纹理内存估算
            _textureMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();
        }
        
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
        
        private void DrawPoolSection()
        {
            if (_poolManager == null) return;
            
            GUILayout.Space(5);
            GUILayout.Label("🏊 对象池", _labelStyle);
            GUILayout.Label("  (详情见 PoolManager)", _labelStyle);
        }
        
        private void DrawNetworkSection()
        {
            if (_networkService == null) return;
            
            GUILayout.Space(5);
            GUILayout.Label("🌐 网络", _labelStyle);
            
            string stateText = _networkService.IsConnected ? "<color=#00FF00>已连接</color>" : "<color=#FF0000>断开</color>";
            GUILayout.Label($"  状态: {stateText}", _labelStyle);
        }
        
        private void DrawFPSGraph(Rect rect)
        {
            GUI.Box(rect, "", _boxStyle);
            
            float graphWidth = rect.width - 10;
            float graphHeight = rect.height - 10;
            float barWidth = graphWidth / _fpsHistory.Length;
            
            for (int i = 0; i < _fpsHistory.Length; i++)
            {
                int idx = (_fpsHistoryIndex + i) % _fpsHistory.Length;
                float fps = _fpsHistory[idx];
                float normalizedFps = Mathf.Clamp01(fps / _targetFPS);
                
                Color barColor = fps >= 55 ? Color.green : fps >= 30 ? Color.yellow : Color.red;
                
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
        
        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
            return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
        }
        
        private static readonly Dictionary<Color, Texture2D> _textureCache = new();
        
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            if (_textureCache.TryGetValue(color, out var cached))
            {
                return cached;
            }
            
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            
            _textureCache[color] = texture;
            return texture;
        }
        
        #endregion
    }
}
