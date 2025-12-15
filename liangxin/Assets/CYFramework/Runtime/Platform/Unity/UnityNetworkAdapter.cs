// ============================================================================
// CYFramework 2.2 - Unity 网络适配器
// 适用平台：PC / Android / iOS（不支持 WebGL/微信小游戏）
// 使用 UnityWebRequest + NativeWebSocket
// ============================================================================

#if !UNITY_WEBGL && !CY_WECHAT

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CYFramework.Infrastructure;
using UnityEngine;
using UnityEngine.Networking;

namespace CYFramework.Platform.Unity
{
    /// <summary>
    /// Unity 平台网络适配器
    /// </summary>
    public class UnityNetworkAdapter : INetworkAdapter
    {
        private bool _isConnected = true;
        private string _networkType = "unknown";
        
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
        
        public bool IsConnected => _isConnected;
        public string NetworkType => _networkType;
        
        public void Initialize()
        {
            UpdateNetworkType();
            CYLog.Debug($"[UnityNetworkAdapter] 初始化完成，网络类型: {_networkType}");
        }
        
        /// <summary>
        /// HTTP GET 请求
        /// </summary>
        public async Task<string> HttpGet(string url, int timeout = 10)
        {
            using var request = UnityWebRequest.Get(url);
            request.timeout = timeout;
            
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(request.error);
            }
            
            return request.downloadHandler.text;
        }
        
        /// <summary>
        /// HTTP POST 请求
        /// </summary>
        public async Task<string> HttpPost(string url, string body, string contentType = "application/json", int timeout = 10)
        {
            using var request = new UnityWebRequest(url, "POST");
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            request.timeout = timeout;
            
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(request.error);
            }
            
            return request.downloadHandler.text;
        }
        
        /// <summary>
        /// 创建 WebSocket
        /// </summary>
        public IWebSocket CreateWebSocket(string url)
        {
            return new UnityWebSocket(url);
        }
        
        /// <summary>
        /// 更新网络类型
        /// </summary>
        private void UpdateNetworkType()
        {
            _networkType = Application.internetReachability switch
            {
                NetworkReachability.ReachableViaLocalAreaNetwork => "wifi",
                NetworkReachability.ReachableViaCarrierDataNetwork => "4g",
                _ => "none"
            };
            _isConnected = _networkType != "none";
        }
    }
    
    /// <summary>
    /// Unity WebSocket 实现
    /// 使用 System.Net.WebSockets（支持 .NET Standard 2.1）
    /// </summary>
    public class UnityWebSocket : IWebSocket
    {
        private readonly string _url;
        private System.Net.WebSockets.ClientWebSocket _webSocket;
        private WebSocketState _state = WebSocketState.Closed;
        private System.Threading.CancellationTokenSource _cts;
        private readonly byte[] _receiveBuffer = new byte[8192];
        
        public WebSocketState State => _state;
        
        public event Action<string> OnMessage;
        public event Action<byte[]> OnBinaryMessage;
        public event Action OnOpen;
        public event Action<string> OnClose;
        public event Action<string> OnError;
        
        public UnityWebSocket(string url)
        {
            _url = url;
        }
        
        /// <summary>
        /// 连接服务器
        /// </summary>
        public async Task Connect()
        {
            if (_state == WebSocketState.Open || _state == WebSocketState.Connecting)
            {
                return;
            }
            
            _state = WebSocketState.Connecting;
            _webSocket = new System.Net.WebSockets.ClientWebSocket();
            _cts = new System.Threading.CancellationTokenSource();
            
            try
            {
                await _webSocket.ConnectAsync(new Uri(_url), _cts.Token);
                _state = WebSocketState.Open;
                OnOpen?.Invoke();
                
                CYLog.Debug($"[UnityWebSocket] 连接成功: {_url}");
                
                // 开始接收消息
                _ = ReceiveLoop();
            }
            catch (Exception ex)
            {
                _state = WebSocketState.Closed;
                OnError?.Invoke(ex.Message);
                CYLog.Error($"[UnityWebSocket] 连接失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 发送文本消息
        /// </summary>
        public void Send(string message)
        {
            if (_state != WebSocketState.Open)
            {
                CYLog.Warning("[UnityWebSocket] 连接未打开");
                return;
            }
            
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                var segment = new ArraySegment<byte>(bytes);
                _ = _webSocket.SendAsync(segment, System.Net.WebSockets.WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
            }
        }
        
        /// <summary>
        /// 发送二进制消息
        /// </summary>
        public void Send(byte[] data)
        {
            if (_state != WebSocketState.Open)
            {
                CYLog.Warning("[UnityWebSocket] 连接未打开");
                return;
            }
            
            try
            {
                var segment = new ArraySegment<byte>(data);
                _ = _webSocket.SendAsync(segment, System.Net.WebSockets.WebSocketMessageType.Binary, true, _cts.Token);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
            }
        }
        
        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            if (_state == WebSocketState.Closed || _state == WebSocketState.Closing)
            {
                return;
            }
            
            _state = WebSocketState.Closing;
            
            try
            {
                _cts?.Cancel();
                
                if (_webSocket?.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    _ = _webSocket.CloseAsync(
                        System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                        "Client closed",
                        System.Threading.CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                CYLog.Warning($"[UnityWebSocket] 关闭异常: {ex.Message}");
            }
            finally
            {
                _state = WebSocketState.Closed;
                OnClose?.Invoke("正常关闭");
                
                _webSocket?.Dispose();
                _webSocket = null;
                _cts?.Dispose();
                _cts = null;
            }
        }
        
        /// <summary>
        /// 消息接收循环
        /// </summary>
        private async Task ReceiveLoop()
        {
            var buffer = new ArraySegment<byte>(_receiveBuffer);
            
            try
            {
                while (_webSocket?.State == System.Net.WebSockets.WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(buffer, _cts.Token);
                    
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                    {
                        _state = WebSocketState.Closed;
                        OnClose?.Invoke(result.CloseStatusDescription ?? "Server closed");
                        break;
                    }
                    
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(_receiveBuffer, 0, result.Count);
                        OnMessage?.Invoke(message);
                    }
                    else if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Binary)
                    {
                        var data = new byte[result.Count];
                        Array.Copy(_receiveBuffer, 0, data, 0, result.Count);
                        OnBinaryMessage?.Invoke(data);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                if (_state != WebSocketState.Closed)
                {
                    OnError?.Invoke(ex.Message);
                    _state = WebSocketState.Closed;
                    OnClose?.Invoke(ex.Message);
                }
            }
        }
    }
}

#endif // !UNITY_WEBGL && !CY_WECHAT
