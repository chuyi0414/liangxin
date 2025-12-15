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
        
        public PlatformType Platform => PlatformType.WeChat;
        public long StorageLimit => STORAGE_LIMIT;
        public long StorageUsed => GetStorageUsed();
        
        #region JS 桥接
        
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string WX_GetStorage(string key);
        
        [DllImport("__Internal")]
        private static extern void WX_SetStorage(string key, string value);
        
        [DllImport("__Internal")]
        private static extern void WX_RemoveStorage(string key);
        
        [DllImport("__Internal")]
        private static extern void WX_ClearStorage();
        
        [DllImport("__Internal")]
        private static extern int WX_GetStorageInfoUsed();
        
        [DllImport("__Internal")]
        private static extern bool WX_HasStorageKey(string key);
#else
        // Editor 模式下的模拟实现
        private static readonly System.Collections.Generic.Dictionary<string, string> _mockStorage = new();
        
        private static string WX_GetStorage(string key)
        {
            return _mockStorage.TryGetValue(key, out var value) ? value : "";
        }
        
        private static void WX_SetStorage(string key, string value)
        {
            _mockStorage[key] = value;
        }
        
        private static void WX_RemoveStorage(string key)
        {
            _mockStorage.Remove(key);
        }
        
        private static void WX_ClearStorage()
        {
            _mockStorage.Clear();
        }
        
        private static int WX_GetStorageInfoUsed()
        {
            int total = 0;
            foreach (var kvp in _mockStorage)
            {
                total += kvp.Key.Length + kvp.Value.Length;
            }
            return total;
        }
        
        private static bool WX_HasStorageKey(string key)
        {
            return _mockStorage.ContainsKey(key);
        }
#endif
        
        #endregion
        
        public void Initialize()
        {
            CYLog.Debug("[WeChatStorageAdapter] 初始化完成");
        }
        
        public string GetString(string key, string defaultValue = "")
        {
            try
            {
                string value = WX_GetStorage(key);
                return string.IsNullOrEmpty(value) ? defaultValue : value;
            }
            catch (Exception ex)
            {
                CYLog.Error($"[WeChatStorageAdapter] GetString 失败: {key}", ex);
                return defaultValue;
            }
        }
        
        public void SetString(string key, string value)
        {
            try
            {
                WX_SetStorage(key, value);
            }
            catch (Exception ex)
            {
                CYLog.Error($"[WeChatStorageAdapter] SetString 失败: {key}", ex);
            }
        }
        
        public int GetInt(string key, int defaultValue = 0)
        {
            string value = GetString(key, null);
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return int.TryParse(value, out int result) ? result : defaultValue;
        }
        
        public void SetInt(string key, int value)
        {
            SetString(key, value.ToString());
        }
        
        public float GetFloat(string key, float defaultValue = 0f)
        {
            string value = GetString(key, null);
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return float.TryParse(value, out float result) ? result : defaultValue;
        }
        
        public void SetFloat(string key, float value)
        {
            SetString(key, value.ToString());
        }
        
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
        
        public void DeleteKey(string key)
        {
            try
            {
                WX_RemoveStorage(key);
            }
            catch (Exception ex)
            {
                CYLog.Error($"[WeChatStorageAdapter] DeleteKey 失败: {key}", ex);
            }
        }
        
        public void DeleteAll()
        {
            try
            {
                WX_ClearStorage();
                CYLog.Warning("[WeChatStorageAdapter] 已清空所有存储");
            }
            catch (Exception ex)
            {
                CYLog.Error("[WeChatStorageAdapter] DeleteAll 失败", ex);
            }
        }
        
        public void Save()
        {
            // 微信的 setStorageSync 是同步的，无需额外保存
            CYLog.Trace("[WeChatStorageAdapter] Save (微信自动同步)");
        }
        
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
