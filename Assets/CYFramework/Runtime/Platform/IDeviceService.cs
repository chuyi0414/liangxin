// ============================================================================
// CYFramework - 设备服务接口
// 平台抽象层：封装设备能力（震动、剪贴板、系统信息等）
// ============================================================================

using UnityEngine;

namespace CYFramework.Runtime.Platform
{
    /// <summary>
    /// 设备服务接口
    /// </summary>
    public interface IDeviceService
    {
        /// <summary>
        /// 设备唯一标识
        /// </summary>
        string DeviceId { get; }

        /// <summary>
        /// 设备型号
        /// </summary>
        string DeviceModel { get; }

        /// <summary>
        /// 操作系统
        /// </summary>
        string OperatingSystem { get; }

        /// <summary>
        /// 系统内存大小（MB）
        /// </summary>
        int SystemMemorySize { get; }

        /// <summary>
        /// 屏幕宽度
        /// </summary>
        int ScreenWidth { get; }

        /// <summary>
        /// 屏幕高度
        /// </summary>
        int ScreenHeight { get; }

        /// <summary>
        /// 是否支持震动
        /// </summary>
        bool SupportsVibration { get; }

        /// <summary>
        /// 触发震动
        /// </summary>
        /// <param name="duration">持续时间（毫秒），部分平台可能忽略此参数</param>
        void Vibrate(long duration = 100);

        /// <summary>
        /// 复制文本到剪贴板
        /// </summary>
        void CopyToClipboard(string text);

        /// <summary>
        /// 从剪贴板获取文本
        /// </summary>
        string GetClipboardText();

        /// <summary>
        /// 打开 URL
        /// </summary>
        void OpenURL(string url);

        /// <summary>
        /// 获取电池电量（0-1）
        /// </summary>
        float GetBatteryLevel();

        /// <summary>
        /// 是否正在充电
        /// </summary>
        bool IsCharging();

        /// <summary>
        /// 获取网络连接类型
        /// </summary>
        NetworkReachability GetNetworkReachability();
    }

    /// <summary>
    /// Unity 平台的设备服务实现
    /// </summary>
    public class UnityDeviceService : IDeviceService
    {
        public string DeviceId => SystemInfo.deviceUniqueIdentifier;
        public string DeviceModel => SystemInfo.deviceModel;
        public string OperatingSystem => SystemInfo.operatingSystem;
        public int SystemMemorySize => SystemInfo.systemMemorySize;
        public int ScreenWidth => Screen.width;
        public int ScreenHeight => Screen.height;

        public bool SupportsVibration
        {
            get
            {
            #if UNITY_ANDROID || UNITY_IOS
                return true;
            #else
                return false;
            #endif
            }
        }

        public void Vibrate(long duration = 100)
        {
        #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator != null)
                    {
                        vibrator.Call("vibrate", duration);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DeviceService] 震动失败: {e.Message}");
            }
        #elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
        #else
            Debug.Log($"[DeviceService] 震动 (模拟): {duration}ms");
        #endif
        }

        public void CopyToClipboard(string text)
        {
            GUIUtility.systemCopyBuffer = text;
        }

        public string GetClipboardText()
        {
            return GUIUtility.systemCopyBuffer;
        }

        public void OpenURL(string url)
        {
            Application.OpenURL(url);
        }

        public float GetBatteryLevel()
        {
            return SystemInfo.batteryLevel;
        }

        public bool IsCharging()
        {
            return SystemInfo.batteryStatus == BatteryStatus.Charging;
        }

        public NetworkReachability GetNetworkReachability()
        {
            return Application.internetReachability;
        }
    }
}
