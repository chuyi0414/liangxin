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
        /// <summary>
        /// 平台类型
        /// </summary>
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
        
        /// <summary>
        /// 存储上限（PlayerPrefs 无明确限制，按 50MB 估算）
        /// </summary>
        public long StorageLimit => 50 * 1024 * 1024;
        
        /// <summary>
        /// 已使用存储（PlayerPrefs 无法精确获取，返回 0）
        /// </summary>
        public long StorageUsed => 0;
        
        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            CYLog.Debug("[UnityStorageAdapter] 初始化完成");
        }
        
        /// <summary>
        /// 获取字符串
        /// </summary>
        public string GetString(string key, string defaultValue = "")
        {
            return PlayerPrefs.GetString(key, defaultValue);
        }
        
        /// <summary>
        /// 设置字符串
        /// </summary>
        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
        }
        
        /// <summary>
        /// 获取整数
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            return PlayerPrefs.GetInt(key, defaultValue);
        }
        
        /// <summary>
        /// 设置整数
        /// </summary>
        public void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
        }
        
        /// <summary>
        /// 获取浮点数
        /// </summary>
        public float GetFloat(string key, float defaultValue = 0f)
        {
            return PlayerPrefs.GetFloat(key, defaultValue);
        }
        
        /// <summary>
        /// 设置浮点数
        /// </summary>
        public void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
        }
        
        /// <summary>
        /// 检查键是否存在
        /// </summary>
        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }
        
        /// <summary>
        /// 删除键
        /// </summary>
        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }
        
        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
        }
        
        /// <summary>
        /// 保存
        /// </summary>
        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
