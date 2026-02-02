// ============================================================================
// CYFramework 2.2 - 微信网络适配器
// 文档位置：3.1.3 网络层 - 微信适配
// 使用 wx.request / wx.connectSocket（不使用 System.Net.Sockets）
// ============================================================================

#if CY_WECHAT || UNITY_WEBGL

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CYFramework.Infrastructure;

namespace CYFramework.Platform.WeChat
{
    /// <summary>
    /// 微信网络适配器
    /// 使用 wx.request / wx.connectSocket
    /// </summary>
    public class WeChatNetworkAdapter : INetworkAdapter
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
        public PlatformType Platform => PlatformType.WeChat;
        /// <summary>
        /// 是否联网
        /// </summary>
        public bool IsConnected => _isConnected;
        /// <summary>
        /// 网络类型
        /// </summary>
        public string NetworkType => _networkType;
        
        #region JS 桥接
        
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        /// <summary>
        /// HTTP 请求
        /// </summary>
        private static extern void WX_HttpRequest(string url, string method, string data, string headers, int callbackId);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 连接 WebSocket
        /// </summary>
        private static extern void WX_ConnectSocket(string url, int callbackId);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 发送 WebSocket 消息
        /// </summary>
        private static extern void WX_SendSocketMessage(string message);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 关闭 WebSocket
        /// </summary>
        private static extern void WX_CloseSocket();
        
        [DllImport("__Internal")]
        /// <summary>
        /// 获取网络类型
        /// </summary>
        private static extern string WX_GetNetworkType();
#endif
        
        #endregion
        
        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            UpdateNetworkType();
            CYLog.Debug($"[WeChatNetworkAdapter] 初始化完成，网络类型: {_networkType}");
        }
        
        /// <summary>
        /// HTTP GET 请求
        /// 文档：使用 wx.request
        /// </summary>
        public async Task<string> HttpGet(string url, int timeout = 10)
        {
            return await HttpRequest(url, "GET", null, timeout);
        }
        
        /// <summary>
        /// HTTP POST 请求
        /// 文档：使用 wx.request
        /// </summary>
        public async Task<string> HttpPost(string url, string body, string contentType = "application/json", int timeout = 10)
        {
            return await HttpRequest(url, "POST", body, timeout);
        }
        
        /// <summary>
        /// 创建 WebSocket
        /// 文档：使用 wx.connectSocket
        /// </summary>
        public IWebSocket CreateWebSocket(string url)
        {
            return new WeChatWebSocket(url);
        }
        
        /// <summary>
        /// HTTP 请求实现
        /// </summary>
        private async Task<string> HttpRequest(string url, string method, string body, int timeout)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // 使用 JS 桥接调用 wx.request
            // 任务完成源
            var tcs = new TaskCompletionSource<string>();
            // 回调 ID
            int callbackId = WeChatCallbackManager.Register(result => tcs.TrySetResult(result));
            
            WX_HttpRequest(url, method, body ?? "", "{}", callbackId);
            
            // 超时处理
            // 超时任务
            var timeoutTask = Task.Delay(timeout * 1000);
            // 完成任务
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                WeChatCallbackManager.Unregister(callbackId);
                throw new TimeoutException($"请求超时: {url}");
            }
            
            return await tcs.Task;
#else
            // Editor 模式：使用 UnityWebRequest 模拟
            // 请求对象
            using var request = method == "GET" 
                ? UnityEngine.Networking.UnityWebRequest.Get(url)
                : BuildJsonPost(url, body, contentType);
            
            request.timeout = timeout;
            // 请求操作
            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                throw new Exception(request.error);
            }
            
            return request.downloadHandler.text;
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        /// <summary>
        /// 构建 JSON POST 请求
        /// </summary>
        private static UnityEngine.Networking.UnityWebRequest BuildJsonPost(string url, string body, string contentType)
        {
            // 请求对象
            var request = new UnityEngine.Networking.UnityWebRequest(url, "POST");
            // 请求体字节
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body ?? "");
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", string.IsNullOrEmpty(contentType) ? "application/json" : contentType);
            return request;
        }
#endif
        
        /// <summary>
        /// 更新网络类型
        /// </summary>
        private void UpdateNetworkType()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _networkType = WX_GetNetworkType();
#else
            _networkType = UnityEngine.Application.internetReachability switch
            {
                UnityEngine.NetworkReachability.ReachableViaLocalAreaNetwork => "wifi",
                UnityEngine.NetworkReachability.ReachableViaCarrierDataNetwork => "4g",
                _ => "none"
            };
#endif
            _isConnected = _networkType != "none";
        }
    }
    
    /// <summary>
    /// 微信 WebSocket 实现
    /// 文档：使用 wx.connectSocket
    /// </summary>
    public class WeChatWebSocket : IWebSocket
    {
        /// <summary>
        /// 连接地址
        /// </summary>
        private readonly string _url;
        /// <summary>
        /// 当前连接状态
        /// </summary>
        private WebSocketState _state = WebSocketState.Closed;
        
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
        public WeChatWebSocket(string url)
        {
            _url = url;
        }
        
        /// <summary>
        /// 连接服务器
        /// </summary>
        public async Task Connect()
        {
            _state = WebSocketState.Connecting;
            
#if UNITY_WEBGL && !UNITY_EDITOR
            // 任务完成源
            var tcs = new TaskCompletionSource<bool>();
            // 回调 ID
            int callbackId = WeChatCallbackManager.Register(result => {
                if (result == "open")
                {
                    _state = WebSocketState.Open;
                    OnOpen?.Invoke();
                    tcs.TrySetResult(true);
                }
                else
                {
                    _state = WebSocketState.Closed;
                    OnError?.Invoke(result);
                    tcs.TrySetResult(false);
                }
            });
            
            WX_ConnectSocket(_url, callbackId);
            await tcs.Task;
#else
            // Editor 模拟
            await Task.Delay(100);
            _state = WebSocketState.Open;
            OnOpen?.Invoke();
#endif
        }
        
        /// <summary>
        /// 发送文本消息
        /// </summary>
        public void Send(string message)
        {
            if (_state != WebSocketState.Open)
            {
                CYLog.Warning("[WeChatWebSocket] 连接未打开");
                return;
            }
            
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_SendSocketMessage(message);
#else
            CYLog.Debug($"[WeChatWebSocket] 发送消息: {message}");
#endif
        }
        
        /// <summary>
        /// 发送二进制消息
        /// </summary>
        public void Send(byte[] data)
        {
            // 微信 WebSocket 发送二进制需要特殊处理：这里使用 base64 发送，JS 侧需解码
            if (_state != WebSocketState.Open)
            {
                CYLog.Warning("[WeChatWebSocket] 连接未打开");
                return;
            }
            
#if UNITY_WEBGL && !UNITY_EDITOR
            // base64 文本
            string base64 = Convert.ToBase64String(data);
            WX_SendSocketMessage(base64);
#else
            CYLog.Debug($"[WeChatWebSocket] 发送二进制(base64): {Convert.ToBase64String(data)}");
#endif
        }
        
        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            _state = WebSocketState.Closing;
            
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_CloseSocket();
#endif
            
            _state = WebSocketState.Closed;
            OnClose?.Invoke("正常关闭");
        }
        
        /// <summary>
        /// JS 回调：收到消息
        /// </summary>
        public void OnMessageReceived(string message)
        {
            OnMessage?.Invoke(message);
        }
        
        /// <summary>
        /// JS 回调：连接关闭
        /// </summary>
        public void OnConnectionClosed(string reason)
        {
            _state = WebSocketState.Closed;
            OnClose?.Invoke(reason);
        }
    }
    
    /// <summary>
    /// 微信回调管理器
    /// </summary>
    public static class WeChatCallbackManager
    {
        /// <summary>
        /// 回调表
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<int, Action<string>> _callbacks = new();
        /// <summary>
        /// 回调自增 ID
        /// </summary>
        private static int _nextId = 1;
        
        /// <summary>
        /// 注册回调
        /// </summary>
        public static int Register(Action<string> callback)
        {
            // 回调 ID
            int id = _nextId++;
            _callbacks[id] = callback;
            return id;
        }
        
        /// <summary>
        /// 注销回调
        /// </summary>
        public static void Unregister(int id)
        {
            _callbacks.Remove(id);
        }
        
        /// <summary>
        /// 供 JS 调用
        /// </summary>
        public static void Invoke(int id, string result)
        {
            if (_callbacks.TryGetValue(id, out var callback)) // callback 为回调函数
            {
                callback?.Invoke(result);
                _callbacks.Remove(id);
            }
        }
    }
}

#endif
