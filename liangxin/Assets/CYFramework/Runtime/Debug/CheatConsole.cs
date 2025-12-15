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
        public string Name { get; }
        public string Description { get; }
        public string Usage { get; }
        
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
        public string Name;
        public string Description;
        public string Usage;
        public MethodInfo Method;
        public object Target;
    }
    
    /// <summary>
    /// 命令控制台
    /// 文档：开发环境下通过特定手势/按键呼出
    /// </summary>
    public class CheatConsole : MonoBehaviour
    {
        [Header("设置")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.BackQuote; // ~ 键
        [SerializeField] private int _maxLogLines = 100;
        [SerializeField] private int _maxHistorySize = 50;

        // 配置开关（来自 CYFrameworkConfig.Debug）
        private bool _enableGMCommands = true;
        
        // 是否显示
        private bool _isVisible;
        
        // 命令注册表
        private readonly Dictionary<string, CommandInfo> _commands = new();
        
        // 命令历史
        private readonly List<string> _commandHistory = new();
        private int _historyIndex;
        
        // 日志
        private readonly List<LogEntry> _logs = new();
        
        // UI
        private string _inputText = "";
        private Vector2 _logScrollPos;
        private GUIStyle _boxStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _logStyle;
        
        // 触摸呼出（移动端）
        private int _touchCount;
        private float _touchTimer;
        
        private struct LogEntry
        {
            public string Text;
            public LogType Type;
        }
        
        private void Awake()
        {
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            // Release 版本禁用
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
        
        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogReceived;
        }
        
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
        
        private void OnGUI()
        {
            if (!_isVisible) return;
            
            InitStyles();
            
            // 控制台面板
            float panelWidth = Screen.width * 0.8f;
            float panelHeight = Screen.height * 0.5f;
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
            
            foreach (var log in _logs)
            {
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
            var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (var method in methods)
            {
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
            var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            
            string cmdName = parts[0].ToLower();
            string[] args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);
            
            // 查找并执行命令
            if (_commands.TryGetValue(cmdName, out var cmdInfo))
            {
                try
                {
                    // 调用方法
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
        
        private void CmdHelp(string[] args)
        {
            if (args.Length > 0)
            {
                string cmdName = args[0].ToLower();
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
                    Log($"  {cmd.Name,-15} - {cmd.Description}");
                }
            }
        }
        
        private void CmdClear(string[] args)
        {
            _logs.Clear();
        }
        
        private void CmdFPS(string[] args)
        {
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
        
        private void CmdTimeScale(string[] args)
        {
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
        
        private void CmdGC(string[] args)
        {
            long before = GC.GetTotalMemory(false);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long after = GC.GetTotalMemory(true);
            
            Log($"GC 完成，释放: {(before - after) / 1024f:F2} KB");
        }
        
        private void CmdLog(string[] args)
        {
            if (args.Length > 0)
            {
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
        
        private void InitStyles()
        {
            if (_boxStyle != null) return;
            
            _boxStyle = new GUIStyle(GUI.skin.box);
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
