// ============================================================================
// CYFramework 2.2 - Unity 存储适配器
// 适用平台：PC / Android / iOS
// ============================================================================

using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Platform.Unity
{
    /// <summary>
    /// Unity 平台存储实现 (基于 PlayerPrefs)
    /// </summary>
    public class UnityStorageAdapter : IStorageAdapter
    {
        public PlatformType Platform
        {
            get
            {
                #if UNITY_ANDROID
                return PlatformType.Android;
                #elif UNITY_IOS
                return PlatformType.iOS;
                #else
                return PlatformType.PC;
                #endif
            }
        }
        
        // PlayerPrefs 没有明确的大小限制，设为 50MB
        public long StorageLimit => 50 * 1024 * 1024;
        
        // 无法精确获取，返回 0
        public long StorageUsed => 0;
        
        public void Initialize()
        {
            CYLog.Debug("[UnityStorageAdapter] 初始化完成");
        }
        
        public string GetString(string key, string defaultValue = "")
        {
            return PlayerPrefs.GetString(key, defaultValue);
        }
        
        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
        }
        
        public int GetInt(string key, int defaultValue = 0)
        {
            return PlayerPrefs.GetInt(key, defaultValue);
        }
        
        public void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
        }
        
        public float GetFloat(string key, float defaultValue = 0f)
        {
            return PlayerPrefs.GetFloat(key, defaultValue);
        }
        
        public void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
        }
        
        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }
        
        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }
        
        public void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
        }
        
        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
