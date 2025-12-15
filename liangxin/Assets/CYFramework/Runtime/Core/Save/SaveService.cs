// ============================================================================
// CYFramework 2.2 - 存档服务
// 文档位置：3.1.4 存档系统 (Save System)
// 功能：跨平台存储、版本迁移、AES 加密、校验和
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CYFramework.Core.Config;
using CYFramework.Infrastructure;
using CYFramework.Platform;
using UnityEngine;

namespace CYFramework.Core.Save
{
    /// <summary>
    /// 存档版本迁移接口
    /// </summary>
    public interface IMigration
    {
        /// <summary>
        /// 源版本
        /// </summary>
        int FromVersion { get; }
        
        /// <summary>
        /// 目标版本
        /// </summary>
        int ToVersion { get; }
        
        /// <summary>
        /// 执行迁移
        /// </summary>
        string Migrate(string json);
    }
    
    /// <summary>
    /// 存档数据基类
    /// </summary>
    [Serializable]
    public abstract class SaveDataBase
    {
        /// <summary>
        /// 存档版本
        /// </summary>
        public int Version = 1;
        
        /// <summary>
        /// 保存时间戳
        /// </summary>
        public long SaveTimestamp;
        
        /// <summary>
        /// 校验和（防篡改）
        /// </summary>
        /// <remarks>
        /// 重要：该字段必须参与 JsonUtility 序列化，否则校验和永远无法落盘，导致加载时持续校验失败。
        /// 同时为了避免“校验和字段自引用”导致计算不稳定，框架会在计算时将其置为固定占位值（空字符串）。
        /// </remarks>
        [HideInInspector]
        public string Checksum = "";
    }
    
    /// <summary>
    /// 存档配置
    /// </summary>
    [Serializable]
    public class SaveConfig
    {
        /// <summary>
        /// 是否启用加密
        /// 注意：WebGL/微信平台需要验证 AES 可用性
        /// </summary>
        public bool EnableEncryption = true;
        
        /// <summary>
        /// 加密密钥（16 字节）
        /// </summary>
        public string EncryptionKey = "CYFramework2024!";
        
        /// <summary>
        /// 是否启用校验和
        /// </summary>
        public bool EnableChecksum = true;
        
        /// <summary>
        /// 是否启用备份（仅 Native 平台有效）
        /// </summary>
        public bool EnableBackup = true;
        
        /// <summary>
        /// 最大备份数量
        /// </summary>
        public int MaxBackupCount = 3;
    }
    
    /// <summary>
    /// 存档服务
    /// </summary>
    public class SaveService : IInitializable, IUpdateable, IDisposableEx
    {
        private SaveConfig _config;
        private IFileSystem _fileSystem;
        private IStorageAdapter _storage;
        
        // 是否使用 Storage 模式（微信/WebGL）
        private bool _useStorageMode;
        
        // 迁移器链
        private readonly List<IMigration> _migrations = new();
        
        // 当前存档版本
        private int _currentVersion = 1;

        // 默认存档键（来自 SaveServiceConfig.SaveFileName）
        private string _defaultSaveKey = "save.dat";

        // 最大存档槽位数量（来自 SaveServiceConfig.MaxSaveSlots）
        private int _maxSaveSlots = 3;

        // 自动存档间隔（秒，来自 SaveServiceConfig.AutoSaveInterval；0 表示禁用）
        private float _autoSaveInterval;
        private float _autoSaveTimer;

        // Storage 模式是否有待落盘的数据（避免每次 SetString 都立刻 Save）
        private bool _storageDirty;
        
        // 缓存的存档数据
        private readonly Dictionary<string, object> _cache = new();

        // 脏标记集合：
        // SaveDataBase 是引用类型，框架无法自动感知字段变更；业务在修改后应显式调用 MarkDirty(key)。
        // 说明：使用 Dictionary<string, byte> 作为“集合”，在一些旧环境/IL2CPP 下更稳，并且便于调试。
        private readonly Dictionary<string, byte> _dirtyKeys = new(32);
        private readonly List<string> _dirtyKeyBuffer = new(32);
        
        public int InitOrder => 20;
        public int UpdateOrder => 20;
        public int DisposeOrder => 20;
        
        public SaveService(SaveConfig config = null)
        {
            _config = config ?? new SaveConfig();
        }
        
        #region 生命周期
        
        public void Initialize()
        {
            // 从 CYConfigurator 读取配置
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                var externalConfig = configurator.GetConfig<SaveServiceConfig>();
                if (externalConfig != null)
                {
                    _config.EnableEncryption = externalConfig.EnableEncryption;
                    _config.EncryptionKey = externalConfig.EncryptionKey;
                    _config.EnableChecksum = externalConfig.EnableChecksum;
                    _config.EnableBackup = externalConfig.EnableBackup;
                    _config.MaxBackupCount = Mathf.Max(0, externalConfig.MaxBackupCount);
                    _defaultSaveKey = string.IsNullOrEmpty(externalConfig.SaveFileName) ? _defaultSaveKey : externalConfig.SaveFileName;
                    _maxSaveSlots = Mathf.Max(1, externalConfig.MaxSaveSlots);
                    _autoSaveInterval = Mathf.Max(0f, externalConfig.AutoSaveInterval);
                    _currentVersion = externalConfig.SaveVersion;
                    CYLog.Debug("[SaveService] 使用 CYConfigurator 配置");
                }
            }
            
            // 获取平台适配器
            if (ServiceLocator.TryGet<IFileSystem>(out var fs))
            {
                _fileSystem = fs;
            }
            
            if (ServiceLocator.TryGet<IStorageAdapter>(out var storage))
            {
                _storage = storage;
            }
            
            // 微信/WebGL 使用 Storage 模式（不依赖文件系统）
            #if CY_WECHAT || UNITY_WEBGL
            _useStorageMode = true;
            CYLog.Debug("[SaveService] 初始化完成 (Storage 模式 - 微信/WebGL)");
            #else
            _useStorageMode = _fileSystem == null;
            CYLog.Debug($"[SaveService] 初始化完成 ({(_useStorageMode ? "Storage" : "File")} 模式)");
            #endif
        }

        public void OnUpdate(float deltaTime)
        {
            // 自动存档：仅在有脏数据时触发，避免无意义写入。
            // 注意：这里使用 unscaled 时间，避免 TimeScale=0 时累积为 0 导致永不触发。
            if (_autoSaveInterval <= 0f) return;
            if (_dirtyKeys.Count <= 0) return;

            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer < _autoSaveInterval) return;

            _autoSaveTimer = 0f;
            SaveAllDirty();
        }
        
        public void Dispose()
        {
            // 保存所有缓存的存档
            SaveAllDirty();
            _cache.Clear();
            _dirtyKeys.Clear();
            CYLog.Debug("[SaveService] 已销毁");
        }
        
        #endregion
        
        #region 公开 API

        /// <summary>
        /// 默认存档 Key（来自 <see cref="SaveServiceConfig.SaveFileName"/>）。
        /// </summary>
        public string DefaultSaveKey => _defaultSaveKey;

        /// <summary>
        /// 最大存档槽位数（来自 <see cref="SaveServiceConfig.MaxSaveSlots"/>）。
        /// </summary>
        public int MaxSaveSlots => _maxSaveSlots;
        
        /// <summary>
        /// 注册版本迁移器
        /// </summary>
        public void RegisterMigration(IMigration migration)
        {
            _migrations.Add(migration);
            _migrations.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
        }
        
        /// <summary>
        /// 设置当前存档版本
        /// </summary>
        public void SetCurrentVersion(int version)
        {
            _currentVersion = version;
        }
        
        /// <summary>
        /// 保存数据
        /// </summary>
        public bool Save<T>(string key, T data) where T : SaveDataBase
        {
            key = NormalizeKey(key);
            if (data == null)
            {
                CYLog.Warning("[SaveService] Save 失败：data 为空");
                return false;
            }

            try
            {
                // 设置元数据
                data.Version = _currentVersion;
                data.SaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                
                // 序列化
                // 注意：校验和字段会被序列化，为避免“校验和参与自身计算”导致不稳定，先写入固定占位值。
                string json;

                // 计算校验和
                if (_config.EnableChecksum)
                {
                    data.Checksum = "";
                    var jsonForChecksum = JsonUtility.ToJson(data);
                    data.Checksum = ComputeChecksum(jsonForChecksum);
                    json = JsonUtility.ToJson(data);
                }
                else
                {
                    // 关闭校验时，清空字段，避免残留旧值被写回。
                    data.Checksum = "";
                    json = JsonUtility.ToJson(data);
                }
                
                // 加密
                string finalData = _config.EnableEncryption ? Encrypt(json) : json;
                
                // 根据平台选择存储方式
                if (_useStorageMode)
                {
                    // 微信/WebGL: 使用 Storage API
                    _storage?.SetString(GetStorageKey(key), finalData);
                    _storageDirty = true;
                    FlushStorageIfNeeded();
                }
                else
                {
                    // Native: 使用文件系统
                    // 备份旧存档
                    if (_config.EnableBackup)
                    {
                        CreateBackup(key);
                    }
                    
                    string path = GetSavePath(key);
                    _fileSystem?.WriteText(path, finalData);
                }
                
                // 更新缓存
                _cache[key] = data;
                _dirtyKeys.Remove(key);
                
                CYLog.Debug($"[SaveService] 保存成功: {key}");
                return true;
            }
            catch (Exception ex)
            {
                CYLog.Error($"[SaveService] 保存失败: {key}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// 加载数据
        /// </summary>
        public T Load<T>(string key) where T : SaveDataBase, new()
        {
            key = NormalizeKey(key);

            // 检查缓存
            if (_cache.TryGetValue(key, out var cached))
            {
                return (T)cached;
            }
            
            try
            {
                string content;
                
                if (_useStorageMode)
                {
                    // 微信/WebGL: 从 Storage 读取
                    content = _storage?.GetString(GetStorageKey(key), null);
                    if (string.IsNullOrEmpty(content))
                    {
                        CYLog.Debug($"[SaveService] 存档不存在，返回新数据: {key}");
                        return new T();
                    }
                }
                else
                {
                    // Native: 从文件读取
                    string path = GetSavePath(key);
                    
                    if (_fileSystem == null || !_fileSystem.FileExists(path))
                    {
                        CYLog.Debug($"[SaveService] 存档不存在，返回新数据: {key}");
                        return new T();
                    }
                    
                    content = _fileSystem.ReadText(path);
                }
                
                // 解密
                string json = _config.EnableEncryption ? Decrypt(content) : content;
                
                // 反序列化
                var data = JsonUtility.FromJson<T>(json);
                
                // 校验和验证
                if (_config.EnableChecksum)
                {
                    var expectedChecksum = data.Checksum;

                    // 与保存端一致：将 Checksum 置为固定占位值后计算。
                    data.Checksum = "";
                    var actualChecksum = ComputeChecksum(JsonUtility.ToJson(data));

                    // 还原，便于调试（并保持数据对象字段语义正确）。
                    data.Checksum = expectedChecksum;
                    
                    if (string.IsNullOrEmpty(expectedChecksum) || expectedChecksum != actualChecksum)
                    {
                        CYLog.Warning($"[SaveService] 校验和不匹配，存档可能被篡改: {key}");
                        // 尝试从备份恢复
                        var backup = LoadFromBackup<T>(key);
                        if (backup != null) return backup;
                    }
                }
                
                // 版本迁移
                if (data.Version < _currentVersion)
                {
                    json = MigrateData(json, data.Version);
                    data = JsonUtility.FromJson<T>(json);
                    data.Version = _currentVersion;
                    
                    // 保存迁移后的数据
                    Save(key, data);
                }
                
                // 更新缓存
                _cache[key] = data;
                _dirtyKeys.Remove(key);
                
                CYLog.Debug($"[SaveService] 加载成功: {key}");
                return data;
            }
            catch (Exception ex)
            {
                CYLog.Error($"[SaveService] 加载失败: {key}", ex);
                
                // 尝试从备份恢复
                var backup = LoadFromBackup<T>(key);
                return backup ?? new T();
            }
        }
        
        /// <summary>
        /// 删除存档
        /// </summary>
        public void Delete(string key)
        {
            key = NormalizeKey(key);

            try
            {
                if (_useStorageMode)
                {
                    _storage?.DeleteKey(GetStorageKey(key));
                    _storageDirty = true;
                    FlushStorageIfNeeded();
                }
                else
                {
                    string path = GetSavePath(key);
                    _fileSystem?.DeleteFile(path);
                }

                _cache.Remove(key);
                _dirtyKeys.Remove(key);
                
                CYLog.Debug($"[SaveService] 删除成功: {key}");
            }
            catch (Exception ex)
            {
                CYLog.Error($"[SaveService] 删除失败: {key}", ex);
            }
        }
        
        /// <summary>
        /// 检查存档是否存在
        /// </summary>
        public bool Exists(string key)
        {
            key = NormalizeKey(key);
            if (_cache.ContainsKey(key)) return true;
            
            if (_useStorageMode)
            {
                return _storage?.HasKey(GetStorageKey(key)) ?? false;
            }
            else
            {
                string path = GetSavePath(key);
                return _fileSystem?.FileExists(path) ?? false;
            }
        }
        
        /// <summary>
        /// 保存默认存档（使用 <see cref="DefaultSaveKey"/>）。
        /// </summary>
        public bool Save<T>(T data) where T : SaveDataBase
        {
            return Save(_defaultSaveKey, data);
        }

        /// <summary>
        /// 加载默认存档（使用 <see cref="DefaultSaveKey"/>）。
        /// </summary>
        public T Load<T>() where T : SaveDataBase, new()
        {
            return Load<T>(_defaultSaveKey);
        }

        /// <summary>
        /// 删除默认存档（使用 <see cref="DefaultSaveKey"/>）。
        /// </summary>
        public void Delete()
        {
            Delete(_defaultSaveKey);
        }

        /// <summary>
        /// 默认存档是否存在（使用 <see cref="DefaultSaveKey"/>）。
        /// </summary>
        public bool Exists()
        {
            return Exists(_defaultSaveKey);
        }

        /// <summary>
        /// 保存指定槽位（0~MaxSaveSlots-1）。
        /// </summary>
        public bool SaveSlot<T>(int slotIndex, T data) where T : SaveDataBase
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                CYLog.Warning($"[SaveService] SaveSlot 失败：slotIndex 超界: {slotIndex}");
                return false;
            }

            return Save(BuildSlotKey(slotIndex), data);
        }

        /// <summary>
        /// 加载指定槽位（0~MaxSaveSlots-1）。
        /// </summary>
        public T LoadSlot<T>(int slotIndex) where T : SaveDataBase, new()
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                CYLog.Warning($"[SaveService] LoadSlot 失败：slotIndex 超界: {slotIndex}");
                return new T();
            }

            return Load<T>(BuildSlotKey(slotIndex));
        }

        /// <summary>
        /// 指定槽位是否存在（0~MaxSaveSlots-1）。
        /// </summary>
        public bool ExistsSlot(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                return false;
            }

            return Exists(BuildSlotKey(slotIndex));
        }

        /// <summary>
        /// 删除指定槽位（0~MaxSaveSlots-1）。
        /// </summary>
        public void DeleteSlot(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                CYLog.Warning($"[SaveService] DeleteSlot 失败：slotIndex 超界: {slotIndex}");
                return;
            }

            Delete(BuildSlotKey(slotIndex));
        }
        
        /// <summary>
        /// 保存所有缓存的存档（不要求先 MarkDirty）。
        /// </summary>
        public void SaveAll()
        {
            if (_cache.Count <= 0) return;

            _dirtyKeyBuffer.Clear();
            foreach (var kv in _cache)
            {
                _dirtyKeyBuffer.Add(kv.Key);
            }

            for (int i = 0; i < _dirtyKeyBuffer.Count; i++)
            {
                var key = _dirtyKeyBuffer[i];
                if (_cache.TryGetValue(key, out var cached) && cached is SaveDataBase saveData)
                {
                    Save(key, saveData);
                }
            }

            _dirtyKeyBuffer.Clear();
        }

        /// <summary>
        /// 尝试加载存档：只有当存档真实存在时才返回 true；不存在则返回 false（不会创建新对象）。
        /// </summary>
        public bool TryLoad<T>(string key, out T data) where T : SaveDataBase, new()
        {
            if (!Exists(key))
            {
                data = null;
                return false;
            }

            data = Load<T>(key);
            return data != null;
        }

        /// <summary>
        /// 加载或创建：存档存在就加载，否则创建并缓存，但不会自动保存（是否保存由业务决定）。
        /// </summary>
        /// <remarks>
        /// 高频用法：进入游戏时拿到一份可用数据对象；数据修改后调用 MarkDirty，再在合适时机 SaveAllDirty。
        /// </remarks>
        public T LoadOrCreate<T>(string key) where T : SaveDataBase, new()
        {
            key = NormalizeKey(key);
            if (Exists(key))
            {
                return Load<T>(key);
            }

            var created = new T();
            _cache[key] = created;
            _dirtyKeys[key] = 1;
            return created;
        }

        /// <summary>
        /// 标记某个存档为脏：业务层修改数据后应调用，框架才能在 SaveAllDirty 时正确保存。
        /// </summary>
        public void MarkDirty(string key)
        {
            key = NormalizeKey(key);
            _dirtyKeys[key] = 1;
        }

        /// <summary>
        /// 保存某个已缓存且被标记为脏的数据；成功后会清除脏标记。
        /// </summary>
        public bool SaveDirty(string key)
        {
            key = NormalizeKey(key);
            if (!_dirtyKeys.ContainsKey(key)) return false;
            if (!_cache.TryGetValue(key, out var cached)) return false;

            if (cached is SaveDataBase saveData)
            {
                // Save<T> 的数据类型约束更强，这里用基类调用即可覆盖 99% 用法。
                var ok = Save(key, saveData);
                if (ok) _dirtyKeys.Remove(key);
                return ok;
            }

            return false;
        }

        /// <summary>
        /// 保存所有脏数据（只保存被 MarkDirty 的键）。
        /// </summary>
        /// <remarks>
        /// - 内部使用复用缓冲列表，避免遍历过程中修改集合导致异常。
        /// - 推荐在流程切换、退出、应用切后台等低频时机调用；不要在 Update 高频调用。
        /// </remarks>
        public void SaveAllDirty()
        {
            if (_dirtyKeys.Count <= 0) return;

            _dirtyKeyBuffer.Clear();
            foreach (var kv in _dirtyKeys)
            {
                _dirtyKeyBuffer.Add(kv.Key);
            }

            for (int i = 0; i < _dirtyKeyBuffer.Count; i++)
            {
                SaveDirty(_dirtyKeyBuffer[i]);
            }

            _dirtyKeyBuffer.Clear();
            FlushStorageIfNeeded();
        }

        /// <summary>
        /// 清理某个键的缓存（不会删除磁盘/Storage 的存档）。
        /// </summary>
        public void ClearCache(string key)
        {
            key = NormalizeKey(key);
            _cache.Remove(key);
            _dirtyKeys.Remove(key);
        }

        /// <summary>
        /// 清理全部缓存（不会删除磁盘/Storage 的存档）。
        /// </summary>
        public void ClearAllCache()
        {
            _cache.Clear();
            _dirtyKeys.Clear();
            _dirtyKeyBuffer.Clear();
        }
        
        #endregion
        
        #region 私有方法

        /// <summary>
        /// 规范化 Key：为空则使用默认 Key。
        /// </summary>
        private string NormalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return _defaultSaveKey;
            }

            return key.Trim();
        }

        /// <summary>
        /// slotIndex 是否有效（0~MaxSaveSlots-1）。
        /// </summary>
        private bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < _maxSaveSlots;
        }

        /// <summary>
        /// 构建槽位 Key：默认规则为 “{DefaultSaveKeyWithoutExt}_slot{index}{ext}”。
        /// </summary>
        private string BuildSlotKey(int slotIndex)
        {
            // DefaultSaveKey 允许包含目录与扩展名
            var baseKey = NormalizeKey(null);
            var extension = Path.GetExtension(baseKey);
            var nameNoExt = Path.GetFileNameWithoutExtension(baseKey);
            var dir = Path.GetDirectoryName(baseKey);

            var fileName = $"{nameNoExt}_slot{slotIndex}{extension}";
            if (string.IsNullOrEmpty(dir))
            {
                return fileName;
            }

            // 统一分隔符，避免 Storage key/路径不一致
            dir = dir.Replace('\\', '/');
            return $"{dir}/{fileName}";
        }

        /// <summary>
        /// Storage 模式落盘（WebGL/微信）。
        /// </summary>
        private void FlushStorageIfNeeded()
        {
            if (!_useStorageMode) return;
            if (!_storageDirty) return;

            _storageDirty = false;
            _storage?.Save();
        }
        
        /// <summary>
        /// 获取存档路径（文件系统模式）
        /// </summary>
        private string GetSavePath(string key)
        {
            key = NormalizeKey(key);

            // 允许传入带扩展名的文件名（例如 "save.dat"），此时不强制追加 .sav
            var fileName = key;
            if (Path.IsPathRooted(fileName))
            {
                return fileName;
            }

            fileName = fileName.TrimStart('/', '\\');
            if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
            {
                fileName += ".sav";
            }

            return $"Saves/{fileName}";
        }
        
        /// <summary>
        /// 获取存储键（Storage 模式 - 微信/WebGL）
        /// </summary>
        private string GetStorageKey(string key)
        {
            key = NormalizeKey(key);
            return $"CYF_Save_{key}";
        }
        
        /// <summary>
        /// 获取备份路径
        /// </summary>
        private string GetBackupPath(string key, int index)
        {
            return $"Saves/Backup/{key}_{index}.bak";
        }
        
        /// <summary>
        /// 创建备份
        /// </summary>
        private void CreateBackup(string key)
        {
            try
            {
                if (_config.MaxBackupCount <= 0) return;

                string sourcePath = GetSavePath(key);
                
                if (_fileSystem == null || !_fileSystem.FileExists(sourcePath)) return;
                
                // 移动旧备份
                for (int i = _config.MaxBackupCount - 1; i > 0; i--)
                {
                    string oldPath = GetBackupPath(key, i - 1);
                    string newPath = GetBackupPath(key, i);
                    
                    if (_fileSystem.FileExists(oldPath))
                    {
                        string content = _fileSystem.ReadText(oldPath);
                        _fileSystem.WriteText(newPath, content);
                    }
                }
                
                // 创建新备份
                string data = _fileSystem.ReadText(sourcePath);
                _fileSystem.WriteText(GetBackupPath(key, 0), data);
            }
            catch (Exception ex)
            {
                // 注意：CYLog.Warning 的第二个参数是 tag，不是异常；这里把异常信息写入 message，避免日志丢失。
                CYLog.Warning($"[SaveService] 创建备份失败: {key}, ex={ex.Message}");
            }
        }
        
        /// <summary>
        /// 从备份恢复
        /// </summary>
        private T LoadFromBackup<T>(string key) where T : SaveDataBase
        {
            for (int i = 0; i < _config.MaxBackupCount; i++)
            {
                try
                {
                    string backupPath = GetBackupPath(key, i);
                    
                    if (_fileSystem != null && _fileSystem.FileExists(backupPath))
                    {
                        string content = _fileSystem.ReadText(backupPath);
                        string json = _config.EnableEncryption ? Decrypt(content) : content;
                        var data = JsonUtility.FromJson<T>(json);
                        
                        CYLog.Info($"[SaveService] 从备份 #{i} 恢复成功: {key}");
                        return data;
                    }
                }
                catch
                {
                    // 继续尝试下一个备份
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 版本迁移
        /// </summary>
        private string MigrateData(string json, int fromVersion)
        {
            string current = json;
            int version = fromVersion;
            
            while (version < _currentVersion)
            {
                var migration = _migrations.Find(m => m.FromVersion == version);
                
                if (migration == null)
                {
                    CYLog.Warning($"[SaveService] 找不到迁移器: v{version} -> v{version + 1}");
                    version++;
                    continue;
                }
                
                CYLog.Info($"[SaveService] 执行迁移: v{version} -> v{migration.ToVersion}");
                current = migration.Migrate(current);
                version = migration.ToVersion;
            }
            
            return current;
        }
        
        /// <summary>
        /// 计算校验和
        /// </summary>
        private string ComputeChecksum(string data)
        {
            using var md5 = MD5.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            byte[] hash = md5.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
        
        /// <summary>
        /// AES 加密
        /// 使用纯 C# 实现，兼容 WebGL/微信（需要验证）
        /// 如果平台不支持，会回退到明文存储
        /// </summary>
        private string Encrypt(string plainText)
        {
#if CY_WECHAT || UNITY_WEBGL
            // WebGL/微信平台：尝试加密，失败则回退到明文
            try
            {
                return EncryptInternal(plainText);
            }
            catch (Exception ex)
            {
                CYLog.Warning($"[SaveService] WebGL/微信平台加密失败，回退到明文: {ex.Message}");
                return plainText;
            }
#else
            return EncryptInternal(plainText);
#endif
        }
        
        /// <summary>
        /// AES 加密内部实现
        /// </summary>
        private string EncryptInternal(string plainText)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(_config.EncryptionKey.PadRight(16).Substring(0, 16));
                aes.IV = new byte[16];
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                
                using var encryptor = aes.CreateEncryptor();
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                CYLog.Error("[SaveService] 加密失败", ex);
                return plainText;
            }
        }
        
        /// <summary>
        /// AES 解密
        /// WebGL/微信平台如果之前回退到明文，这里也会处理
        /// </summary>
        private string Decrypt(string cipherText)
        {
#if CY_WECHAT || UNITY_WEBGL
            // WebGL/微信平台：尝试解密，失败则假定是明文
            try
            {
                return DecryptInternal(cipherText);
            }
            catch
            {
                // 可能是明文存储，直接返回
                return cipherText;
            }
#else
            return DecryptInternal(cipherText);
#endif
        }
        
        /// <summary>
        /// AES 解密内部实现
        /// </summary>
        private string DecryptInternal(string cipherText)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(_config.EncryptionKey.PadRight(16).Substring(0, 16));
                aes.IV = new byte[16];
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                
                using var decryptor = aes.CreateDecryptor();
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                CYLog.Error("[SaveService] 解密失败", ex);
                return cipherText;
            }
        }
        
        #endregion
    }
}
