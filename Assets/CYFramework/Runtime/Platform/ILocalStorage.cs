// ============================================================================
// CYFramework - 本地存储接口
// 平台抽象层：封装本地数据存储的平台差异
// 
// 说明：
// - Unity 平台使用 PlayerPrefs
// - 微信小游戏可以实现对应的 Storage API
// ============================================================================

using UnityEngine;

namespace CYFramework.Runtime.Platform
{
    /// <summary>
    /// 本地存储接口
    /// 用于隔离不同平台的本地存储 API 差异
    /// </summary>
    public interface ILocalStorage
    {
        /// <summary>
        /// 检查是否存在指定键
        /// </summary>
        bool HasKey(string key);

        /// <summary>
        /// 获取字符串值
        /// </summary>
        string GetString(string key, string defaultValue = "");

        /// <summary>
        /// 设置字符串值
        /// </summary>
        void SetString(string key, string value);

        /// <summary>
        /// 获取整数值
        /// </summary>
        int GetInt(string key, int defaultValue = 0);

        /// <summary>
        /// 设置整数值
        /// </summary>
        void SetInt(string key, int value);

        /// <summary>
        /// 获取浮点值
        /// </summary>
        float GetFloat(string key, float defaultValue = 0f);

        /// <summary>
        /// 设置浮点值
        /// </summary>
        void SetFloat(string key, float value);

        /// <summary>
        /// 删除指定键
        /// </summary>
        void DeleteKey(string key);

        /// <summary>
        /// 删除所有数据
        /// </summary>
        void DeleteAll();

        /// <summary>
        /// 保存数据到磁盘
        /// </summary>
        void Save();
    }

    /// <summary>
    /// Unity 平台的本地存储实现（基于 PlayerPrefs）
    /// </summary>
    public class UnityLocalStorage : ILocalStorage
    {
        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
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

    /// <summary>
    /// 本地存储扩展方法
    /// 提供 JSON 序列化支持
    /// </summary>
    public static class LocalStorageExtensions
    {
        /// <summary>
        /// 获取对象（JSON 反序列化）
        /// </summary>
        public static T GetObject<T>(this ILocalStorage storage, string key, T defaultValue = default)
        {
            string json = storage.GetString(key, null);
            if (string.IsNullOrEmpty(json))
            {
                return defaultValue;
            }

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 设置对象（JSON 序列化）
        /// </summary>
        public static void SetObject<T>(this ILocalStorage storage, string key, T value)
        {
            string json = JsonUtility.ToJson(value);
            storage.SetString(key, json);
        }
    }
}
