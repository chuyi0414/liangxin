// ============================================================================
// CYFramework 2.2 - 微信小游戏存储适配器
// 文档位置：3.1.4 存档系统 - 微信适配
// 使用 wx.setStorageSync / wx.getStorageSync (上限 10MB)
// ============================================================================

#if CY_WECHAT || UNITY_WEBGL

using System;
using System.Runtime.InteropServices;
using CYFramework.Infrastructure;

namespace CYFramework.Platform.WeChat
{
    /// <summary>
    /// 微信小游戏存储适配器
    /// 基于 wx.setStorageSync / wx.getStorageSync
    /// </summary>
    public class WeChatStorageAdapter : IStorageAdapter
    {
        // 微信存储上限 10MB
        private const long STORAGE_LIMIT = 10 * 1024 * 1024;
        
        /// <summary>
        /// 平台类型
        /// </summary>
        public PlatformType Platform => PlatformType.WeChat;
        /// <summary>
        /// 存储上限
        /// </summary>
        public long StorageLimit => STORAGE_LIMIT;
        /// <summary>
        /// 已使用存储
        /// </summary>
        public long StorageUsed => GetStorageUsed();
        
        #region JS 桥接
        
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        /// <summary>
        /// 获取存储字符串
        /// </summary>
        private static extern string WX_GetStorage(string key);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 设置存储字符串
        /// </summary>
        private static extern void WX_SetStorage(string key, string value);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 删除存储键
        /// </summary>
        private static extern void WX_RemoveStorage(string key);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 清空存储
        /// </summary>
        private static extern void WX_ClearStorage();
        
        [DllImport("__Internal")]
        /// <summary>
        /// 获取已用存储字节数
        /// </summary>
        private static extern int WX_GetStorageInfoUsed();
        
        [DllImport("__Internal")]
        /// <summary>
        /// 是否存在存储键
        /// </summary>
        private static extern bool WX_HasStorageKey(string key);
#else
        // Editor 模式下的模拟实现
        private static readonly System.Collections.Generic.Dictionary<string, string> _mockStorage = new();
        
        /// <summary>
        /// 获取存储字符串（模拟）
        /// </summary>
        private static string WX_GetStorage(string key)
        {
            return _mockStorage.TryGetValue(key, out var value) ? value : ""; // value 为存储值
        }
        
        /// <summary>
        /// 设置存储字符串（模拟）
        /// </summary>
        private static void WX_SetStorage(string key, string value)
        {
            _mockStorage[key] = value;
        }
        
        /// <summary>
        /// 删除存储键（模拟）
        /// </summary>
        private static void WX_RemoveStorage(string key)
        {
            _mockStorage.Remove(key);
        }
        
        /// <summary>
        /// 清空存储（模拟）
        /// </summary>
        private static void WX_ClearStorage()
        {
            _mockStorage.Clear();
        }
        
        /// <summary>
        /// 获取已用存储字节数（模拟）
        /// </summary>
        private static int WX_GetStorageInfoUsed()
        {
            // 累计字节数
            int total = 0;
            foreach (var kvp in _mockStorage) // kvp 为键值对
            {
                total += kvp.Key.Length + kvp.Value.Length;
            }
            return total;
        }
        
        /// <summary>
        /// 是否存在存储键（模拟）
        /// </summary>
        private static bool WX_HasStorageKey(string key)
        {
            return _mockStorage.ContainsKey(key);
        }
#endif
        
        #endregion
        
        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            CYLog.Debug("[WeChatStorageAdapter] 初始化完成");
        }
        
        /// <summary>
        /// 获取字符串
        /// </summary>
        public string GetString(string key, string defaultValue = "")
        {
            try
            {
                // 存储值
                string value = WX_GetStorage(key);
                return string.IsNullOrEmpty(value) ? defaultValue : value;
            }
            catch (Exception ex)
            {
                // ex 为读取异常
                CYLog.Error($"[WeChatStorageAdapter] GetString 失败: {key}", ex);
                return defaultValue;
            }
        }
        
        /// <summary>
        /// 设置字符串
        /// </summary>
        public void SetString(string key, string value)
        {
            try
            {
                WX_SetStorage(key, value);
            }
            catch (Exception ex)
            {
                // ex 为写入异常
                CYLog.Error($"[WeChatStorageAdapter] SetString 失败: {key}", ex);
            }
        }
        
        /// <summary>
        /// 获取整数
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            // 字符串值
            string value = GetString(key, null);
            if (string.IsNullOrEmpty(value)) return defaultValue;
            // 解析结果
            return int.TryParse(value, out int result) ? result : defaultValue; // result 为解析结果
        }
        
        /// <summary>
        /// 设置整数
        /// </summary>
        public void SetInt(string key, int value)
        {
            SetString(key, value.ToString());
        }
        
        /// <summary>
        /// 获取浮点数
        /// </summary>
        public float GetFloat(string key, float defaultValue = 0f)
        {
            // 字符串值
            string value = GetString(key, null);
            if (string.IsNullOrEmpty(value)) return defaultValue;
            // 解析结果
            return float.TryParse(value, out float result) ? result : defaultValue; // result 为解析结果
        }
        
        /// <summary>
        /// 设置浮点数
        /// </summary>
        public void SetFloat(string key, float value)
        {
            SetString(key, value.ToString());
        }
        
        /// <summary>
        /// 检查键是否存在
        /// </summary>
        public bool HasKey(string key)
        {
            try
            {
                return WX_HasStorageKey(key);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 删除键
        /// </summary>
        public void DeleteKey(string key)
        {
            try
            {
                WX_RemoveStorage(key);
            }
            catch (Exception ex)
            {
                // ex 为删除异常
                CYLog.Error($"[WeChatStorageAdapter] DeleteKey 失败: {key}", ex);
            }
        }
        
        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void DeleteAll()
        {
            try
            {
                WX_ClearStorage();
                CYLog.Warning("[WeChatStorageAdapter] 已清空所有存储");
            }
            catch (Exception ex)
            {
                // ex 为清空异常
                CYLog.Error("[WeChatStorageAdapter] DeleteAll 失败", ex);
            }
        }
        
        /// <summary>
        /// 保存
        /// </summary>
        public void Save()
        {
            // 微信的 setStorageSync 是同步的，无需额外保存
            CYLog.Trace("[WeChatStorageAdapter] Save (微信自动同步)");
        }
        
        /// <summary>
        /// 获取已使用存储
        /// </summary>
        private long GetStorageUsed()
        {
            try
            {
                return WX_GetStorageInfoUsed();
            }
            catch
            {
                return 0;
            }
        }
    }
}

#endif
