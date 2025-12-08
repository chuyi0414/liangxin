// ============================================================================
// CYFramework 2.2 - 平台适配层接口定义
// 文档位置：Layer 1: Platform Adapter (平台适配层)
// 功能：定义跨平台抽象接口
// ============================================================================

using System;
using System.Threading.Tasks;

namespace CYFramework.Platform
{
    /// <summary>
    /// 平台类型
    /// </summary>
    public enum PlatformType
    {
        PC,
        Android,
        iOS,
        WebGL,
        WeChat
    }
    
    /// <summary>
    /// 平台适配器基础接口
    /// </summary>
    public interface IPlatformAdapter
    {
        /// <summary>
        /// 平台类型
        /// </summary>
        PlatformType Platform { get; }
        
        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize();
    }
    
    /// <summary>
    /// 文件系统适配器接口
    /// </summary>
    public interface IFileSystem : IPlatformAdapter
    {
        /// <summary>
        /// 持久化数据路径
        /// </summary>
        string PersistentDataPath { get; }
        
        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        bool FileExists(string path);
        
        /// <summary>
        /// 读取文本文件
        /// </summary>
        string ReadText(string path);
        
        /// <summary>
        /// 写入文本文件
        /// </summary>
        void WriteText(string path, string content);
        
        /// <summary>
        /// 读取二进制文件
        /// </summary>
        byte[] ReadBytes(string path);
        
        /// <summary>
        /// 写入二进制文件
        /// </summary>
        void WriteBytes(string path, byte[] data);
        
        /// <summary>
        /// 删除文件
        /// </summary>
        void DeleteFile(string path);
        
        /// <summary>
        /// 异步读取文件
        /// </summary>
        Task<byte[]> ReadBytesAsync(string path);
        
        /// <summary>
        /// 异步写入文件
        /// </summary>
        Task WriteBytesAsync(string path, byte[] data);
    }
    
    /// <summary>
    /// 网络适配器接口
    /// </summary>
    public interface INetworkAdapter : IPlatformAdapter
    {
        /// <summary>
        /// 是否联网
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// 网络类型（WiFi/4G/5G 等）
        /// </summary>
        string NetworkType { get; }
        
        /// <summary>
        /// HTTP GET 请求
        /// </summary>
        Task<string> HttpGet(string url, int timeout = 10);
        
        /// <summary>
        /// HTTP POST 请求
        /// </summary>
        Task<string> HttpPost(string url, string body, string contentType = "application/json", int timeout = 10);
        
        /// <summary>
        /// 创建 WebSocket 连接
        /// </summary>
        IWebSocket CreateWebSocket(string url);
    }
    
    /// <summary>
    /// WebSocket 接口
    /// </summary>
    public interface IWebSocket
    {
        /// <summary>
        /// 连接状态
        /// </summary>
        WebSocketState State { get; }
        
        /// <summary>
        /// 连接服务器
        /// </summary>
        Task Connect();
        
        /// <summary>
        /// 发送文本消息
        /// </summary>
        void Send(string message);
        
        /// <summary>
        /// 发送二进制消息
        /// </summary>
        void Send(byte[] data);
        
        /// <summary>
        /// 关闭连接
        /// </summary>
        void Close();
        
        /// <summary>
        /// 消息接收事件
        /// </summary>
        event Action<string> OnMessage;
        
        /// <summary>
        /// 二进制消息接收事件
        /// </summary>
        event Action<byte[]> OnBinaryMessage;
        
        /// <summary>
        /// 连接打开事件
        /// </summary>
        event Action OnOpen;
        
        /// <summary>
        /// 连接关闭事件
        /// </summary>
        event Action<string> OnClose;
        
        /// <summary>
        /// 错误事件
        /// </summary>
        event Action<string> OnError;
    }
    
    /// <summary>
    /// WebSocket 状态
    /// </summary>
    public enum WebSocketState
    {
        Connecting,
        Open,
        Closing,
        Closed
    }
    
    /// <summary>
    /// 存储适配器接口
    /// </summary>
    public interface IStorageAdapter : IPlatformAdapter
    {
        /// <summary>
        /// 存储上限（字节）
        /// </summary>
        long StorageLimit { get; }
        
        /// <summary>
        /// 已使用存储（字节）
        /// </summary>
        long StorageUsed { get; }
        
        /// <summary>
        /// 获取字符串
        /// </summary>
        string GetString(string key, string defaultValue = "");
        
        /// <summary>
        /// 设置字符串
        /// </summary>
        void SetString(string key, string value);
        
        /// <summary>
        /// 获取整数
        /// </summary>
        int GetInt(string key, int defaultValue = 0);
        
        /// <summary>
        /// 设置整数
        /// </summary>
        void SetInt(string key, int value);
        
        /// <summary>
        /// 获取浮点数
        /// </summary>
        float GetFloat(string key, float defaultValue = 0f);
        
        /// <summary>
        /// 设置浮点数
        /// </summary>
        void SetFloat(string key, float value);
        
        /// <summary>
        /// 检查键是否存在
        /// </summary>
        bool HasKey(string key);
        
        /// <summary>
        /// 删除键
        /// </summary>
        void DeleteKey(string key);
        
        /// <summary>
        /// 清空所有数据
        /// </summary>
        void DeleteAll();
        
        /// <summary>
        /// 保存（立即写入）
        /// </summary>
        void Save();
    }
    
    /// <summary>
    /// 音频适配器接口
    /// </summary>
    public interface IAudioAdapter : IPlatformAdapter
    {
        /// <summary>
        /// 是否需要解锁（iOS WebAudio 限制）
        /// </summary>
        bool NeedsUnlock { get; }
        
        /// <summary>
        /// 是否已解锁
        /// </summary>
        bool IsUnlocked { get; }
        
        /// <summary>
        /// 尝试解锁音频
        /// </summary>
        void TryUnlock();
        
        /// <summary>
        /// 播放 BGM
        /// </summary>
        void PlayBGM(string path, float volume = 1f, bool loop = true);
        
        /// <summary>
        /// 停止 BGM
        /// </summary>
        void StopBGM(float fadeOut = 0.5f);
        
        /// <summary>
        /// 暂停 BGM
        /// </summary>
        void PauseBGM();
        
        /// <summary>
        /// 恢复 BGM
        /// </summary>
        void ResumeBGM();
        
        /// <summary>
        /// 播放音效
        /// </summary>
        void PlaySFX(string path, float volume = 1f);
        
        /// <summary>
        /// 设置主音量
        /// </summary>
        void SetMasterVolume(float volume);
        
        /// <summary>
        /// 静音
        /// </summary>
        void Mute(bool mute);
        
        /// <summary>
        /// 暂停所有音频
        /// </summary>
        void PauseAll();
        
        /// <summary>
        /// 恢复所有音频
        /// </summary>
        void ResumeAll();
    }
    
    /// <summary>
    /// 震动适配器接口
    /// </summary>
    public interface IVibrationAdapter : IPlatformAdapter
    {
        /// <summary>
        /// 是否支持震动
        /// </summary>
        bool IsSupported { get; }
        
        /// <summary>
        /// 短震动
        /// </summary>
        void VibrateShort();
        
        /// <summary>
        /// 长震动
        /// </summary>
        void VibrateLong();
        
        /// <summary>
        /// 自定义震动
        /// </summary>
        void Vibrate(int milliseconds);
    }
}
