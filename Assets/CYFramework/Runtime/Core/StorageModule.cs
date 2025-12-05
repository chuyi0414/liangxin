// ============================================================================
// CYFramework - 存储模块
// 封装本地存储功能，提供统一的数据持久化接口
// ============================================================================

using UnityEngine;
using CYFramework.Runtime.Platform;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 存储模块
    /// 提供本地数据持久化功能
    /// </summary>
    public class StorageModule : IModule
    {
        // ====================================================================
        // IModule 实现
        // ====================================================================

        public int Priority => 20; // 存储模块优先级中等
        public bool NeedUpdate => false;

        public void Initialize()
        {
            // 根据平台自动选择存储实现
#if WECHAT_MINIGAME
            _storage = new WeChatLocalStorage();
            Log.I("StorageModule", "初始化完成（微信小游戏存储）");
#else
            _storage = new UnityLocalStorage();
            Log.I("StorageModule", "初始化完成（Unity PlayerPrefs）");
#endif
        }

        public void Update(float deltaTime) { }

        public void Shutdown()
        {
            // 关闭前保存数据
            Save();
            Log.I("StorageModule", "已关闭");
        }

        // ====================================================================
        // 配置
        // ====================================================================

        /// <summary>
        /// 键名前缀（用于区分不同项目）
        /// </summary>
        public string KeyPrefix { get; set; } = "";

        /// <summary>
        /// 是否自动保存（每次 Set 后自动调用 Save）
        /// </summary>
        public bool AutoSave { get; set; } = false;

        // ====================================================================
        // 存储核心
        // ====================================================================

        private ILocalStorage _storage;

        /// <summary>
        /// 获取底层存储实现
        /// </summary>
        public ILocalStorage Storage => _storage;

        /// <summary>
        /// 设置存储实现（用于平台切换）
        /// </summary>
        public void SetStorageImpl(ILocalStorage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// 获取带前缀的完整键名
        /// </summary>
        private string GetFullKey(string key)
        {
            return string.IsNullOrEmpty(KeyPrefix) ? key : KeyPrefix + key;
        }

        /// <summary>
        /// 自动保存（如果启用）
        /// </summary>
        private void TryAutoSave()
        {
            if (AutoSave) Save();
        }

        // ====================================================================
        // 便捷方法
        // ====================================================================

        public bool HasKey(string key) => _storage.HasKey(GetFullKey(key));

        public string GetString(string key, string defaultValue = "") 
            => _storage.GetString(GetFullKey(key), defaultValue);

        public void SetString(string key, string value)
        {
            _storage.SetString(GetFullKey(key), value);
            TryAutoSave();
        }

        public int GetInt(string key, int defaultValue = 0) 
            => _storage.GetInt(GetFullKey(key), defaultValue);

        public void SetInt(string key, int value)
        {
            _storage.SetInt(GetFullKey(key), value);
            TryAutoSave();
        }

        public float GetFloat(string key, float defaultValue = 0f) 
            => _storage.GetFloat(GetFullKey(key), defaultValue);

        public void SetFloat(string key, float value)
        {
            _storage.SetFloat(GetFullKey(key), value);
            TryAutoSave();
        }

        public void DeleteKey(string key) 
            => _storage.DeleteKey(GetFullKey(key));

        public void DeleteAll() 
            => _storage.DeleteAll();

        public void Save() 
            => _storage.Save();

        /// <summary>
        /// 获取对象（JSON 反序列化）
        /// </summary>
        public T GetObject<T>(string key, T defaultValue = default) 
            => _storage.GetObject(key, defaultValue);

        /// <summary>
        /// 设置对象（JSON 序列化）
        /// </summary>
        public void SetObject<T>(string key, T value) 
            => _storage.SetObject(key, value);
    }
}
