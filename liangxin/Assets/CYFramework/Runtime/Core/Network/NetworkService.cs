// ============================================================================
// CYFramework 2.2 - 网络服务
// 文档位置：3.1.3 网络层 (Network Layer)
// 功能：HTTP/WebSocket、自动重连、心跳保活、熔断降级
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CYFramework.Core.Config;
using CYFramework.Infrastructure;
using CYFramework.Platform;
using UnityEngine;
using UnityEngine.Networking;

namespace CYFramework.Core.Network
{
    /// <summary>
    /// 网络状态
    /// </summary>
    public enum NetworkState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }
    
    /// <summary>
    /// 网络配置
    /// </summary>
    [Serializable]
    public class NetworkConfig
    {
        /// <summary>
        /// HTTP 超时时间（秒）
        /// </summary>
        public int HttpTimeout = 10;

        /// <summary>
        /// HTTP 最大重试次数（不含首次请求）。
        /// </summary>
        public int HttpMaxRetry = 0;
        
        /// <summary>
        /// WebSocket 重连最大次数
        /// </summary>
        public int MaxReconnectAttempts = 5;
        
        /// <summary>
        /// 重连基础间隔（秒）
        /// </summary>
        public float ReconnectBaseInterval = 1f;
        
        /// <summary>
        /// 重连最大间隔（秒）
        /// </summary>
        public float ReconnectMaxInterval = 30f;
        
        /// <summary>
        /// 心跳间隔（秒）
        /// </summary>
        public float HeartbeatInterval = 15f;
        
        /// <summary>
        /// 心跳超时次数
        /// </summary>
        public int HeartbeatTimeoutCount = 3;
        
        /// <summary>
        /// 熔断阈值（连续失败次数）
        /// </summary>
        public int CircuitBreakerThreshold = 5;
        
        /// <summary>
        /// 熔断恢复时间（秒）
        /// </summary>
        public float CircuitBreakerRecoveryTime = 30f;
    }
    
    /// <summary>
    /// HTTP 响应
    /// </summary>
    public class HttpResponse
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess;
        /// <summary>
        /// HTTP 状态码
        /// </summary>
        public int StatusCode;
        /// <summary>
        /// 响应内容
        /// </summary>
        public string Data;
        /// <summary>
        /// 错误信息
        /// </summary>
        public string Error;
    }
    
    /// <summary>
    /// 网络服务
    /// </summary>
    public class NetworkService : IInitializable, ITickable, IDisposableEx
    {
        // WebSocket 断线期间最大缓存条数，避免无限堆积导致内存不可控。
        private const int MaxPendingWsMessages = 64;

        /// <summary>
        /// 网络配置
        /// </summary>
        private NetworkConfig _config;
        /// <summary>
        /// 平台网络适配器
        /// </summary>
        private INetworkAdapter _adapter;
        
        // WebSocket 相关
        /// <summary>
        /// WebSocket 实例
        /// </summary>
        private IWebSocket _webSocket;
        /// <summary>
        /// WebSocket 地址
        /// </summary>
        private string _wsUrl;
        /// <summary>
        /// WebSocket 当前状态
        /// </summary>
        private NetworkState _wsState = NetworkState.Disconnected;
        /// <summary>
        /// 已尝试重连次数
        /// </summary>
        private int _reconnectAttempts;
        /// <summary>
        /// 重连计时器
        /// </summary>
        private float _reconnectTimer;
        
        // 心跳相关
        /// <summary>
        /// 心跳计时器
        /// </summary>
        private float _heartbeatTimer;
        /// <summary>
        /// 未收到心跳次数
        /// </summary>
        private int _missedHeartbeats;
        
        // 熔断器
        /// <summary>
        /// 连续失败次数
        /// </summary>
        private int _consecutiveFailures;
        /// <summary>
        /// 熔断器是否打开
        /// </summary>
        private bool _isCircuitOpen;
        /// <summary>
        /// 熔断器打开时长
        /// </summary>
        private float _circuitOpenTime;
        
        // 请求队列（断线重发）
        /// <summary>
        /// 待发送请求队列
        /// </summary>
        private readonly Queue<(string url, string body, Action<HttpResponse> callback)> _pendingRequests = new();

        // WebSocket 待发送消息队列：断线/重连期间缓存，连接成功后自动冲刷。
        // 注意：仅适用于“允许丢失时序”的消息；强一致消息应在业务层自行做 ack/重发机制。
        private readonly Queue<string> _pendingWsMessages = new();
        
        // 事件
        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event Action<NetworkState> OnStateChanged;
        /// <summary>
        /// 文本消息事件
        /// </summary>
        public event Action<string> OnMessage;
        /// <summary>
        /// 二进制消息事件
        /// </summary>
        public event Action<byte[]> OnBinaryMessage;
        
        /// <summary>
        /// 当前连接状态
        /// </summary>
        public NetworkState State => _wsState;
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _wsState == NetworkState.Connected;
        
        /// <summary>
        /// 初始化顺序
        /// </summary>
        public int InitOrder => 10;
        /// <summary>
        /// Tick 顺序
        /// </summary>
        public int TickOrder => 10;
        /// <summary>
        /// 释放顺序
        /// </summary>
        public int DisposeOrder => 10;
        
        /// <summary>
        /// 构造网络服务
        /// </summary>
        public NetworkService(NetworkConfig config = null)
        {
            _config = config ?? new NetworkConfig();
        }
        
        #region 生命周期
        
        /// <summary>
        /// 初始化网络服务
        /// </summary>
        public void Initialize()
        {
            // 从 CYConfigurator 读取配置
            // 配置中心
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                // 外部配置
                var externalConfig = configurator.GetConfig<NetworkServiceConfig>();
                if (externalConfig != null)
                {
                    _config.HttpTimeout = (int)externalConfig.HttpTimeout;
                    _config.HttpMaxRetry = Mathf.Max(0, externalConfig.HttpMaxRetry);
                    _config.HeartbeatInterval = externalConfig.HeartbeatInterval;
                    _config.MaxReconnectAttempts = externalConfig.MaxReconnectAttempts;
                    _config.ReconnectBaseInterval = externalConfig.ReconnectInterval;
                    // HeartbeatTimeout（秒）换算为 HeartbeatTimeoutCount：至少为 1
                    // 例如 interval=30s, timeout=10s -> ceil(10/30)=1（下一次心跳未回包即判定超时）
                    // interval=5s, timeout=10s -> ceil(10/5)=2（连续 2 次心跳未回包判定超时）
                    // 心跳超时次数
                    var timeoutCount = Mathf.CeilToInt(externalConfig.HeartbeatTimeout / Mathf.Max(0.001f, externalConfig.HeartbeatInterval));
                    _config.HeartbeatTimeoutCount = Mathf.Max(1, timeoutCount);
                    _config.CircuitBreakerThreshold = externalConfig.CircuitBreakerThreshold;
                    _config.CircuitBreakerRecoveryTime = externalConfig.CircuitBreakerResetTime;
                    CYLog.Debug("[NetworkService] 使用 CYConfigurator 配置");
                }
            }
            
            // 获取平台网络适配器
            // 网络适配器实例
            if (ServiceLocator.TryGet<INetworkAdapter>(out var adapter))
            {
                _adapter = adapter;
            }
            
            CYLog.Debug($"[NetworkService] 初始化完成，适配器: {_adapter?.GetType().Name ?? "无"}");
        }
        
        /// <summary>
        /// Tick 驱动（心跳/重连/熔断）
        /// </summary>
        public void Tick(float deltaTime)
        {
            // 更新心跳
            UpdateHeartbeat(deltaTime);
            
            // 更新重连
            UpdateReconnect(deltaTime);
            
            // 更新熔断器恢复
            UpdateCircuitBreaker(deltaTime);
        }
        
        /// <summary>
        /// 释放网络服务
        /// </summary>
        public void Dispose()
        {
            OnStateChanged = null;
            OnMessage = null;
            OnBinaryMessage = null;
            
            CloseWebSocket();
            _pendingRequests.Clear();
            CYLog.Debug("[NetworkService] 已销毁");
        }
        
        #endregion
        
        #region HTTP API
        
        /// <summary>
        /// HTTP GET 请求
        /// 统一走 INetworkAdapter，确保平台一致性
        /// </summary>
        public async Task<HttpResponse> Get(string url)
        {
            if (_isCircuitOpen)
            {
                return new HttpResponse { IsSuccess = false, Error = "Circuit breaker is open" };
            }
            
            // 最大重试次数
            var maxRetry = _config != null ? Mathf.Max(0, _config.HttpMaxRetry) : 0;
            // 总尝试次数
            var totalAttempts = 1 + maxRetry;

            // attempt 为重试次数索引
            for (int attempt = 0; attempt < totalAttempts; attempt++)
            {
                try
                {
                    // 响应数据
                    string data;

                    // 优先使用平台适配器（确保微信/WebGL 路径一致）
                    if (_adapter != null)
                    {
                        data = await _adapter.HttpGet(url, _config.HttpTimeout);
                    }
                    else
                    {
                        // Fallback: 直接使用 UnityWebRequest
                        data = await HttpGetFallback(url);
                    }

                    // HTTP 响应对象
                    var response = new HttpResponse
                    {
                        StatusCode = 200,
                        IsSuccess = true,
                        Data = data,
                        Error = null
                    };

                    HandleRequestResult(true);
                    return response;
                }
                catch (Exception ex)
                {
                    HandleRequestResult(false);

                    // 达到最大重试次数，返回失败
                    if (attempt >= totalAttempts - 1)
                    {
                        return new HttpResponse { IsSuccess = false, Error = ex.Message };
                    }

                    // 低频路径：简单退避一帧，避免立刻重试造成尖峰
                    await Task.Yield();
                }
            }

            return new HttpResponse { IsSuccess = false, Error = "Unknown network error" };
        }
        
        /// <summary>
        /// HTTP POST 请求
        /// 统一走 INetworkAdapter，确保平台一致性
        /// </summary>
        public async Task<HttpResponse> Post(string url, string body, string contentType = "application/json")
        {
            if (_isCircuitOpen)
            {
                return new HttpResponse { IsSuccess = false, Error = "Circuit breaker is open" };
            }
            
            // 最大重试次数
            var maxRetry = _config != null ? Mathf.Max(0, _config.HttpMaxRetry) : 0;
            // 总尝试次数
            var totalAttempts = 1 + maxRetry;

            // attempt 为重试次数索引
            for (int attempt = 0; attempt < totalAttempts; attempt++)
            {
                try
                {
                    // 响应数据
                    string data;

                    // 优先使用平台适配器（确保微信/WebGL 路径一致）
                    if (_adapter != null)
                    {
                        data = await _adapter.HttpPost(url, body, contentType, _config.HttpTimeout);
                    }
                    else
                    {
                        // Fallback: 直接使用 UnityWebRequest
                        data = await HttpPostFallback(url, body, contentType);
                    }

                    // HTTP 响应对象
                    var response = new HttpResponse
                    {
                        StatusCode = 200,
                        IsSuccess = true,
                        Data = data,
                        Error = null
                    };

                    HandleRequestResult(true);
                    return response;
                }
                catch (Exception ex)
                {
                    HandleRequestResult(false);

                    if (attempt >= totalAttempts - 1)
                    {
                        return new HttpResponse { IsSuccess = false, Error = ex.Message };
                    }

                    await Task.Yield();
                }
            }

            return new HttpResponse { IsSuccess = false, Error = "Unknown network error" };
        }

        /// <summary>
        /// HTTP GET 并解析 JSON（使用 Unity JsonUtility）。
        /// </summary>
        /// <remarks>
        /// - JsonUtility 更适合“配置/协议数据结构固定”的场景；复杂 JSON 建议业务层用更强的 JSON 库自行处理。
        /// - 返回 null 表示请求失败或解析失败（错误信息会写入 response.Error 或日志）。
        /// </remarks>
        public async Task<T> GetJson<T>(string url) where T : class
        {
            // HTTP 响应
            var response = await Get(url);
            if (!response.IsSuccess || string.IsNullOrEmpty(response.Data))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(response.Data);
            }
            catch (Exception ex)
            {
                CYLog.Error($"[NetworkService] JSON 解析失败: {url}", ex);
                return null;
            }
        }

        /// <summary>
        /// HTTP POST（JSON）并解析 JSON（使用 Unity JsonUtility）。
        /// </summary>
        public async Task<TResponse> PostJson<TBody, TResponse>(string url, TBody body)
            where TResponse : class
        {
            // JSON 字符串
            string json;
            try
            {
                json = JsonUtility.ToJson(body);
            }
            catch (Exception ex)
            {
                CYLog.Error("[NetworkService] JSON 序列化失败", ex);
                return null;
            }

            // HTTP 响应
            var response = await Post(url, json, "application/json");
            if (!response.IsSuccess || string.IsNullOrEmpty(response.Data))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<TResponse>(response.Data);
            }
            catch (Exception ex)
            {
                CYLog.Error($"[NetworkService] JSON 解析失败: {url}", ex);
                return null;
            }
        }
        
        /// <summary>
        /// HTTP GET Fallback（无适配器时使用）
        /// </summary>
        private async Task<string> HttpGetFallback(string url)
        {
            // UnityWebRequest 请求
            using var request = UnityWebRequest.Get(url);
            request.timeout = _config.HttpTimeout;
            
            // 异步请求操作
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(request.error);
            }
            
            return request.downloadHandler?.text;
        }
        
        /// <summary>
        /// HTTP POST Fallback（无适配器时使用）
        /// </summary>
        private async Task<string> HttpPostFallback(string url, string body, string contentType)
        {
            // UnityWebRequest 请求
            using var request = new UnityWebRequest(url, "POST");
            // 请求体字节
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            request.timeout = _config.HttpTimeout;
            
            // 异步请求操作
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(request.error);
            }
            
            return request.downloadHandler?.text;
        }
        
        #endregion
        
        #region WebSocket API
        
        /// <summary>
        /// 连接 WebSocket
        /// </summary>
        public async Task ConnectWebSocket(string url)
        {
            if (_wsState == NetworkState.Connected || _wsState == NetworkState.Connecting)
            {
                CYLog.Warning("[NetworkService] WebSocket 已连接或正在连接");
                return;
            }
            
            _wsUrl = url;
            _wsState = NetworkState.Connecting;
            OnStateChanged?.Invoke(_wsState);
            
            try
            {
                CYLog.Info($"[NetworkService] 正在连接 WebSocket: {url}");
                
                // 使用平台适配器创建 WebSocket
                if (_adapter != null)
                {
                    _webSocket = _adapter.CreateWebSocket(url);
                }
                else
                {
                    CYLog.Error("[NetworkService] INetworkAdapter 未注册");
                    _wsState = NetworkState.Disconnected;
                    OnStateChanged?.Invoke(_wsState);
                    return;
                }
                
                // 绑定事件
                _webSocket.OnOpen += OnWebSocketOpen;
                _webSocket.OnMessage += OnWebSocketMessage;
                _webSocket.OnBinaryMessage += OnWebSocketBinaryMessage;
                _webSocket.OnClose += OnWebSocketClose;
                _webSocket.OnError += OnWebSocketError;
                
                // 连接
                await _webSocket.Connect();
            }
            catch (Exception ex)
            {
                CYLog.Error($"[NetworkService] WebSocket 连接失败: {ex.Message}");
                _wsState = NetworkState.Disconnected;
                OnStateChanged?.Invoke(_wsState);
                StartReconnect();
            }
        }
        
        /// <summary>
        /// WebSocket 连接成功回调
        /// </summary>
        private void OnWebSocketOpen()
        {
            _wsState = NetworkState.Connected;
            _reconnectAttempts = 0;
            _missedHeartbeats = 0;
            OnStateChanged?.Invoke(_wsState);
            CYLog.Info("[NetworkService] WebSocket 连接成功");

            // 连接成功后冲刷断线期间缓存的消息
            FlushPendingWsMessages();
        }
        
        /// <summary>
        /// WebSocket 文本消息回调
        /// </summary>
        private void OnWebSocketMessage(string message)
        {
            // 处理心跳响应
            if (message == "pong" || message == "{\"type\":\"pong\"}")
            {
                _missedHeartbeats = 0;
                return;
            }
            
            OnMessage?.Invoke(message);
        }
        
        /// <summary>
        /// WebSocket 二进制消息回调
        /// </summary>
        private void OnWebSocketBinaryMessage(byte[] data)
        {
            OnBinaryMessage?.Invoke(data);
        }
        
        /// <summary>
        /// WebSocket 关闭回调
        /// </summary>
        private void OnWebSocketClose(string reason)
        {
            CYLog.Warning($"[NetworkService] WebSocket 关闭: {reason}");
            _wsState = NetworkState.Disconnected;
            OnStateChanged?.Invoke(_wsState);
            
            // 如果不是主动关闭，尝试重连
            if (reason != "正常关闭" && reason != "Client closed")
            {
                StartReconnect();
            }
        }
        
        /// <summary>
        /// WebSocket 错误回调
        /// </summary>
        private void OnWebSocketError(string error)
        {
            CYLog.Error($"[NetworkService] WebSocket 错误: {error}");
        }
        
        /// <summary>
        /// 发送 WebSocket 消息
        /// </summary>
        public void SendWebSocket(string message)
        {
            if (_wsState != NetworkState.Connected)
            {
                CYLog.Warning("[NetworkService] WebSocket 未连接，消息已缓存");
                return;
            }
            
            _webSocket?.Send(message);
        }
        
        /// <summary>
        /// 尝试发送 WebSocket 消息：未连接则返回 false（不会自动缓存）。
        /// </summary>
        public bool TrySendWebSocket(string message)
        {
            if (_wsState != NetworkState.Connected || _webSocket == null) return false;
            _webSocket.Send(message);
            return true;
        }

        /// <summary>
        /// 发送 WebSocket 消息：若未连接则进入缓存队列，待连接成功后自动发送。
        /// </summary>
        public void SendWebSocketOrQueue(string message)
        {
            if (!TrySendWebSocket(message))
            {
                EnqueuePendingWsMessage(message);
            }
        }

        /// <summary>
        /// 关闭 WebSocket
        /// </summary>
        public void CloseWebSocket()
        {
            CloseWebSocket(clearPendingMessages: true);
        }

        /// <summary>
        /// 关闭 WebSocket（可选是否清空待发送消息队列）。
        /// </summary>
        public void CloseWebSocket(bool clearPendingMessages)
        {
            _webSocket?.Close();
            _webSocket = null;
            _wsState = NetworkState.Disconnected;
            OnStateChanged?.Invoke(_wsState);

            if (clearPendingMessages)
            {
                _pendingWsMessages.Clear();
            }
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 缓存待发送的 WebSocket 消息
        /// </summary>
        private void EnqueuePendingWsMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            // 为了避免断线期间无限堆积，超过上限则丢弃最早的一条
            if (_pendingWsMessages.Count >= MaxPendingWsMessages)
            {
                _pendingWsMessages.Dequeue();
            }

            _pendingWsMessages.Enqueue(message);
        }

        /// <summary>
        /// 冲刷待发送的 WebSocket 消息
        /// </summary>
        private void FlushPendingWsMessages()
        {
            if (_wsState != NetworkState.Connected || _webSocket == null) return;

            while (_pendingWsMessages.Count > 0)
            {
                _webSocket.Send(_pendingWsMessages.Dequeue());
            }
        }

        /// <summary>
        /// 处理请求结果（熔断器逻辑）
        /// </summary>
        private void HandleRequestResult(bool success)
        {
            if (success)
            {
                _consecutiveFailures = 0;
            }
            else
            {
                _consecutiveFailures++;
                
                if (_consecutiveFailures >= _config.CircuitBreakerThreshold)
                {
                    OpenCircuitBreaker();
                }
            }
        }
        
        /// <summary>
        /// 打开熔断器
        /// </summary>
        private void OpenCircuitBreaker()
        {
            if (_isCircuitOpen) return;
            
            _isCircuitOpen = true;
            _circuitOpenTime = 0f;
            CYLog.Warning($"[NetworkService] 熔断器已打开，{_config.CircuitBreakerRecoveryTime}秒后尝试恢复");
        }
        
        /// <summary>
        /// 更新熔断器
        /// </summary>
        private void UpdateCircuitBreaker(float deltaTime)
        {
            if (!_isCircuitOpen) return;
            
            _circuitOpenTime += deltaTime;
            
            if (_circuitOpenTime >= _config.CircuitBreakerRecoveryTime)
            {
                _isCircuitOpen = false;
                _consecutiveFailures = 0;
                CYLog.Info("[NetworkService] 熔断器已关闭，恢复请求");
            }
        }
        
        /// <summary>
        /// 启动重连
        /// </summary>
        private void StartReconnect()
        {
            if (_reconnectAttempts >= _config.MaxReconnectAttempts)
            {
                CYLog.Error("[NetworkService] 重连次数已达上限");
                return;
            }
            
            _wsState = NetworkState.Reconnecting;
            OnStateChanged?.Invoke(_wsState);
            
            // 指数退避计算重连间隔
            // 计算后的重连间隔
            float interval = Mathf.Min(
                _config.ReconnectBaseInterval * Mathf.Pow(2, _reconnectAttempts),
                _config.ReconnectMaxInterval
            );
            
            _reconnectTimer = interval;
            _reconnectAttempts++;
            
            CYLog.Info($"[NetworkService] {interval}秒后进行第 {_reconnectAttempts} 次重连");
        }
        
        /// <summary>
        /// 更新重连
        /// </summary>
        private void UpdateReconnect(float deltaTime)
        {
            if (_wsState != NetworkState.Reconnecting) return;
            
            _reconnectTimer -= deltaTime;
            
            if (_reconnectTimer <= 0)
            {
                _ = ConnectWebSocket(_wsUrl);
            }
        }
        
        /// <summary>
        /// 更新心跳
        /// </summary>
        private void UpdateHeartbeat(float deltaTime)
        {
            if (_wsState != NetworkState.Connected) return;
            
            _heartbeatTimer += deltaTime;
            
            if (_heartbeatTimer >= _config.HeartbeatInterval)
            {
                _heartbeatTimer = 0;
                SendHeartbeat();
            }
        }
        
        /// <summary>
        /// 发送心跳
        /// </summary>
        private void SendHeartbeat()
        {
            if (_webSocket == null || _wsState != NetworkState.Connected) return;
            
            _missedHeartbeats++;
            
            // 检查是否超时
            if (_missedHeartbeats >= _config.HeartbeatTimeoutCount)
            {
                OnHeartbeatTimeout();
                return;
            }
            
            // 发送心跳包
            try
            {
                _webSocket.Send("{\"type\":\"ping\"}");
                CYLog.Trace("[NetworkService] 发送心跳");
            }
            catch (Exception ex)
            {
                CYLog.Warning($"[NetworkService] 发送心跳失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 心跳超时
        /// </summary>
        private void OnHeartbeatTimeout()
        {
            _missedHeartbeats++;
            
            if (_missedHeartbeats >= _config.HeartbeatTimeoutCount)
            {
                CYLog.Warning("[NetworkService] 心跳超时，断开连接");
                CloseWebSocket();
                StartReconnect();
            }
        }
        
        #endregion
    }
}
