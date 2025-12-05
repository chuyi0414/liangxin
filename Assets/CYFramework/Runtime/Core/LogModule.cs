// ============================================================================
// CYFramework - 日志模块
// 封装 Unity 日志，提供日志等级控制和统一格式
// 
// 设计要点：
// - 支持日志等级（Debug/Info/Warning/Error）
// - 发布版本可关闭低级别日志
// - 统一日志格式，方便追踪
// ============================================================================

using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 日志等级
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 调试信息（最详细，仅开发时使用）
        /// </summary>
        Debug = 0,

        /// <summary>
        /// 普通信息
        /// </summary>
        Info = 1,

        /// <summary>
        /// 警告信息
        /// </summary>
        Warning = 2,

        /// <summary>
        /// 错误信息
        /// </summary>
        Error = 3,

        /// <summary>
        /// 关闭所有日志
        /// </summary>
        Off = 4
    }

    /// <summary>
    /// 日志模块
    /// 提供统一的日志输出接口
    /// </summary>
    public class LogModule : IModule
    {
        // ====================================================================
        // IModule 实现
        // ====================================================================

        public int Priority => -10; // 日志模块优先级最高，最先初始化
        public bool NeedUpdate => false;

        public void Initialize()
        {
            Debug.Log("[LogModule] 初始化完成");
        }

        public void Update(float deltaTime) { }

        public void Shutdown()
        {
            Debug.Log("[LogModule] 已关闭");
        }

        // ====================================================================
        // 日志配置
        // ====================================================================

        /// <summary>
        /// 当前日志等级，低于此等级的日志不会输出
        /// </summary>
        public LogLevel CurrentLogLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// 是否显示时间戳
        /// </summary>
        public bool ShowTimestamp { get; set; } = true;

        /// <summary>
        /// 是否显示模块名
        /// </summary>
        public bool ShowModule { get; set; } = true;

        /// <summary>
        /// 是否启用日志（总开关）
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 设置日志等级
        /// </summary>
        public void SetLogLevel(LogLevel level)
        {
            CurrentLogLevel = level;
        }

        // ====================================================================
        // 日志方法
        // ====================================================================

        /// <summary>
        /// 输出调试日志
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void D(string tag, string message)
        {
            Log(LogLevel.Debug, tag, message);
        }

        /// <summary>
        /// 输出信息日志
        /// </summary>
        public void I(string tag, string message)
        {
            Log(LogLevel.Info, tag, message);
        }

        /// <summary>
        /// 输出警告日志
        /// </summary>
        public void W(string tag, string message)
        {
            Log(LogLevel.Warning, tag, message);
        }

        /// <summary>
        /// 输出错误日志
        /// </summary>
        public void E(string tag, string message)
        {
            Log(LogLevel.Error, tag, message);
        }

        /// <summary>
        /// 输出错误日志（带异常）
        /// </summary>
        public void E(string tag, string message, Exception exception)
        {
            Log(LogLevel.Error, tag, $"{message}\n{exception}");
        }

        /// <summary>
        /// 通用日志输出
        /// </summary>
        public void Log(LogLevel level, string tag, string message)
        {
            if (!IsEnabled) return;
            if (level < CurrentLogLevel) return;

            string formattedMessage = FormatMessage(level, tag, message);

            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(formattedMessage);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(formattedMessage);
                    break;
                case LogLevel.Error:
                    Debug.LogError(formattedMessage);
                    break;
            }
        }

        /// <summary>
        /// 格式化日志消息
        /// </summary>
        private string FormatMessage(LogLevel level, string tag, string message)
        {
            string levelStr = level switch
            {
                LogLevel.Debug => "D",
                LogLevel.Info => "I",
                LogLevel.Warning => "W",
                LogLevel.Error => "E",
                _ => "?"
            };

            string result = "";
            
            if (ShowTimestamp)
            {
                result += $"[{DateTime.Now:HH:mm:ss.fff}]";
            }
            
            result += $"[{levelStr}]";
            
            if (ShowModule && !string.IsNullOrEmpty(tag))
            {
                result += $"[{tag}]";
            }
            
            result += $" {message}";
            return result;
        }
    }

    /// <summary>
    /// 日志静态工具类
    /// 提供便捷的静态方法，内部调用 LogModule
    /// </summary>
    public static class Log
    {
        private static LogModule _module;

        private static LogModule Module
        {
            get
            {
                if (_module == null)
                {
                    _module = CYFrameworkEntry.Instance?.GetModule<LogModule>();
                }
                return _module;
            }
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void D(string tag, string message)
        {
            Module?.D(tag, message);
        }

        /// <summary>
        /// 信息日志
        /// </summary>
        public static void I(string tag, string message)
        {
            if (Module != null)
                Module.I(tag, message);
            else
                Debug.Log($"[I][{tag}] {message}");
        }

        /// <summary>
        /// 警告日志
        /// </summary>
        public static void W(string tag, string message)
        {
            if (Module != null)
                Module.W(tag, message);
            else
                Debug.LogWarning($"[W][{tag}] {message}");
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        public static void E(string tag, string message)
        {
            if (Module != null)
                Module.E(tag, message);
            else
                Debug.LogError($"[E][{tag}] {message}");
        }

        /// <summary>
        /// 错误日志（带异常）
        /// </summary>
        public static void E(string tag, string message, Exception exception)
        {
            if (Module != null)
                Module.E(tag, message, exception);
            else
                Debug.LogError($"[E][{tag}] {message}\n{exception}");
        }
    }
}
