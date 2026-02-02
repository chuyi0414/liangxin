// ============================================================================
// CYFramework 2.2 - Unity 震动适配器
// 适用平台：PC / Android / iOS（不支持 WebGL/微信小游戏）
// ============================================================================

#if !UNITY_WEBGL && !CY_WECHAT

using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Platform.Unity
{
    /// <summary>
    /// Unity 平台震动实现
    /// </summary>
    public class UnityVibrationAdapter : IVibrationAdapter
    {
        /// <summary>
        /// 是否支持震动
        /// </summary>
        private bool _isSupported;
        
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
        /// 是否支持震动
        /// </summary>
        public bool IsSupported => _isSupported;
        
        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            // PC 不支持震动，移动端支持
#if UNITY_ANDROID || UNITY_IOS
            _isSupported = true;
#else
            _isSupported = false;
#endif
            CYLog.Debug($"[UnityVibrationAdapter] 初始化完成，震动支持: {_isSupported}");
        }
        
        /// <summary>
        /// 短震动 (约 15ms)
        /// </summary>
        public void VibrateShort()
        {
            if (!_isSupported) return;
            
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(15);
#elif UNITY_IOS && !UNITY_EDITOR
            VibrateIOS(1519); // kSystemSoundID_Vibrate_Light
#endif
        }
        
        /// <summary>
        /// 长震动 (约 400ms)
        /// </summary>
        public void VibrateLong()
        {
            if (!_isSupported) return;
            
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(400);
#elif UNITY_IOS && !UNITY_EDITOR
            VibrateIOS(4095); // kSystemSoundID_Vibrate
#endif
        }
        
        /// <summary>
        /// 自定义震动
        /// </summary>
        public void Vibrate(int milliseconds)
        {
            if (!_isSupported) return;
            
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(milliseconds);
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS 不支持自定义时长，根据时长选择模式
            if (milliseconds < 50)
                VibrateIOS(1519); // Light
            else if (milliseconds < 200)
                VibrateIOS(1520); // Medium
            else
                VibrateIOS(4095); // Heavy
#endif
        }
        
#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Android 震动服务对象
        /// </summary>
        private static AndroidJavaObject _vibrator;
        /// <summary>
        /// Android 震动服务类（预留）
        /// </summary>
        private static AndroidJavaClass _vibratorClass;
        
        /// <summary>
        /// Android 震动实现
        /// </summary>
        private static void VibrateAndroid(long milliseconds)
        {
            try
            {
                if (_vibrator == null)
                {
                    // Unity Player 类
                    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    // Unity 当前 Activity
                    using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                
                if (Application.platform == RuntimePlatform.Android)
                {
                    // Android 8.0+ 使用 VibrationEffect
                    if (GetAndroidSDKVersion() >= 26)
                    {
                        // VibrationEffect 类
                        using var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                        // 震动效果对象
                        using var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", milliseconds, -1); // -1 = DEFAULT_AMPLITUDE
                        _vibrator.Call("vibrate", effect);
                    }
                    else
                    {
                        _vibrator.Call("vibrate", milliseconds);
                    }
                }
            }
            catch (System.Exception ex)
            {
                // ex 为震动异常
                CYLog.Warning($"[UnityVibrationAdapter] Android 震动失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取 Android SDK 版本
        /// </summary>
        private static int GetAndroidSDKVersion()
        {
            // Android 版本类
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            return version.GetStatic<int>("SDK_INT");
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        /// <summary>
        /// iOS 系统震动接口
        /// </summary>
        private static extern void AudioServicesPlaySystemSound(uint systemSoundID);
        
        /// <summary>
        /// iOS 震动实现
        /// </summary>
        private static void VibrateIOS(uint soundId)
        {
            try
            {
                AudioServicesPlaySystemSound(soundId);
            }
            catch (System.Exception ex)
            {
                // ex 为震动异常
                CYLog.Warning($"[UnityVibrationAdapter] iOS 震动失败: {ex.Message}");
            }
        }
#endif
    }
}

#endif // !UNITY_WEBGL && !CY_WECHAT
