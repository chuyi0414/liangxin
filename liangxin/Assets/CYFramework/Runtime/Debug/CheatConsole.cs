// ============================================================================
// CYFramework 2.2 - 命令控制台 (Cheat Console)
// 文档位置：8.2 命令控制台 (Cheat Console)
// 功能：加金币/道具、跳关、切换服务器、强制触发事件
// ============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using CYFramework.Core.Config;
using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Debug
{
    /// <summary>
    /// 控制台命令特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ConsoleCommandAttribute : Attribute
    {
        /// <summary>
        /// 命令名称
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// 命令描述
        /// </summary>
        public string Description { get; }
        /// <summary>
        /// 命令用法
        /// </summary>
        public string Usage { get; }
        
        /// <summary>
        /// 构造控制台命令特性
        /// </summary>
        public ConsoleCommandAttribute(string name, string description = "", string usage = "")
        {
            Name = name;
            Description = description;
            Usage = usage;
        }
    }
    
    /// <summary>
    /// 命令信息
    /// </summary>
    public class CommandInfo
    {
        /// <summary>
        /// 命令名称
        /// </summary>
        public string Name;
        /// <summary>
        /// 命令描述
        /// </summary>
        public string Description;
        /// <summary>
        /// 命令用法
        /// </summary>
        public string Usage;
        /// <summary>
        /// 命令方法
        /// </summary>
        public MethodInfo Method;
        /// <summary>
        /// 命令目标对象
        /// </summary>
        public object Target;
    }
    
    /// <summary>
    /// 命令控制台
    /// 文档：开发环境下通过特定手势/按键呼出
    /// </summary>
    public class CheatConsole : MonoBehaviour
    {
        [Header("设置")]
        /// <summary>
        /// 控制台开关键
        /// </summary>
        [SerializeField] private KeyCode _toggleKey = KeyCode.BackQuote; // ~ 键
        /// <summary>
        /// 最大日志行数
        /// </summary>
        [SerializeField] private int _maxLogLines = 100;
        /// <summary>
        /// 最大历史命令数
        /// </summary>
        [SerializeField] private int _maxHistorySize = 50;

        // 配置开关（来自 CYFrameworkConfig.Debug）
        /// <summary>
        /// 是否启用 GM 命令
        /// </summary>
        private bool _enableGMCommands = true;
        
        // 是否显示
        /// <summary>
        /// 是否可见
        /// </summary>
        private bool _isVisible;
        
        // 命令注册表
        /// <summary>
        /// 命令注册表
        /// </summary>
        private readonly Dictionary<string, CommandInfo> _commands = new();
        
        // 命令历史
        /// <summary>
        /// 命令历史列表
        /// </summary>
        private readonly List<string> _commandHistory = new();
        /// <summary>
        /// 历史索引
        /// </summary>
        private int _historyIndex;
        
        // 日志
        /// <summary>
        /// 控制台日志列表
        /// </summary>
        private readonly List<LogEntry> _logs = new();
        
        // UI
        /// <summary>
        /// 输入文本
        /// </summary>
        private string _inputText = "";
        /// <summary>
        /// 日志滚动位置
        /// </summary>
        private Vector2 _logScrollPos;
        /// <summary>
        /// 面板样式
        /// </summary>
        private GUIStyle _boxStyle;
        /// <summary>
        /// 输入样式
        /// </summary>
        private GUIStyle _inputStyle;
        /// <summary>
        /// 日志样式
        /// </summary>
        private GUIStyle _logStyle;
        
        // 触摸呼出（移动端）
        /// <summary>
        /// 触摸计数
        /// </summary>
        private int _touchCount;
        /// <summary>
        /// 触摸计时
        /// </summary>
        private float _touchTimer;
        
        /// <summary>
        /// 日志条目
        /// </summary>
        private struct LogEntry
        {
            /// <summary>
            /// 日志文本
            /// </summary>
            public string Text;
            /// <summary>
            /// 日志类型
            /// </summary>
            public LogType Type;
        }
        
        /// <summary>
        /// Unity Awake
        /// </summary>
        private void Awake()
        {
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            // Release 版本禁用
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
                    if (!config.EnableConsole)
                    {
                        gameObject.SetActive(false);
                        return;
                    }

                    _toggleKey = config.ConsoleToggleKey;
                    _enableGMCommands = config.EnableGMCommands;
                }
            }
            
            // 注册内置命令
            if (_enableGMCommands)
            {
                RegisterBuiltinCommands();
            }
            
            // 监听 Unity 日志
            Application.logMessageReceived += OnLogReceived;
        }
        
        /// <summary>
        /// Unity OnDestroy
        /// </summary>
        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogReceived;
        }
        
        /// <summary>
        /// Unity Update
        /// </summary>
        private void Update()
        {
            // 键盘呼出
            if (Input.GetKeyDown(_toggleKey))
            {
                Toggle();
            }
            
            // 触摸呼出（5 指点击 3 次）
            CheckTouchActivation();
        }
        
        /// <summary>
        /// Unity OnGUI
        /// </summary>
        private void OnGUI()
        {
            if (!_isVisible) return;
            
            InitStyles();
            
            // 控制台面板
            // 面板宽度
            float panelWidth = Screen.width * 0.8f;
            // 面板高度
            float panelHeight = Screen.height * 0.5f;
            // 面板矩形
            Rect panelRect = new Rect(
                (Screen.width - panelWidth) / 2,
                Screen.height - panelHeight - 10,
                panelWidth,
                panelHeight
            );
            
            GUI.Box(panelRect, "", _boxStyle);
            
            GUILayout.BeginArea(new Rect(panelRect.x + 10, panelRect.y + 10, panelRect.width - 20, panelRect.height - 20));
            
            // 日志区域
            _logScrollPos = GUILayout.BeginScrollView(_logScrollPos, GUILayout.Height(panelHeight - 80));
            
            // 遍历日志列表
            foreach (var log in _logs)
            {
                // 当前日志
                Color color = log.Type switch
                {
                    LogType.Error => Color.red,
                    LogType.Exception => Color.red,
                    LogType.Warning => Color.yellow,
                    _ => Color.white
                };
                
                GUI.contentColor = color;
                GUILayout.Label(log.Text, _logStyle);
            }
            GUI.contentColor = Color.white;
            
            GUILayout.EndScrollView();
            
            // 输入区域
            GUILayout.Space(5);
            
            GUI.SetNextControlName("ConsoleInput");
            
            // 当前事件
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                HandleInputKey(e);
            }
            
            GUILayout.BeginHorizontal();
            GUILayout.Label(">", GUILayout.Width(15));
            _inputText = GUILayout.TextField(_inputText, _inputStyle);
            
            if (GUILayout.Button("执行", GUILayout.Width(60)))
            {
                ExecuteInput();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.EndArea();
            
            // 聚焦输入框
            if (_isVisible)
            {
                GUI.FocusControl("ConsoleInput");
            }
        }
        
        #region 公开 API
        
        /// <summary>
        /// 切换显示
        /// </summary>
        public void Toggle()
        {
            _isVisible = !_isVisible;
            _inputText = "";
        }
        
        /// <summary>
        /// 显示控制台
        /// </summary>
        public void Show()
        {
            _isVisible = true;
        }
        
        /// <summary>
        /// 隐藏控制台
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
        }
        
        /// <summary>
        /// 注册命令
        /// </summary>
        public void RegisterCommand(string name, Action<string[]> action, string description = "", string usage = "")
        {
            _commands[name.ToLower()] = new CommandInfo
            {
                Name = name,
                Description = description,
                Usage = usage,
                Method = action.Method,
                Target = action.Target
            };
        }
        
        /// <summary>
        /// 注册对象的所有命令
        /// </summary>
        public void RegisterCommands(object target)
        {
            // 目标对象方法列表
            var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (var method in methods)
            {
                // 当前方法
                var attr = method.GetCustomAttribute<ConsoleCommandAttribute>();
                if (attr != null)
                {
                    _commands[attr.Name.ToLower()] = new CommandInfo
                    {
                        Name = attr.Name,
                        Description = attr.Description,
                        Usage = attr.Usage,
                        Method = method,
                        Target = target
                    };
                }
            }
        }
        
        /// <summary>
        /// 输出到控制台
        /// </summary>
        public void Log(string message, LogType type = LogType.Log)
        {
            _logs.Add(new LogEntry { Text = message, Type = type });
            
            if (_logs.Count > _maxLogLines)
            {
                _logs.RemoveAt(0);
            }
            
            // 滚动到底部
            _logScrollPos.y = float.MaxValue;
        }
        
        /// <summary>
        /// 执行命令
        /// </summary>
        public void Execute(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            
            Log($"> {command}");
            
            // 解析命令
            // 分割后的命令片段
            var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            
            // 命令名称
            string cmdName = parts[0].ToLower();
            // 命令参数
            string[] args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);
            
            // 查找并执行命令
            // 命令信息
            if (_commands.TryGetValue(cmdName, out var cmdInfo))
            {
                try
                {
                    // 调用方法
                    // 参数信息
                    var parameters = cmdInfo.Method.GetParameters();
                    
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                    {
                        cmdInfo.Method.Invoke(cmdInfo.Target, new object[] { args });
                    }
                    else if (parameters.Length == 0)
                    {
                        cmdInfo.Method.Invoke(cmdInfo.Target, null);
                    }
                    else
                    {
                        Log($"命令参数不匹配: {cmdName}", LogType.Error);
                    }
                }
                catch (Exception ex)
                {
                    Log($"命令执行失败: {ex.Message}", LogType.Error);
                }
            }
            else
            {
                Log($"未知命令: {cmdName}，输入 'help' 查看帮助", LogType.Warning);
            }
        }
        
        #endregion
        
        #region 内置命令
        
        /// <summary>
        /// 注册内置命令
        /// </summary>
        private void RegisterBuiltinCommands()
        {
            RegisterCommand("help", CmdHelp, "显示帮助信息", "help [命令名]");
            RegisterCommand("clear", CmdClear, "清空控制台");
            RegisterCommand("fps", CmdFPS, "设置目标帧率", "fps <帧率>");
            RegisterCommand("timescale", CmdTimeScale, "设置时间缩放", "timescale <倍率>");
            RegisterCommand("gc", CmdGC, "强制 GC");
            RegisterCommand("log", CmdLog, "设置日志级别", "log <级别>");
            RegisterCommand("quit", CmdQuit, "退出游戏");
        }
        
        /// <summary>
        /// 帮助命令
        /// </summary>
        private void CmdHelp(string[] args)
        {
            if (args.Length > 0)
            {
                // 命令名称
                string cmdName = args[0].ToLower();
                // 命令信息
                if (_commands.TryGetValue(cmdName, out var cmd))
                {
                    Log($"命令: {cmd.Name}");
                    Log($"  描述: {cmd.Description}");
                    Log($"  用法: {cmd.Usage}");
                }
                else
                {
                    Log($"未知命令: {cmdName}", LogType.Warning);
                }
            }
            else
            {
                Log("=== 可用命令 ===");
                foreach (var cmd in _commands.Values)
                {
                    // 当前命令
                    Log($"  {cmd.Name,-15} - {cmd.Description}");
                }
            }
        }
        
        /// <summary>
        /// 清空控制台命令
        /// </summary>
        private void CmdClear(string[] args)
        {
            _logs.Clear();
        }
        
        /// <summary>
        /// FPS 显示命令
        /// </summary>
        private void CmdFPS(string[] args)
        {
            // 目标帧率
            if (args.Length > 0 && int.TryParse(args[0], out int fps))
            {
                Application.targetFrameRate = fps;
                Log($"目标帧率设置为: {fps}");
            }
            else
            {
                Log($"当前帧率: {Application.targetFrameRate}");
            }
        }
        
        /// <summary>
        /// TimeScale 命令
        /// </summary>
        private void CmdTimeScale(string[] args)
        {
            // 时间缩放倍率
            if (args.Length > 0 && float.TryParse(args[0], out float scale))
            {
                Time.timeScale = scale;
                Log($"时间缩放设置为: {scale}");
            }
            else
            {
                Log($"当前时间缩放: {Time.timeScale}");
            }
        }
        
        /// <summary>
        /// GC 命令
        /// </summary>
        private void CmdGC(string[] args)
        {
            // GC 前内存
            long before = GC.GetTotalMemory(false);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            // GC 后内存
            long after = GC.GetTotalMemory(true);
            
            Log($"GC 完成，释放: {(before - after) / 1024f:F2} KB");
        }
        
        /// <summary>
        /// 输出日志命令
        /// </summary>
        private void CmdLog(string[] args)
        {
            if (args.Length > 0)
            {
                // 目标日志级别
                if (Enum.TryParse<LogLevel>(args[0], true, out var level))
                {
                    CYLog.SetMinLevel(level);
                    Log($"日志级别设置为: {level}");
                }
                else
                {
                    Log("无效的日志级别", LogType.Error);
                }
            }
            else
            {
                Log("用法: log <Trace|Debug|Info|Warning|Error|Fatal>");
            }
        }
        
        /// <summary>
        /// 退出应用命令
        /// </summary>
        private void CmdQuit(string[] args)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 初始化 GUI 样式
        /// </summary>
        private void InitStyles()
        {
            if (_boxStyle != null) return;
            
            _boxStyle = new GUIStyle(GUI.skin.box);
            // 背景纹理
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.9f));
            bgTex.Apply();
            _boxStyle.normal.background = bgTex;
            
            _inputStyle = new GUIStyle(GUI.skin.textField);
            _inputStyle.fontSize = 14;
            
            _logStyle = new GUIStyle(GUI.skin.label);
            _logStyle.fontSize = 12;
            _logStyle.wordWrap = true;
        }
        
        /// <summary>
        /// 处理输入按键
        /// </summary>
        private void HandleInputKey(Event e)
        {
            switch (e.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ExecuteInput();
                    e.Use();
                    break;
                    
                case KeyCode.UpArrow:
                    NavigateHistory(-1);
                    e.Use();
                    break;
                    
                case KeyCode.DownArrow:
                    NavigateHistory(1);
                    e.Use();
                    break;
                    
                case KeyCode.Escape:
                    Hide();
                    e.Use();
                    break;
            }
        }
        
        /// <summary>
        /// 执行输入命令
        /// </summary>
        private void ExecuteInput()
        {
            if (string.IsNullOrWhiteSpace(_inputText)) return;
            
            // 添加到历史
            _commandHistory.Add(_inputText);
            if (_commandHistory.Count > _maxHistorySize)
            {
                _commandHistory.RemoveAt(0);
            }
            _historyIndex = _commandHistory.Count;
            
            Execute(_inputText);
            _inputText = "";
        }
        
        /// <summary>
        /// 历史命令导航
        /// </summary>
        private void NavigateHistory(int direction)
        {
            if (_commandHistory.Count == 0) return;
            
            _historyIndex = Mathf.Clamp(_historyIndex + direction, 0, _commandHistory.Count);
            
            if (_historyIndex < _commandHistory.Count)
            {
                _inputText = _commandHistory[_historyIndex];
            }
            else
            {
                _inputText = "";
            }
        }
        
        /// <summary>
        /// 检查触摸激活
        /// </summary>
        private void CheckTouchActivation()
        {
            // 5 指点击 3 次呼出
            if (Input.touchCount >= 5)
            {
                _touchCount++;
                _touchTimer = 0;
                
                if (_touchCount >= 3)
                {
                    Toggle();
                    _touchCount = 0;
                }
            }
            else
            {
                _touchTimer += Time.unscaledDeltaTime;
                if (_touchTimer > 0.5f)
                {
                    _touchCount = 0;
                }
            }
        }
        
        /// <summary>
        /// Unity 日志回调
        /// </summary>
        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                Log(condition, type);
            }
        }
        
        #endregion
    }
}
