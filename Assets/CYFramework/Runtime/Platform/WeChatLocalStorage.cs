// ============================================================================
// CYFramework - 微信小游戏本地存储实现
// 适配微信小游戏的 wx.setStorageSync / wx.getStorageSync 等 API
// 需要导入微信小游戏 Unity SDK（minigame-unity-webgl-transform）
// ============================================================================

#if WECHAT_MINIGAME
using System;
using UnityEngine;
using WeChatWASM;

namespace CYFramework.Runtime.Platform
{
    /// <summary>
    /// 微信小游戏平台的本地存储实现
    /// 使用微信小游戏 SDK 的同步存储 API
    /// </summary>
    public class WeChatLocalStorage : ILocalStorage
    {
        // 用于标记某个 key 是否存在的特殊前缀
        private const string ExistMarker = "__CYFW_EXISTS__";

        // ====================================================================
        // 基础操作
        // ====================================================================

        /// <summary>
        /// 检查是否存在指定键
        /// </summary>
        public bool HasKey(string key)
        {
            try
            {
                // 微信小游戏没有直接的 HasKey API
                // 通过尝试获取值来判断，如果返回 null 或空则认为不存在
                string value = WX.StorageGetStringSync(key, null);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取字符串值
        /// </summary>
        public string GetString(string key, string defaultValue = "")
        {
            try
            {
                string value = WX.StorageGetStringSync(key, null);
                return value ?? defaultValue;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WeChatStorage] GetString 失败: {key}, {e.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// 设置字符串值
        /// </summary>
        public void SetString(string key, string value)
        {
            try
            {
                WX.StorageSetStringSync(key, value ?? "");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WeChatStorage] SetString 失败: {key}, {e.Message}");
            }
        }

        /// <summary>
        /// 获取整数值
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            try
            {
                // 微信小游戏存储的都是字符串，需要自行转换
                string value = WX.StorageGetStringSync(key, null);
                if (value != null && int.TryParse(value, out int result))
                {
                    return result;
                }
                return defaultValue;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WeChatStorage] GetInt 失败: {key}, {e.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// 设置整数值
        /// </summary>
        public void SetInt(string key, int value)
        {
            try
            {
                WX.StorageSetStringSync(key, value.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[WeChatStorage] SetInt 失败: {key}, {e.Message}");
            }
        }

        /// <summary>
        /// 获取浮点值
        /// </summary>
        public float GetFloat(string key, float defaultValue = 0f)
        {
            try
            {
                string value = WX.StorageGetStringSync(key, null);
                if (value != null && float.TryParse(value, out float result))
                {
                    return result;
                }
                return defaultValue;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WeChatStorage] GetFloat 失败: {key}, {e.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// 设置浮点值
        /// </summary>
        public void SetFloat(string key, float value)
        {
            try
            {
                WX.StorageSetStringSync(key, value.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[WeChatStorage] SetFloat 失败: {key}, {e.Message}");
            }
        }

        /// <summary>
        /// 删除指定键
        /// </summary>
        public void DeleteKey(string key)
        {
            try
            {
                WX.StorageDeleteKeySync(key);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WeChatStorage] DeleteKey 失败: {key}, {e.Message}");
            }
        }

        /// <summary>
        /// 删除所有数据
        /// 注意：这会清除所有小游戏存储数据，谨慎使用
        /// </summary>
        public void DeleteAll()
        {
            try
            {
                WX.StorageClearSync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[WeChatStorage] DeleteAll 失败: {e.Message}");
            }
        }

        /// <summary>
        /// 保存数据到磁盘
        /// 微信小游戏的 Sync 方法会自动持久化，此处为空实现
        /// </summary>
        public void Save()
        {
            // 微信小游戏的同步存储 API 会自动持久化，无需额外调用
        }
    }
}
#endif
