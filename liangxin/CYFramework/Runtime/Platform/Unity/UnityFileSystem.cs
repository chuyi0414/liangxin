// ============================================================================
// CYFramework 2.2 - Unity 文件系统适配器
// 适用平台：PC / Android / iOS（不支持 WebGL/微信小游戏）
// ============================================================================

// WebGL/微信小游戏不支持 System.IO.File，排除此适配器
#if !UNITY_WEBGL && !CY_WECHAT

using System.IO;
using System.Threading.Tasks;
using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Platform.Unity
{
    /// <summary>
    /// Unity 平台文件系统实现
    /// </summary>
    public class UnityFileSystem : IFileSystem
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
        /// 持久化数据路径
        /// </summary>
        public string PersistentDataPath => Application.persistentDataPath;
        
        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            CYLog.Debug($"[UnityFileSystem] 初始化完成，数据路径: {PersistentDataPath}");
        }
        
        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        public bool FileExists(string path)
        {
            // 完整路径
            string fullPath = GetFullPath(path);
            return File.Exists(fullPath);
        }
        
        /// <summary>
        /// 读取文本文件
        /// </summary>
        public string ReadText(string path)
        {
            // 完整路径
            string fullPath = GetFullPath(path);
            
            if (!File.Exists(fullPath))
            {
                CYLog.Warning($"[UnityFileSystem] 文件不存在: {fullPath}");
                return null;
            }
            
            return File.ReadAllText(fullPath);
        }
        
        /// <summary>
        /// 写入文本文件
        /// </summary>
        public void WriteText(string path, string content)
        {
            // 完整路径
            string fullPath = GetFullPath(path);
            EnsureDirectory(fullPath);
            File.WriteAllText(fullPath, content);
        }
        
        /// <summary>
        /// 读取二进制文件
        /// </summary>
        public byte[] ReadBytes(string path)
        {
            // 完整路径
            string fullPath = GetFullPath(path);
            
            if (!File.Exists(fullPath))
            {
                CYLog.Warning($"[UnityFileSystem] 文件不存在: {fullPath}");
                return null;
            }
            
            return File.ReadAllBytes(fullPath);
        }
        
        /// <summary>
        /// 写入二进制文件
        /// </summary>
        public void WriteBytes(string path, byte[] data)
        {
            // 完整路径
            string fullPath = GetFullPath(path);
            EnsureDirectory(fullPath);
            File.WriteAllBytes(fullPath, data);
        }
        
        /// <summary>
        /// 删除文件
        /// </summary>
        public void DeleteFile(string path)
        {
            // 完整路径
            string fullPath = GetFullPath(path);
            
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        
        /// <summary>
        /// 异步读取二进制文件
        /// </summary>
        public async Task<byte[]> ReadBytesAsync(string path)
        {
            // 完整路径
            string fullPath = GetFullPath(path);
            
            if (!File.Exists(fullPath))
            {
                return null;
            }
            
            // 此文件不在 WebGL 上编译，可以使用真异步
            // 文件流
            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            {
                // 读取缓冲区
                byte[] buffer = new byte[stream.Length];
                await stream.ReadAsync(buffer, 0, buffer.Length);
                return buffer;
            }
        }
        
        /// <summary>
        /// 异步写入二进制文件
        /// </summary>
        public async Task WriteBytesAsync(string path, byte[] data)
        {
            // 完整路径
            string fullPath = GetFullPath(path);
            EnsureDirectory(fullPath);
            
            // 此文件不在 WebGL 上编译，可以使用真异步
            // 文件流
            using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await stream.WriteAsync(data, 0, data.Length);
            }
        }
        
        /// <summary>
        /// 获取完整路径
        /// </summary>
        private string GetFullPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }
            return Path.Combine(PersistentDataPath, path);
        }
        
        /// <summary>
        /// 确保目录存在
        /// </summary>
        private void EnsureDirectory(string filePath)
        {
            // 目录路径
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}

#endif // !UNITY_WEBGL && !CY_WECHAT
