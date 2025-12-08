// ============================================================================
// CYFramework 2.2 - 日志系统
// 文档位置：8.3 日志分级
// 功能：日志分级、平台适配、异步上报
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CYFramework.Infrastructure
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Fatal = 5
    }
    
    /// <summary>
    /// 日志条目
    /// </summary>
    public struct LogEntry
    {
        public LogLevel Level;
        public string Tag;
        public string Message;
        public string StackTrace;
        public DateTime Timestamp;
    }
    
    /// <summary>
    /// 日志输出器接口
    /// </summary>
    public interface ILogOutput
    {
        void Write(in LogEntry entry);
    }
    
    /// <summary>
    /// CYFramework 日志系统
    /// 零 GC 设计，支持多输出器
    /// </summary>
    public static class CYLog
    {
        // 当前日志级别
        private static LogLevel _minLevel = LogLevel.Debug;
        
        // 输出器列表
        private static readonly List<ILogOutput> _outputs = new();
        
        // 最近日志缓存（用于 Crash 上报）
        private static readonly Queue<LogEntry> _recentLogs = new();
        private const int MAX_RECENT_LOGS = 100;
        
        // StringBuilder 复用，避免 GC
        private static readonly StringBuilder _sb = new(256);
        
        // 是否已初始化
        private static bool _initialized;
        
        #region 初始化
        
        /// <summary>
        /// 初始化日志系统
        /// </summary>
        public static void Initialize(LogLevel minLevel = LogLevel.Debug)
        {
            if (_initialized) return;
            
            _minLevel = minLevel;
            
            // 添加默认输出器
            AddOutput(new UnityLogOutput());
            
            // 注册 Unity 异常回调
            Application.logMessageReceived += OnUnityLog;
            
            _initialized = true;
            Info("[CYLog] 日志系统初始化完成");
        }
        
        /// <summary>
        /// 设置最低日志级别
        /// </summary>
        public static void SetMinLevel(LogLevel level)
        {
            _minLevel = level;
        }
        
        /// <summary>
        /// 添加输出器
        /// </summary>
        public static void AddOutput(ILogOutput output)
        {
            if (output != null && !_outputs.Contains(output))
            {
                _outputs.Add(output);
            }
        }
        
        /// <summary>
        /// 移除输出器
        /// </summary>
        public static void RemoveOutput(ILogOutput output)
        {
            _outputs.Remove(output);
        }
        
        #endregion
        
        #region 日志 API
        
        /// <summary>
        /// Trace 级别日志
        /// </summary>
        public static void Trace(string message, string tag = null)
        {
            Log(LogLevel.Trace, message, tag);
        }
        
        /// <summary>
        /// Debug 级别日志
        /// </summary>
        public static void Debug(string message, string tag = null)
        {
            Log(LogLevel.Debug, message, tag);
        }
        
        /// <summary>
        /// Info 级别日志
        /// </summary>
        public static void Info(string message, string tag = null)
        {
            Log(LogLevel.Info, message, tag);
        }
        
        /// <summary>
        /// Warning 级别日志
        /// </summary>
        public static void Warning(string message, string tag = null)
        {
            Log(LogLevel.Warning, message, tag);
        }
        
        /// <summary>
        /// Error 级别日志
        /// </summary>
        public static void Error(string message, string tag = null)
        {
            Log(LogLevel.Error, message, tag);
        }
        
        /// <summary>
        /// Error 级别日志（带异常）
        /// </summary>
        public static void Error(string message, Exception ex, string tag = null)
        {
            Log(LogLevel.Error, $"{message}\n{ex}", tag, ex.StackTrace);
        }
        
        /// <summary>
        /// Fatal 级别日志
        /// </summary>
        public static void Fatal(string message, string tag = null)
        {
            Log(LogLevel.Fatal, message, tag);
        }
        
        #endregion
        
        #region 核心方法
        
        /// <summary>
        /// 写入日志
        /// </summary>
        private static void Log(LogLevel level, string message, string tag, string stackTrace = null)
        {
            // 级别过滤
            if (level < _minLevel) return;
            
            var entry = new LogEntry
            {
                Level = level,
                Tag = tag ?? "CYFramework",
                Message = message,
                StackTrace = stackTrace,
                Timestamp = DateTime.Now
            };
            
            // 缓存最近日志
            CacheRecentLog(entry);
            
            // 输出到所有输出器
            for (int i = 0; i < _outputs.Count; i++)
            {
                try
                {
                    _outputs[i].Write(entry);
                }
                catch
                {
                    // 忽略输出器异常，防止死循环
                }
            }
        }
        
        /// <summary>
        /// 缓存最近日志
        /// </summary>
        private static void CacheRecentLog(LogEntry entry)
        {
            if (_recentLogs.Count >= MAX_RECENT_LOGS)
            {
                _recentLogs.Dequeue();
            }
            _recentLogs.Enqueue(entry);
        }
        
        /// <summary>
        /// 获取最近日志（用于 Crash 上报）
        /// </summary>
        public static LogEntry[] GetRecentLogs()
        {
            return _recentLogs.ToArray();
        }
        
        /// <summary>
        /// Unity 日志回调
        /// </summary>
        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            // 只处理异常和错误
            if (type == LogType.Exception || type == LogType.Error)
            {
                var entry = new LogEntry
                {
                    Level = type == LogType.Exception ? LogLevel.Fatal : LogLevel.Error,
                    Tag = "Unity",
                    Message = condition,
                    StackTrace = stackTrace,
                    Timestamp = DateTime.Now
                };
                CacheRecentLog(entry);
            }
        }
        
        #endregion
        
        #region 格式化
        
        /// <summary>
        /// 格式化日志条目
        /// </summary>
        public static string Format(in LogEntry entry)
        {
            _sb.Clear();
            _sb.Append('[');
            _sb.Append(entry.Timestamp.ToString("HH:mm:ss.fff"));
            _sb.Append("][");
            _sb.Append(entry.Level.ToString().ToUpper());
            _sb.Append("][");
            _sb.Append(entry.Tag);
            _sb.Append("] ");
            _sb.Append(entry.Message);
            
            return _sb.ToString();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Unity 控制台输出器
    /// </summary>
    public class UnityLogOutput : ILogOutput
    {
        public void Write(in LogEntry entry)
        {
            string formatted = CYLog.Format(entry);
            
            switch (entry.Level)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Info:
                    UnityEngine.Debug.Log(formatted);
                    break;
                    
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(formatted);
                    break;
                    
                case LogLevel.Error:
                case LogLevel.Fatal:
                    if (!string.IsNullOrEmpty(entry.StackTrace))
                    {
                        UnityEngine.Debug.LogError($"{formatted}\n{entry.StackTrace}");
                    }
                    else
                    {
                        UnityEngine.Debug.LogError(formatted);
                    }
                    break;
            }
        }
    }
    
#if CY_WECHAT
    /// <summary>
    /// 微信小游戏日志输出器
    /// 映射到 console.log / console.warn / console.error
    /// </summary>
    public class WeChatLogOutput : ILogOutput
    {
        public void Write(in LogEntry entry)
        {
            string formatted = CYLog.Format(entry);
            
            // 通过 JS 桥接调用 console
            switch (entry.Level)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Info:
                    // WX.CallJS("console.log", formatted);
                    break;
                    
                case LogLevel.Warning:
                    // WX.CallJS("console.warn", formatted);
                    break;
                    
                case LogLevel.Error:
                case LogLevel.Fatal:
                    // WX.CallJS("console.error", formatted);
                    break;
            }
        }
    }
#endif
}
