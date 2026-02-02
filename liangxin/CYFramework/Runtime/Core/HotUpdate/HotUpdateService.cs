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
        /// <summary>
        /// 版本号
        /// </summary>
        public string version;
        /// <summary>
        /// 最低兼容版本
        /// </summary>
        public string minVersion;
        /// <summary>
        /// CDN 地址
        /// </summary>
        public string cdnUrl;
        /// <summary>
        /// 资源包列表
        /// </summary>
        public List<AssetBundle> bundles;
        /// <summary>
        /// 总大小
        /// </summary>
        public long totalSize;
        /// <summary>
        /// 更新说明
        /// </summary>
        public string updateNote;
        /// <summary>
        /// 是否强制更新
        /// </summary>
        public bool forceUpdate;
    }
    
    /// <summary>
    /// 资源包信息
    /// </summary>
    [Serializable]
    public class AssetBundle
    {
        /// <summary>
        /// 资源包名称
        /// </summary>
        public string name;
        /// <summary>
        /// 资源包哈希
        /// </summary>
        public string hash;
        /// <summary>
        /// 资源包大小
        /// </summary>
        public long size;
        /// <summary>
        /// 下载优先级
        /// </summary>
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
        /// 单个文件下载失败最大重试次数（不含首次请求）。
        /// </summary>
        public int MaxDownloadRetry = 0;
        
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
        /// <summary>
        /// 当前更新状态
        /// </summary>
        public UpdateState State;
        /// <summary>
        /// 总体进度（0~1）
        /// </summary>
        public float Progress;
        /// <summary>
        /// 已下载字节数
        /// </summary>
        public long DownloadedBytes;
        /// <summary>
        /// 总字节数
        /// </summary>
        public long TotalBytes;
        /// <summary>
        /// 当前下载文件名
        /// </summary>
        public string CurrentFile;
        /// <summary>
        /// 已下载文件数
        /// </summary>
        public int DownloadedCount;
        /// <summary>
        /// 总文件数
        /// </summary>
        public int TotalCount;
    }
    
    /// <summary>
    /// 热更新服务接口
    /// </summary>
    public interface IHotUpdateService
    {
        /// <summary>
        /// 当前更新状态
        /// </summary>
        UpdateState State { get; }
        /// <summary>
        /// 当前更新进度
        /// </summary>
        UpdateProgress Progress { get; }
        
        /// <summary>
        /// 检查是否需要更新
        /// </summary>
        Task<bool> CheckUpdate();
        /// <summary>
        /// 下载更新资源
        /// </summary>
        Task<bool> DownloadUpdate(Action<UpdateProgress> onProgress = null);
        /// <summary>
        /// 应用更新
        /// </summary>
        void ApplyUpdate();
        
        /// <summary>
        /// 发现新版本事件
        /// </summary>
        event Action<VersionInfo> OnNewVersionFound;
        /// <summary>
        /// 进度变化事件
        /// </summary>
        event Action<UpdateProgress> OnProgressChanged;
        /// <summary>
        /// 错误事件
        /// </summary>
        event Action<string> OnError;
    }
    
    /// <summary>
    /// 热更新服务
    /// 文档：微信分包 + Native Addressables
    /// <para>
    /// 警告：HotUpdateService 的 Storage 模式仅适用于 &lt;2MB 的配置/脚本补丁。
    /// 对于大资源，请使用微信分包或 Addressables 远程加载。
    /// 避免在 WebGL/微信端使用 Convert.ToBase64String 存大文件，会导致内存膨胀 (~33%) 且分配在大堆 (LOH) 上引发 OOM。
    /// </para>
    /// </summary>
    public class HotUpdateService : IHotUpdateService, IInitializable, IDisposableEx
    {
        /// <summary>
        /// 热更新配置
        /// </summary>
        private HotUpdateConfig _config;
        /// <summary>
        /// 网络服务
        /// </summary>
        private NetworkService _network;
        
        /// <summary>
        /// 当前更新状态
        /// </summary>
        private UpdateState _state = UpdateState.Idle;
        /// <summary>
        /// 当前更新进度
        /// </summary>
        private UpdateProgress _progress;
        /// <summary>
        /// 远端版本信息
        /// </summary>
        private VersionInfo _remoteVersion;
        /// <summary>
        /// 本地版本号
        /// </summary>
        private string _localVersion;
        
        // 待下载列表
        /// <summary>
        /// 待下载资源列表
        /// </summary>
        private readonly List<AssetBundle> _pendingDownloads = new();
        
        /// <summary>
        /// 当前更新状态
        /// </summary>
        public UpdateState State => _state;
        /// <summary>
        /// 当前更新进度
        /// </summary>
        public UpdateProgress Progress => _progress;
        
        /// <summary>
        /// 发现新版本事件
        /// </summary>
        public event Action<VersionInfo> OnNewVersionFound;
        /// <summary>
        /// 进度变化事件
        /// </summary>
        public event Action<UpdateProgress> OnProgressChanged;
        /// <summary>
        /// 错误事件
        /// </summary>
        public event Action<string> OnError;
        
        /// <summary>
        /// 初始化顺序
        /// </summary>
        public int InitOrder => 40;
        /// <summary>
        /// 释放顺序
        /// </summary>
        public int DisposeOrder => 40;
        
        /// <summary>
        /// 无参构造函数（ServiceLocator 需要）
        /// </summary>
        public HotUpdateService() : this(null) { }
        
        /// <summary>
        /// 构造热更新服务
        /// </summary>
        public HotUpdateService(HotUpdateConfig config)
        {
            _config = config ?? new HotUpdateConfig();
        }
        
        #region 生命周期
        
        /// <summary>
        /// 初始化热更新服务
        /// </summary>
        public void Initialize()
        {
            // 从 CYConfigurator 读取配置
            // 配置中心
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                // 外部配置
                var externalConfig = configurator.GetConfig<HotUpdateServiceConfig>();
                if (externalConfig != null)
                {
                    _config.CdnBaseUrl = externalConfig.CdnBaseUrl;
                    _config.VersionUrl = externalConfig.CdnBaseUrl + externalConfig.VersionFileName;
                    _config.DownloadTimeout = (int)externalConfig.DownloadTimeout;
                    _config.MaxConcurrentDownloads = externalConfig.MaxConcurrentDownloads;
                    _config.MaxDownloadRetry = Mathf.Max(0, externalConfig.MaxDownloadRetry);
                    _config.EnableIncrementalUpdate = externalConfig.EnableIncrementalUpdate;
                    CYLog.Debug("[HotUpdateService] 使用 CYConfigurator 配置");
                }
            }
            
            // 网络服务实例
            if (ServiceLocator.TryGet<NetworkService>(out var network))
            {
                _network = network;
            }
            
            // 读取本地版本
            _localVersion = Application.version;
            
            CYLog.Debug($"[HotUpdateService] 初始化完成，本地版本: {_localVersion}");
        }
        
        /// <summary>
        /// 释放热更新服务
        /// </summary>
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
                // 总下载字节数
                long totalBytes = 0;
                foreach (var bundle in _pendingDownloads)
                {
                    // 当前资源包
                    totalBytes += bundle.size;
                }
                
                _progress.TotalBytes = totalBytes;
                _progress.TotalCount = _pendingDownloads.Count;
                _progress.DownloadedBytes = 0;
                _progress.DownloadedCount = 0;
                
                // 按优先级排序
                _pendingDownloads.Sort((a, b) => b.priority.CompareTo(a.priority));

                // 并发下载（WebGL/微信依旧是单线程 async 交错执行，不依赖多线程）
                // 是否全部下载成功
                bool allSuccess = await DownloadAllConcurrent(_pendingDownloads, onProgress);
                if (!allSuccess)
                {
                    SetState(UpdateState.Failed);
                    return false;
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
            
            // 版本信息响应
            var response = await _network.Get(_config.VersionUrl);
            
            if (!response.IsSuccess)
            {
                CYLog.Error($"[HotUpdateService] 获取版本信息失败: {response.Error}");
                SetState(UpdateState.Failed);
                return false;
            }
            
            // 远端版本数据
            _remoteVersion = JsonUtility.FromJson<VersionInfo>(response.Data);
            
            if (_remoteVersion == null)
            {
                CYLog.Error("[HotUpdateService] 解析版本信息失败");
                SetState(UpdateState.Failed);
                return false;
            }
            
            // 比较版本
            // 是否存在新版本
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
                // 当前资源包
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
            // Storage 适配器
            if (ServiceLocator.TryGet<IStorageAdapter>(out var storage))
            {
                // 哈希存储键
                string hashKey = $"CYF_Bundle_{bundle.name}_hash";
                // 本地哈希值
                string localHash = storage.GetString(hashKey, null);
                return !string.IsNullOrEmpty(localHash) && localHash == bundle.hash;
            }
            return false;
#else
            // Native 平台：使用文件系统
            // 本地资源路径
            string localPath = System.IO.Path.Combine(Application.persistentDataPath, "Bundles", bundle.name);
            // 哈希文件路径
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
            // 下载 URL
            string url = _config.CdnBaseUrl + bundle.name;
            CYLog.Debug($"[HotUpdateService] 下载: {url}");

            // 最大重试次数
            var maxRetry = _config != null ? Mathf.Max(0, _config.MaxDownloadRetry) : 0;
            // 总尝试次数
            var totalAttempts = 1 + maxRetry;

            // attempt 为重试次数索引
            for (int attempt = 0; attempt < totalAttempts; attempt++)
            {
                try
                {
                    // 使用 UnityWebRequest 下载
                    using var request = UnityEngine.Networking.UnityWebRequest.Get(url);
                    request.timeout = _config.DownloadTimeout;

                    // 异步请求操作
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        CYLog.Warning($"[HotUpdateService] 下载失败: {bundle.name}, attempt={attempt + 1}/{totalAttempts}, error={request.error}");
                        if (attempt >= totalAttempts - 1) return false;
                        await Task.Yield();
                        continue;
                    }

                    // 下载到的二进制数据
                    byte[] data = request.downloadHandler.data;

#if CY_WECHAT || UNITY_WEBGL
                    // WebGL/微信平台：使用 Storage 存储
                    // Storage 适配器
                    if (ServiceLocator.TryGet<IStorageAdapter>(out var storage))
                    {
                        // 将二进制数据转为 Base64 存储
                        // 数据键
                        string dataKey = $"CYF_Bundle_{bundle.name}";
                        // 哈希键
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
                    // 本地目录
                    string localDir = System.IO.Path.Combine(Application.persistentDataPath, "Bundles");
                    // 本地资源路径
                    string localPath = System.IO.Path.Combine(localDir, bundle.name);
                    // 哈希文件路径
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
                    CYLog.Warning($"[HotUpdateService] 下载异常: {bundle.name}, attempt={attempt + 1}/{totalAttempts}, ex={ex.Message}");
                    if (attempt >= totalAttempts - 1) return false;
                    await Task.Yield();
                }
            }

            return false;
        }

        /// <summary>
        /// 并发下载入口：受 <see cref="HotUpdateConfig.MaxConcurrentDownloads"/> 控制。
        /// </summary>
        private async Task<bool> DownloadAllConcurrent(List<AssetBundle> bundles, Action<UpdateProgress> onProgress)
        {
            if (bundles == null || bundles.Count <= 0) return true;

            // 最大并发数
            int maxConcurrent = _config != null ? Mathf.Max(1, _config.MaxConcurrentDownloads) : 1;
            if (maxConcurrent <= 1)
            {
                // 退化为串行逻辑（保持行为稳定）
                // i 为索引
                for (int i = 0; i < bundles.Count; i++)
                {
                    // 当前资源包
                    var bundle = bundles[i];
                    _progress.CurrentFile = bundle.name;
                    NotifyProgress();
                    onProgress?.Invoke(_progress);

                    // 当前包下载结果
                    bool success = await DownloadBundle(bundle);
                    if (!success)
                    {
                        OnError?.Invoke($"下载失败: {bundle.name}");
                        return false;
                    }

                    _progress.DownloadedBytes += bundle.size;
                    _progress.DownloadedCount++;
                    _progress.Progress = _progress.TotalBytes > 0 ? (float)_progress.DownloadedBytes / _progress.TotalBytes : 1f;
                    NotifyProgress();
                    onProgress?.Invoke(_progress);
                }

                return true;
            }

            // 并发窗口：用 index 作为工作队列指针（不使用 LINQ，减少分配）
            // 下一个待下载索引
            int nextIndex = 0;
            // 总资源数
            int total = bundles.Count;

            // 共享进度：并发情况下 CurrentFile 只展示“最近完成的文件”
            _progress.CurrentFile = "";
            NotifyProgress();
            onProgress?.Invoke(_progress);

            // 并发任务列表
            var tasks = new List<Task<(bool ok, AssetBundle bundle)>>(maxConcurrent);

            Task<(bool ok, AssetBundle bundle)> StartOne()
            {
                if (nextIndex >= total) return null;
                // 取出当前资源包
                var bundle = bundles[nextIndex++];
                return DownloadOne(bundle);
            }

            async Task<(bool ok, AssetBundle bundle)> DownloadOne(AssetBundle bundle)
            {
                // 单个任务开始前更新 CurrentFile（多任务会覆盖，但不影响统计）
                _progress.CurrentFile = bundle.name;
                NotifyProgress();
                onProgress?.Invoke(_progress);

                bool ok = await DownloadBundle(bundle);
                return (ok, bundle);
            }

            // 预热启动
            // i 为索引
            for (int i = 0; i < maxConcurrent; i++)
            {
                // 启动一个下载任务
                var t = StartOne();
                if (t != null) tasks.Add(t);
            }

            while (tasks.Count > 0)
            {
                // 等待任意任务完成
                var finished = await Task.WhenAny(tasks);
                tasks.Remove(finished);

                // 已完成任务结果
                var result = await finished;
                if (!result.ok)
                {
                    OnError?.Invoke($"下载失败: {result.bundle.name}");
                    return false;
                }

                _progress.CurrentFile = result.bundle.name;
                _progress.DownloadedBytes += result.bundle.size;
                _progress.DownloadedCount++;
                _progress.Progress = _progress.TotalBytes > 0 ? (float)_progress.DownloadedBytes / _progress.TotalBytes : 1f;
                NotifyProgress();
                onProgress?.Invoke(_progress);

                // 启动下一个任务
                var next = StartOne();
                if (next != null) tasks.Add(next);
            }

            return true;
        }
        
        /// <summary>
        /// 比较版本号
        /// </summary>
        private int CompareVersion(string v1, string v2)
        {
            // 版本段数组
            var parts1 = v1.Split('.');
            // 版本段数组
            var parts2 = v2.Split('.');
            
            // 最大段数
            int maxLen = Mathf.Max(parts1.Length, parts2.Length);
            
            for (int i = 0; i < maxLen; i++)
            {
                // v1 当前段数值
                int n1 = i < parts1.Length && int.TryParse(parts1[i], out int p1) ? p1 : 0;
                // v2 当前段数值
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
