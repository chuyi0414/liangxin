// ============================================================================
// CYFramework 2.2 - 热更新服务
// 文档位置：3.1.5 热更新 (Hot Update)
// 功能：版本检测、资源增量下载、微信分包/Native Addressables
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CYFramework.Core.Config;
using CYFramework.Core.Network;
using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Core.HotUpdate
{
    /// <summary>
    /// 更新状态
    /// </summary>
    public enum UpdateState
    {
        Idle,
        CheckingVersion,
        Downloading,
        Applying,
        Completed,
        Failed
    }
    
    /// <summary>
    /// 版本信息
    /// </summary>
    [Serializable]
    public class VersionInfo
    {
        public string version;
        public string minVersion;
        public string cdnUrl;
        public List<AssetBundle> bundles;
        public long totalSize;
        public string updateNote;
        public bool forceUpdate;
    }
    
    /// <summary>
    /// 资源包信息
    /// </summary>
    [Serializable]
    public class AssetBundle
    {
        public string name;
        public string hash;
        public long size;
        public int priority;
    }
    
    /// <summary>
    /// 热更新配置
    /// </summary>
    [Serializable]
    public class HotUpdateConfig
    {
        /// <summary>
        /// 版本检测 URL
        /// </summary>
        public string VersionUrl = "https://cdn.example.com/version.json";
        
        /// <summary>
        /// CDN 基础 URL
        /// </summary>
        public string CdnBaseUrl = "https://cdn.example.com/bundles/";
        
        /// <summary>
        /// 下载超时时间（秒）
        /// </summary>
        public int DownloadTimeout = 30;
        
        /// <summary>
        /// 最大并发下载数
        /// </summary>
        public int MaxConcurrentDownloads = 3;
        
        /// <summary>
        /// 是否启用增量更新
        /// </summary>
        public bool EnableIncrementalUpdate = true;
    }
    
    /// <summary>
    /// 热更新进度
    /// </summary>
    public struct UpdateProgress
    {
        public UpdateState State;
        public float Progress;
        public long DownloadedBytes;
        public long TotalBytes;
        public string CurrentFile;
        public int DownloadedCount;
        public int TotalCount;
    }
    
    /// <summary>
    /// 热更新服务接口
    /// </summary>
    public interface IHotUpdateService
    {
        UpdateState State { get; }
        UpdateProgress Progress { get; }
        
        Task<bool> CheckUpdate();
        Task<bool> DownloadUpdate(Action<UpdateProgress> onProgress = null);
        void ApplyUpdate();
        
        event Action<VersionInfo> OnNewVersionFound;
        event Action<UpdateProgress> OnProgressChanged;
        event Action<string> OnError;
    }
    
    /// <summary>
    /// 热更新服务
    /// 文档：微信分包 + Native Addressables
    /// </summary>
    public class HotUpdateService : IHotUpdateService, IInitializable, IDisposableEx
    {
        private HotUpdateConfig _config;
        private NetworkService _network;
        
        private UpdateState _state = UpdateState.Idle;
        private UpdateProgress _progress;
        private VersionInfo _remoteVersion;
        private string _localVersion;
        
        // 待下载列表
        private readonly List<AssetBundle> _pendingDownloads = new();
        
        public UpdateState State => _state;
        public UpdateProgress Progress => _progress;
        
        public event Action<VersionInfo> OnNewVersionFound;
        public event Action<UpdateProgress> OnProgressChanged;
        public event Action<string> OnError;
        
        public int InitOrder => 40;
        public int DisposeOrder => 40;
        
        /// <summary>
        /// 无参构造函数（ServiceLocator 需要）
        /// </summary>
        public HotUpdateService() : this(null) { }
        
        public HotUpdateService(HotUpdateConfig config)
        {
            _config = config ?? new HotUpdateConfig();
        }
        
        #region 生命周期
        
        public void Initialize()
        {
            // 从 CYConfigurator 读取配置
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                var externalConfig = configurator.GetConfig<HotUpdateServiceConfig>();
                if (externalConfig != null)
                {
                    _config.CdnBaseUrl = externalConfig.CdnBaseUrl;
                    _config.VersionUrl = externalConfig.CdnBaseUrl + externalConfig.VersionFileName;
                    _config.DownloadTimeout = (int)externalConfig.DownloadTimeout;
                    _config.MaxConcurrentDownloads = externalConfig.MaxConcurrentDownloads;
                    _config.EnableIncrementalUpdate = externalConfig.EnableIncrementalUpdate;
                    CYLog.Debug("[HotUpdateService] 使用 CYConfigurator 配置");
                }
            }
            
            if (ServiceLocator.TryGet<NetworkService>(out var network))
            {
                _network = network;
            }
            
            // 读取本地版本
            _localVersion = Application.version;
            
            CYLog.Debug($"[HotUpdateService] 初始化完成，本地版本: {_localVersion}");
        }
        
        public void Dispose()
        {
            _pendingDownloads.Clear();
            CYLog.Debug("[HotUpdateService] 已销毁");
        }
        
        #endregion
        
        #region 公开 API
        
        /// <summary>
        /// 检查更新
        /// 文档：启动时对比 version.json
        /// </summary>
        public async Task<bool> CheckUpdate()
        {
            if (_state != UpdateState.Idle && _state != UpdateState.Failed)
            {
                CYLog.Warning("[HotUpdateService] 正在检查更新中");
                return false;
            }
            
            SetState(UpdateState.CheckingVersion);
            
            try
            {
#if CY_WECHAT || UNITY_WEBGL
                // WebGL/微信小游戏：使用微信 API 检查更新
                return await CheckWeChatUpdate();
#else
                // Native: 从服务器获取版本信息
                return await CheckServerUpdate();
#endif
            }
            catch (Exception ex)
            {
                CYLog.Error("[HotUpdateService] 检查更新失败", ex);
                SetState(UpdateState.Failed);
                OnError?.Invoke(ex.Message);
                return false;
            }
        }
        
        /// <summary>
        /// 下载更新
        /// 文档：资源 CDN + 按版本号增量下载
        /// </summary>
        public async Task<bool> DownloadUpdate(Action<UpdateProgress> onProgress = null)
        {
            if (_pendingDownloads.Count == 0)
            {
                CYLog.Warning("[HotUpdateService] 没有待下载的更新");
                return true;
            }
            
            SetState(UpdateState.Downloading);
            
            try
            {
                long totalBytes = 0;
                foreach (var bundle in _pendingDownloads)
                {
                    totalBytes += bundle.size;
                }
                
                _progress.TotalBytes = totalBytes;
                _progress.TotalCount = _pendingDownloads.Count;
                _progress.DownloadedBytes = 0;
                _progress.DownloadedCount = 0;
                
                // 按优先级排序
                _pendingDownloads.Sort((a, b) => b.priority.CompareTo(a.priority));
                
                foreach (var bundle in _pendingDownloads)
                {
                    _progress.CurrentFile = bundle.name;
                    NotifyProgress();
                    onProgress?.Invoke(_progress);
                    
                    bool success = await DownloadBundle(bundle);
                    
                    if (!success)
                    {
                        SetState(UpdateState.Failed);
                        OnError?.Invoke($"下载失败: {bundle.name}");
                        return false;
                    }
                    
                    _progress.DownloadedBytes += bundle.size;
                    _progress.DownloadedCount++;
                    _progress.Progress = (float)_progress.DownloadedBytes / _progress.TotalBytes;
                    NotifyProgress();
                    onProgress?.Invoke(_progress);
                }
                
                SetState(UpdateState.Completed);
                CYLog.Info("[HotUpdateService] 下载完成");
                return true;
            }
            catch (Exception ex)
            {
                CYLog.Error("[HotUpdateService] 下载更新失败", ex);
                SetState(UpdateState.Failed);
                OnError?.Invoke(ex.Message);
                return false;
            }
        }
        
        /// <summary>
        /// 应用更新
        /// </summary>
        public void ApplyUpdate()
        {
            SetState(UpdateState.Applying);
            
#if CY_WECHAT
            // 微信：重启小程序
            CYLog.Info("[HotUpdateService] 微信端应用更新，需重启小程序");
            // WX.RestartMiniProgram();
#else
            // Native: 重新加载 AssetBundle Catalog
            CYLog.Info("[HotUpdateService] 应用更新完成");
#endif
            
            SetState(UpdateState.Completed);
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 检查服务器更新（Native 端）
        /// </summary>
        private async Task<bool> CheckServerUpdate()
        {
            if (_network == null)
            {
                CYLog.Error("[HotUpdateService] NetworkService 未注册");
                SetState(UpdateState.Failed);
                return false;
            }
            
            var response = await _network.Get(_config.VersionUrl);
            
            if (!response.IsSuccess)
            {
                CYLog.Error($"[HotUpdateService] 获取版本信息失败: {response.Error}");
                SetState(UpdateState.Failed);
                return false;
            }
            
            _remoteVersion = JsonUtility.FromJson<VersionInfo>(response.Data);
            
            if (_remoteVersion == null)
            {
                CYLog.Error("[HotUpdateService] 解析版本信息失败");
                SetState(UpdateState.Failed);
                return false;
            }
            
            // 比较版本
            bool hasUpdate = CompareVersion(_localVersion, _remoteVersion.version) < 0;
            
            if (hasUpdate)
            {
                CYLog.Info($"[HotUpdateService] 发现新版本: {_remoteVersion.version}");
                
                // 计算需要下载的资源
                CalculatePendingDownloads();
                
                OnNewVersionFound?.Invoke(_remoteVersion);
            }
            else
            {
                CYLog.Info("[HotUpdateService] 已是最新版本");
            }
            
            SetState(UpdateState.Idle);
            return hasUpdate;
        }
        
        /// <summary>
        /// 检查微信更新
        /// 文档：微信代码分包
        /// </summary>
        private async Task<bool> CheckWeChatUpdate()
        {
            // 微信使用自己的更新机制
            // wx.getUpdateManager
            CYLog.Debug("[HotUpdateService] 微信端使用 wx.getUpdateManager");
            
            await Task.Yield();
            SetState(UpdateState.Idle);
            
            // 返回 false 表示无需手动更新（微信自动处理）
            return false;
        }
        
        /// <summary>
        /// 计算待下载资源
        /// </summary>
        private void CalculatePendingDownloads()
        {
            _pendingDownloads.Clear();
            
            if (_remoteVersion?.bundles == null) return;
            
            foreach (var bundle in _remoteVersion.bundles)
            {
                // 检查本地是否已有该版本的资源
                if (!IsLocalBundleValid(bundle))
                {
                    _pendingDownloads.Add(bundle);
                }
            }
            
            CYLog.Debug($"[HotUpdateService] 待下载资源: {_pendingDownloads.Count} 个");
        }
        
        /// <summary>
        /// 检查本地资源是否有效
        /// 文档：WebGL/微信不支持 System.IO
        /// </summary>
        private bool IsLocalBundleValid(AssetBundle bundle)
        {
#if CY_WECHAT || UNITY_WEBGL
            // WebGL/微信平台：使用 Storage 检查
            if (ServiceLocator.TryGet<IStorageAdapter>(out var storage))
            {
                string hashKey = $"CYF_Bundle_{bundle.name}_hash";
                string localHash = storage.GetString(hashKey, null);
                return !string.IsNullOrEmpty(localHash) && localHash == bundle.hash;
            }
            return false;
#else
            // Native 平台：使用文件系统
            string localPath = System.IO.Path.Combine(Application.persistentDataPath, "Bundles", bundle.name);
            string hashPath = localPath + ".hash";
            
            if (!System.IO.File.Exists(localPath) || !System.IO.File.Exists(hashPath))
            {
                return false;
            }
            
            try
            {
                string localHash = System.IO.File.ReadAllText(hashPath).Trim();
                return localHash == bundle.hash;
            }
            catch
            {
                return false;
            }
#endif
        }
        
        /// <summary>
        /// 下载单个资源包
        /// 文档：WebGL/微信不支持 System.IO，使用 Storage 存储
        /// </summary>
        private async Task<bool> DownloadBundle(AssetBundle bundle)
        {
            string url = _config.CdnBaseUrl + bundle.name;
            CYLog.Debug($"[HotUpdateService] 下载: {url}");
            
            try
            {
                // 使用 UnityWebRequest 下载
                using var request = UnityEngine.Networking.UnityWebRequest.Get(url);
                request.timeout = _config.DownloadTimeout;
                
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    CYLog.Error($"[HotUpdateService] 下载失败: {request.error}");
                    return false;
                }
                
                byte[] data = request.downloadHandler.data;
                
#if CY_WECHAT || UNITY_WEBGL
                // WebGL/微信平台：使用 Storage 存储
                if (ServiceLocator.TryGet<IStorageAdapter>(out var storage))
                {
                    // 将二进制数据转为 Base64 存储
                    string dataKey = $"CYF_Bundle_{bundle.name}";
                    string hashKey = $"CYF_Bundle_{bundle.name}_hash";
                    
                    storage.SetString(dataKey, Convert.ToBase64String(data));
                    storage.SetString(hashKey, bundle.hash);
                    storage.Save();
                    
                    CYLog.Debug($"[HotUpdateService] 下载完成 (Storage): {bundle.name}");
                    return true;
                }
                else
                {
                    CYLog.Error("[HotUpdateService] IStorageAdapter 未注册");
                    return false;
                }
#else
                // Native 平台：使用文件系统
                string localDir = System.IO.Path.Combine(Application.persistentDataPath, "Bundles");
                string localPath = System.IO.Path.Combine(localDir, bundle.name);
                string hashPath = localPath + ".hash";
                
                if (!System.IO.Directory.Exists(localDir))
                {
                    System.IO.Directory.CreateDirectory(localDir);
                }
                
                await System.IO.File.WriteAllBytesAsync(localPath, data);
                await System.IO.File.WriteAllTextAsync(hashPath, bundle.hash);
                
                CYLog.Debug($"[HotUpdateService] 下载完成: {bundle.name}");
                return true;
#endif
            }
            catch (Exception ex)
            {
                CYLog.Error($"[HotUpdateService] 下载异常: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 比较版本号
        /// </summary>
        private int CompareVersion(string v1, string v2)
        {
            var parts1 = v1.Split('.');
            var parts2 = v2.Split('.');
            
            int maxLen = Mathf.Max(parts1.Length, parts2.Length);
            
            for (int i = 0; i < maxLen; i++)
            {
                int n1 = i < parts1.Length && int.TryParse(parts1[i], out int p1) ? p1 : 0;
                int n2 = i < parts2.Length && int.TryParse(parts2[i], out int p2) ? p2 : 0;
                
                if (n1 < n2) return -1;
                if (n1 > n2) return 1;
            }
            
            return 0;
        }
        
        /// <summary>
        /// 设置状态
        /// </summary>
        private void SetState(UpdateState state)
        {
            _state = state;
            _progress.State = state;
            NotifyProgress();
        }
        
        /// <summary>
        /// 通知进度变化
        /// </summary>
        private void NotifyProgress()
        {
            OnProgressChanged?.Invoke(_progress);
        }
        
        #endregion
    }
}
