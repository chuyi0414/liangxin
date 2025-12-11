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
        [NonSerialized]
        public string Checksum;
    }
    
    /// <summary>
    /// 存档配置
    /// </summary>
    [Serializable]
    public class SaveConfig
    {
        /// <summary>
        /// 是否启用加密
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
        /// 是否启用备份
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
    public class SaveService : IInitializable, IDisposableEx
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
        
        // 缓存的存档数据
        private readonly Dictionary<string, object> _cache = new();
        
        public int InitOrder => 20;
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
                    _config.MaxBackupCount = externalConfig.MaxSaveSlots;
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
        
        public void Dispose()
        {
            // 保存所有缓存的存档
            SaveAll();
            _cache.Clear();
            CYLog.Debug("[SaveService] 已销毁");
        }
        
        #endregion
        
        #region 公开 API
        
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
            try
            {
                // 设置元数据
                data.Version = _currentVersion;
                data.SaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                
                // 序列化
                string json = JsonUtility.ToJson(data);
                
                // 计算校验和
                if (_config.EnableChecksum)
                {
                    data.Checksum = ComputeChecksum(json);
                    json = JsonUtility.ToJson(data);
                }
                
                // 加密
                string finalData = _config.EnableEncryption ? Encrypt(json) : json;
                
                // 根据平台选择存储方式
                if (_useStorageMode)
                {
                    // 微信/WebGL: 使用 Storage API
                    _storage?.SetString(GetStorageKey(key), finalData);
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
                    string expectedChecksum = data.Checksum;
                    data.Checksum = null;
                    string actualChecksum = ComputeChecksum(JsonUtility.ToJson(data));
                    
                    if (expectedChecksum != actualChecksum)
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
            try
            {
                string path = GetSavePath(key);
                _fileSystem?.DeleteFile(path);
                _cache.Remove(key);
                
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
        /// 保存所有缓存的存档
        /// </summary>
        public void SaveAll()
        {
            // 简化实现：实际需要追踪脏数据
            CYLog.Debug("[SaveService] SaveAll 完成");
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 获取存档路径（文件系统模式）
        /// </summary>
        private string GetSavePath(string key)
        {
            return $"Saves/{key}.sav";
        }
        
        /// <summary>
        /// 获取存储键（Storage 模式 - 微信/WebGL）
        /// </summary>
        private string GetStorageKey(string key)
        {
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
                CYLog.Warning($"[SaveService] 创建备份失败: {key}", ex.Message);
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
        /// 使用纯 C# 实现，兼容 WebGL
        /// </summary>
        private string Encrypt(string plainText)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(_config.EncryptionKey.PadRight(16).Substring(0, 16));
                aes.IV = new byte[16]; // 简化：使用零 IV
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
        /// </summary>
        private string Decrypt(string cipherText)
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
