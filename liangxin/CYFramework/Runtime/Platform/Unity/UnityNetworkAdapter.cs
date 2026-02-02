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
        /// <summary>
        /// 当前是否联网
        /// </summary>
        private bool _isConnected = true;
        /// <summary>
        /// 当前网络类型文本
        /// </summary>
        private string _networkType = "unknown";
        
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
        /// 是否联网
        /// </summary>
        public bool IsConnected => _isConnected;
        /// <summary>
        /// 网络类型
        /// </summary>
        public string NetworkType => _networkType;
        
        /// <summary>
        /// 初始化
        /// </summary>
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
            // 请求对象
            using var request = UnityWebRequest.Get(url);
            request.timeout = timeout;
            
            // 请求操作
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
            // 请求对象
            using var request = new UnityWebRequest(url, "POST");
            // 请求体字节
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            request.timeout = timeout;
            
            // 请求操作
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
        /// <summary>
        /// 连接地址
        /// </summary>
        private readonly string _url;
        /// <summary>
        /// WebSocket 客户端
        /// </summary>
        private System.Net.WebSockets.ClientWebSocket _webSocket;
        /// <summary>
        /// 当前连接状态
        /// </summary>
        private WebSocketState _state = WebSocketState.Closed;
        /// <summary>
        /// 取消令牌源
        /// </summary>
        private System.Threading.CancellationTokenSource _cts;
        /// <summary>
        /// 接收缓冲区
        /// </summary>
        private readonly byte[] _receiveBuffer = new byte[8192];
        
        /// <summary>
        /// WebSocket 状态
        /// </summary>
        public WebSocketState State => _state;
        
        /// <summary>
        /// 文本消息事件
        /// </summary>
        public event Action<string> OnMessage;
        /// <summary>
        /// 二进制消息事件
        /// </summary>
        public event Action<byte[]> OnBinaryMessage;
        /// <summary>
        /// 连接打开事件
        /// </summary>
        public event Action OnOpen;
        /// <summary>
        /// 连接关闭事件
        /// </summary>
        public event Action<string> OnClose;
        /// <summary>
        /// 错误事件
        /// </summary>
        public event Action<string> OnError;
        
        /// <summary>
        /// 构造函数
        /// </summary>
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
                // ex 为连接异常
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
                // 文本字节
                var bytes = Encoding.UTF8.GetBytes(message);
                // 发送分段
                var segment = new ArraySegment<byte>(bytes);
                _ = _webSocket.SendAsync(segment, System.Net.WebSockets.WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception ex)
            {
                // ex 为发送异常
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
                // 发送分段
                var segment = new ArraySegment<byte>(data);
                _ = _webSocket.SendAsync(segment, System.Net.WebSockets.WebSocketMessageType.Binary, true, _cts.Token);
            }
            catch (Exception ex)
            {
                // ex 为发送异常
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
                // ex 为关闭异常
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
            // 接收缓冲区分段
            var buffer = new ArraySegment<byte>(_receiveBuffer);
            
            try
            {
                while (_webSocket?.State == System.Net.WebSockets.WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    // 接收结果
                    var result = await _webSocket.ReceiveAsync(buffer, _cts.Token);
                    
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                    {
                        _state = WebSocketState.Closed;
                        OnClose?.Invoke(result.CloseStatusDescription ?? "Server closed");
                        break;
                    }
                    
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                    {
                        // 文本消息
                        var message = Encoding.UTF8.GetString(_receiveBuffer, 0, result.Count);
                        OnMessage?.Invoke(message);
                    }
                    else if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Binary)
                    {
                        // 二进制消息
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
                // ex 为接收异常
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
