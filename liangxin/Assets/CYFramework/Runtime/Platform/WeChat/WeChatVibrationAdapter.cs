// ============================================================================
// CYFramework 2.2 - 微信震动适配器
// 使用 wx.vibrateShort / wx.vibrateLong
// ============================================================================

#if CY_WECHAT || UNITY_WEBGL

using System.Runtime.InteropServices;
using CYFramework.Infrastructure;

namespace CYFramework.Platform.WeChat
{
    /// <summary>
    /// 微信小游戏震动实现
    /// 使用 wx.vibrateShort / wx.vibrateLong
    /// </summary>
    public class WeChatVibrationAdapter : IVibrationAdapter
    {
        /// <summary>
        /// 是否支持震动
        /// </summary>
        private bool _isSupported = true;
        
        /// <summary>
        /// 平台类型
        /// </summary>
        public PlatformType Platform => PlatformType.WeChat;
        /// <summary>
        /// 是否支持震动
        /// </summary>
        public bool IsSupported => _isSupported;
        
        #region JS 桥接
        
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        /// <summary>
        /// 短震动接口
        /// </summary>
        private static extern void WX_VibrateShort(string type);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 长震动接口
        /// </summary>
        private static extern void WX_VibrateLong();
#endif
        
        #endregion
        
        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            CYLog.Debug("[WeChatVibrationAdapter] 初始化完成");
        }
        
        /// <summary>
        /// 短震动 (约 15ms)
        /// type: heavy/medium/light
        /// </summary>
        public void VibrateShort()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                WX_VibrateShort("medium");
            }
            catch (System.Exception ex)
            {
                // ex 为震动异常
                CYLog.Warning($"[WeChatVibrationAdapter] 短震动失败: {ex.Message}");
            }
#else
            CYLog.Trace("[WeChatVibrationAdapter] Editor 模式: VibrateShort");
#endif
        }
        
        /// <summary>
        /// 长震动 (约 400ms)
        /// </summary>
        public void VibrateLong()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                WX_VibrateLong();
            }
            catch (System.Exception ex)
            {
                // ex 为震动异常
                CYLog.Warning($"[WeChatVibrationAdapter] 长震动失败: {ex.Message}");
            }
#else
            CYLog.Trace("[WeChatVibrationAdapter] Editor 模式: VibrateLong");
#endif
        }
        
        /// <summary>
        /// 自定义震动
        /// 微信不支持自定义时长，根据时长自动选择
        /// </summary>
        public void Vibrate(int milliseconds)
        {
            if (milliseconds < 100)
            {
                VibrateShort();
            }
            else
            {
                VibrateLong();
            }
        }
    }
}

#endif // CY_WECHAT || UNITY_WEBGL
