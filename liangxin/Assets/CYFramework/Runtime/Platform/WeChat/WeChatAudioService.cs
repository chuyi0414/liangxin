// ============================================================================
// CYFramework 2.2 - 微信音频服务
// 文档位置：3.1.7 音频系统 - 微信端特供处理
// 使用 wx.createInnerAudioContext (BGM) + WebAudio API (SFX)
// ============================================================================

#if CY_WECHAT || UNITY_WEBGL

using System;
using System.Runtime.InteropServices;
using CYFramework.Infrastructure;
using CYFramework.Core.Audio;

namespace CYFramework.Platform.WeChat
{
    /// <summary>
    /// 微信音频服务
    /// 文档：
    /// - BGM：wx.createInnerAudioContext()，流式加载，省内存
    /// - SFX：WebAudio API，快速触发，高频短音效
    /// - 实例复用：禁止频繁创建 AudioContext
    /// </summary>
    public class WeChatAudioService : IAudioService, IInitializable, IPausable, IDisposableEx
    {
        // SFX 池大小
        private const int SFX_POOL_SIZE = 16;
        
        // 音量
        private float _masterVolume = 1f;
        /// <summary>
        /// BGM 音量
        /// </summary>
        private float _bgmVolume = 0.8f;
        /// <summary>
        /// SFX 音量
        /// </summary>
        private float _sfxVolume = 1f;
        /// <summary>
        /// 是否静音
        /// </summary>
        private bool _isMuted;
        
        // 音频解锁状态
        private bool _audioUnlocked;
        
        // 当前 BGM
        private string _currentBGM;
        /// <summary>
        /// BGM 是否暂停
        /// </summary>
        private bool _isBGMPaused;
        
        /// <summary>
        /// 是否静音
        /// </summary>
        public bool IsMuted => _isMuted;
        
        /// <summary>
        /// 初始化优先级
        /// </summary>
        public int InitOrder => 30;
        /// <summary>
        /// 销毁优先级
        /// </summary>
        public int DisposeOrder => 30;
        
        #region JS 桥接
        
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        /// <summary>
        /// 初始化音频
        /// </summary>
        private static extern void WX_InitAudio(int sfxPoolSize);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 播放 BGM
        /// </summary>
        private static extern void WX_PlayBGM(string name, float volume, bool loop);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 停止 BGM
        /// </summary>
        private static extern void WX_StopBGM(float fadeOut);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 暂停 BGM
        /// </summary>
        private static extern void WX_PauseBGM();
        
        [DllImport("__Internal")]
        /// <summary>
        /// 恢复 BGM
        /// </summary>
        private static extern void WX_ResumeBGM();
        
        [DllImport("__Internal")]
        /// <summary>
        /// 播放 SFX
        /// </summary>
        private static extern void WX_PlaySFX(string name, float volume);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 设置主音量
        /// </summary>
        private static extern void WX_SetMasterVolume(float volume);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 设置静音
        /// </summary>
        private static extern void WX_Mute(bool mute);
        
        [DllImport("__Internal")]
        /// <summary>
        /// 解锁音频
        /// </summary>
        private static extern void WX_UnlockAudio();
        
        [DllImport("__Internal")]
        /// <summary>
        /// 暂停所有音频
        /// </summary>
        private static extern void WX_PauseAllAudio();
        
        [DllImport("__Internal")]
        /// <summary>
        /// 恢复所有音频
        /// </summary>
        private static extern void WX_ResumeAllAudio();
        
        [DllImport("__Internal")]
        /// <summary>
        /// 销毁音频资源
        /// </summary>
        private static extern void WX_DisposeAudio();
#endif
        
        #endregion
        
        #region 生命周期
        
        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_InitAudio(SFX_POOL_SIZE);
#endif
            CYLog.Debug($"[WeChatAudioService] 初始化完成，SFX 池大小: {SFX_POOL_SIZE}");
        }
        
        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_DisposeAudio();
#endif
            CYLog.Debug("[WeChatAudioService] 已销毁");
        }
        
        /// <summary>
        /// 暂停音频
        /// 文档：微信小游戏切后台必须静音，否则审核不通过
        /// </summary>
        public void OnPause()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_PauseAllAudio();
#endif
            CYLog.Debug("[WeChatAudioService] 音频已暂停");
        }
        
        /// <summary>
        /// 恢复回调
        /// </summary>
        public void OnResume(float pauseDuration)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_ResumeAllAudio();
#endif
            CYLog.Debug("[WeChatAudioService] 音频已恢复");
        }
        
        #endregion
        
        #region IAudioService
        
        /// <summary>
        /// 播放 BGM
        /// </summary>
        public void PlayBGM(string name, float volume = 1f, bool loop = true)
        {
            if (string.IsNullOrEmpty(name)) return;
            
            // 同一曲目不重复播放
            if (_currentBGM == name) return;
            
            TryUnlockAudio();
            
            _currentBGM = name;
            // 最终音量
            float finalVolume = _bgmVolume * volume * _masterVolume;
            
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_PlayBGM(name, finalVolume, loop);
#else
            CYLog.Debug($"[WeChatAudioService] 播放 BGM: {name}");
#endif
            
            _isBGMPaused = false;
        }
        
        /// <summary>
        /// 停止 BGM
        /// </summary>
        public void StopBGM(float fadeOut = 0.5f)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_StopBGM(fadeOut);
#endif
            _currentBGM = null;
            CYLog.Debug("[WeChatAudioService] 停止 BGM");
        }
        
        /// <summary>
        /// 暂停 BGM
        /// </summary>
        public void PauseBGM()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_PauseBGM();
#endif
            _isBGMPaused = true;
        }
        
        /// <summary>
        /// 恢复 BGM
        /// </summary>
        public void ResumeBGM()
        {
            if (!_isBGMPaused) return;
            
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_ResumeBGM();
#endif
            _isBGMPaused = false;
        }
        
        /// <summary>
        /// 播放音效
        /// </summary>
        public void PlaySFX(string name, float volume = 1f)
        {
            if (string.IsNullOrEmpty(name)) return;
            
            TryUnlockAudio();
            
            // 最终音量
            float finalVolume = _sfxVolume * volume * _masterVolume;
            
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_PlaySFX(name, finalVolume);
#else
            CYLog.Debug($"[WeChatAudioService] 播放 SFX: {name}");
#endif
        }
        
        /// <summary>
        /// 设置主音量
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            _masterVolume = UnityEngine.Mathf.Clamp01(volume);
            
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_SetMasterVolume(_masterVolume);
#endif
        }
        
        /// <summary>
        /// 设置 BGM 音量
        /// </summary>
        public void SetBGMVolume(float volume)
        {
            _bgmVolume = UnityEngine.Mathf.Clamp01(volume);
        }
        
        /// <summary>
        /// 设置 SFX 音量
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            _sfxVolume = UnityEngine.Mathf.Clamp01(volume);
        }
        
        /// <summary>
        /// 静音开关
        /// </summary>
        public void Mute(bool mute)
        {
            _isMuted = mute;
            
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_Mute(mute);
#endif
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 尝试解锁音频
        /// 文档：iOS 不允许自动播放音频，需用户交互触发
        /// </summary>
        private void TryUnlockAudio()
        {
            if (_audioUnlocked) return;
            
#if UNITY_WEBGL && !UNITY_EDITOR
            WX_UnlockAudio();
#endif
            
            _audioUnlocked = true;
            CYLog.Debug("[WeChatAudioService] 音频已解锁");
        }
        
        #endregion
    }
}

#endif

