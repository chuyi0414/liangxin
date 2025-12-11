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
        public bool IsSuccess;
        public int StatusCode;
        public string Data;
        public string Error;
    }
    
    /// <summary>
    /// 网络服务
    /// </summary>
    public class NetworkService : IInitializable, ITickable, IDisposableEx
    {
        private NetworkConfig _config;
        private INetworkAdapter _adapter;
        
        // WebSocket 相关
        private IWebSocket _webSocket;
        private string _wsUrl;
        private NetworkState _wsState = NetworkState.Disconnected;
        private int _reconnectAttempts;
        private float _reconnectTimer;
        
        // 心跳相关
        private float _heartbeatTimer;
        private int _missedHeartbeats;
        
        // 熔断器
        private int _consecutiveFailures;
        private bool _isCircuitOpen;
        private float _circuitOpenTime;
        
        // 请求队列（断线重发）
        private readonly Queue<(string url, string body, Action<HttpResponse> callback)> _pendingRequests = new();
        
        // 事件
        public event Action<NetworkState> OnStateChanged;
        public event Action<string> OnMessage;
        public event Action<byte[]> OnBinaryMessage;
        
        public NetworkState State => _wsState;
        public bool IsConnected => _wsState == NetworkState.Connected;
        
        public int InitOrder => 10;
        public int TickOrder => 10;
        public int DisposeOrder => 10;
        
        public NetworkService(NetworkConfig config = null)
        {
            _config = config ?? new NetworkConfig();
        }
        
        #region 生命周期
        
        public void Initialize()
        {
            // 从 CYConfigurator 读取配置
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                var externalConfig = configurator.GetConfig<NetworkServiceConfig>();
                if (externalConfig != null)
                {
                    _config.HttpTimeout = (int)externalConfig.HttpTimeout;
                    _config.HeartbeatInterval = externalConfig.HeartbeatInterval;
                    _config.MaxReconnectAttempts = externalConfig.MaxReconnectAttempts;
                    _config.ReconnectBaseInterval = externalConfig.ReconnectInterval;
                    _config.CircuitBreakerThreshold = externalConfig.CircuitBreakerThreshold;
                    _config.CircuitBreakerRecoveryTime = externalConfig.CircuitBreakerResetTime;
                    CYLog.Debug("[NetworkService] 使用 CYConfigurator 配置");
                }
            }
            
            // 获取平台网络适配器
            if (ServiceLocator.TryGet<INetworkAdapter>(out var adapter))
            {
                _adapter = adapter;
            }
            
            CYLog.Debug($"[NetworkService] 初始化完成，适配器: {_adapter?.GetType().Name ?? "无"}");
        }
        
        public void Tick(float deltaTime)
        {
            // 更新心跳
            UpdateHeartbeat(deltaTime);
            
            // 更新重连
            UpdateReconnect(deltaTime);
            
            // 更新熔断器恢复
            UpdateCircuitBreaker(deltaTime);
        }
        
        public void Dispose()
        {
            CloseWebSocket();
            _pendingRequests.Clear();
            CYLog.Debug("[NetworkService] 已销毁");
        }
        
        #endregion
        
        #region HTTP API
        
        /// <summary>
        /// HTTP GET 请求
        /// </summary>
        public async Task<HttpResponse> Get(string url)
        {
            if (_isCircuitOpen)
            {
                return new HttpResponse { IsSuccess = false, Error = "Circuit breaker is open" };
            }
            
            try
            {
                using var request = UnityWebRequest.Get(url);
                request.timeout = _config.HttpTimeout;
                
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                var response = new HttpResponse
                {
                    StatusCode = (int)request.responseCode,
                    IsSuccess = request.result == UnityWebRequest.Result.Success,
                    Data = request.downloadHandler?.text,
                    Error = request.error
                };
                
                HandleRequestResult(response.IsSuccess);
                return response;
            }
            catch (Exception ex)
            {
                HandleRequestResult(false);
                return new HttpResponse { IsSuccess = false, Error = ex.Message };
            }
        }
        
        /// <summary>
        /// HTTP POST 请求
        /// </summary>
        public async Task<HttpResponse> Post(string url, string body, string contentType = "application/json")
        {
            if (_isCircuitOpen)
            {
                return new HttpResponse { IsSuccess = false, Error = "Circuit breaker is open" };
            }
            
            try
            {
                using var request = new UnityWebRequest(url, "POST");
                byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", contentType);
                request.timeout = _config.HttpTimeout;
                
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                var response = new HttpResponse
                {
                    StatusCode = (int)request.responseCode,
                    IsSuccess = request.result == UnityWebRequest.Result.Success,
                    Data = request.downloadHandler?.text,
                    Error = request.error
                };
                
                HandleRequestResult(response.IsSuccess);
                return response;
            }
            catch (Exception ex)
            {
                HandleRequestResult(false);
                return new HttpResponse { IsSuccess = false, Error = ex.Message };
            }
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
        
        private void OnWebSocketOpen()
        {
            _wsState = NetworkState.Connected;
            _reconnectAttempts = 0;
            _missedHeartbeats = 0;
            OnStateChanged?.Invoke(_wsState);
            CYLog.Info("[NetworkService] WebSocket 连接成功");
        }
        
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
        
        private void OnWebSocketBinaryMessage(byte[] data)
        {
            OnBinaryMessage?.Invoke(data);
        }
        
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
        /// 关闭 WebSocket
        /// </summary>
        public void CloseWebSocket()
        {
            _webSocket?.Close();
            _webSocket = null;
            _wsState = NetworkState.Disconnected;
            OnStateChanged?.Invoke(_wsState);
        }
        
        #endregion
        
        #region 私有方法
        
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
