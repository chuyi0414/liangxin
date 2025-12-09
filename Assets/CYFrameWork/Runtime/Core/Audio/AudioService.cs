// ============================================================================
// CYFramework 2.2 - 音频服务
// 文档位置：3.1.7 音频系统 (Audio System)
// 功能：跨平台音频、微信特供处理、生命周期挂起
// ============================================================================

using System;
using System.Collections.Generic;
using CYFramework.Core.Pool;
using CYFramework.Infrastructure;
using CYFramework.Platform;
using UnityEngine;

namespace CYFramework.Core.Audio
{
    /// <summary>
    /// 音频服务接口
    /// </summary>
    public interface IAudioService
    {
        void PlayBGM(string name, float volume = 1f, bool loop = true);
        void StopBGM(float fadeOut = 0.5f);
        void PauseBGM();
        void ResumeBGM();
        void PlaySFX(string name, float volume = 1f);
        void SetMasterVolume(float volume);
        void SetBGMVolume(float volume);
        void SetSFXVolume(float volume);
        void Mute(bool mute);
        bool IsMuted { get; }
    }
    
    /// <summary>
    /// 音频配置
    /// </summary>
    [Serializable]
    public class AudioConfig
    {
        /// <summary>
        /// SFX 池大小
        /// </summary>
        public int SFXPoolSize = 16;
        
        /// <summary>
        /// 默认 BGM 音量
        /// </summary>
        public float DefaultBGMVolume = 0.8f;
        
        /// <summary>
        /// 默认 SFX 音量
        /// </summary>
        public float DefaultSFXVolume = 1f;
        
        /// <summary>
        /// BGM 淡出时间
        /// </summary>
        public float BGMFadeTime = 0.5f;
    }
    
    /// <summary>
    /// Unity 平台音频服务实现
    /// </summary>
    public class UnityAudioService : IAudioService, IInitializable, IPausable, IDisposableEx
    {
        private AudioConfig _config;
        
        // BGM
        private AudioSource _bgmSource;
        private string _currentBGM;
        private float _bgmVolume;
        private bool _isBGMPaused;
        
        // SFX 池
        private readonly List<AudioSource> _sfxPool = new();
        private int _sfxPoolIndex;
        private float _sfxVolume;
        
        // 主音量
        private float _masterVolume = 1f;
        private bool _isMuted;
        
        // 音频解锁状态（iOS WebAudio 限制）
        private bool _audioUnlocked;
        
        // 资源缓存
        private readonly Dictionary<string, AudioClip> _clipCache = new();
        
        public bool IsMuted => _isMuted;
        
        public int InitOrder => 30;
        public int DisposeOrder => 30;
        
        /// <summary>
        /// 无参构造函数（ServiceLocator 需要）
        /// </summary>
        public UnityAudioService() : this(null) { }
        
        public UnityAudioService(AudioConfig config)
        {
            _config = config ?? new AudioConfig();
            _bgmVolume = _config.DefaultBGMVolume;
            _sfxVolume = _config.DefaultSFXVolume;
        }
        
        #region 生命周期
        
        public void Initialize()
        {
            // 创建音频根节点
            var audioRoot = new GameObject("AudioService");
            UnityEngine.Object.DontDestroyOnLoad(audioRoot);
            
            // 创建 BGM 源
            _bgmSource = audioRoot.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            
            // 创建 SFX 池
            for (int i = 0; i < _config.SFXPoolSize; i++)
            {
                var sfxSource = audioRoot.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                _sfxPool.Add(sfxSource);
            }
            
            UpdateVolumes();
            
            CYLog.Debug($"[AudioService] 初始化完成，SFX 池大小: {_config.SFXPoolSize}");
        }
        
        public void Dispose()
        {
            StopBGM(0);
            _clipCache.Clear();
            
            CYLog.Debug("[AudioService] 已销毁");
        }
        
        public void OnPause()
        {
            // 文档位置：3.1.7 生命周期挂起处理（微信审核红线）
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Pause();
            }
            
            foreach (var sfx in _sfxPool)
            {
                if (sfx.isPlaying)
                {
                    sfx.Pause();
                }
            }
            
            AudioListener.pause = true;
            CYLog.Debug("[AudioService] 音频已暂停");
        }
        
        public void OnResume(float pauseDuration)
        {
            AudioListener.pause = false;
            
            if (_bgmSource != null && !_isBGMPaused)
            {
                _bgmSource.UnPause();
            }
            
            // SFX 不自动恢复（通常是短音效）
            
            CYLog.Debug("[AudioService] 音频已恢复");
        }
        
        #endregion
        
        #region BGM
        
        public void PlayBGM(string name, float volume = 1f, bool loop = true)
        {
            if (string.IsNullOrEmpty(name)) return;
            
            // 同一曲目不重复播放
            if (_currentBGM == name && _bgmSource.isPlaying) return;
            
            // 尝试解锁音频
            TryUnlockAudio();
            
            var clip = LoadClip(name);
            if (clip == null)
            {
                CYLog.Warning($"[AudioService] BGM 加载失败: {name}");
                return;
            }
            
            _currentBGM = name;
            _bgmSource.clip = clip;
            _bgmSource.volume = _bgmVolume * volume * _masterVolume;
            _bgmSource.loop = loop;
            _bgmSource.Play();
            _isBGMPaused = false;
            
            CYLog.Debug($"[AudioService] 播放 BGM: {name}");
        }
        
        public void StopBGM(float fadeOut = 0.5f)
        {
            if (_bgmSource == null || !_bgmSource.isPlaying) return;
            
            // TODO: 实现淡出
            _bgmSource.Stop();
            _currentBGM = null;
            
            CYLog.Debug("[AudioService] 停止 BGM");
        }
        
        public void PauseBGM()
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Pause();
                _isBGMPaused = true;
            }
        }
        
        public void ResumeBGM()
        {
            if (_bgmSource != null && _isBGMPaused)
            {
                _bgmSource.UnPause();
                _isBGMPaused = false;
            }
        }
        
        #endregion
        
        #region SFX
        
        public void PlaySFX(string name, float volume = 1f)
        {
            if (string.IsNullOrEmpty(name)) return;
            
            // 尝试解锁音频
            TryUnlockAudio();
            
            var clip = LoadClip(name);
            if (clip == null)
            {
                CYLog.Warning($"[AudioService] SFX 加载失败: {name}");
                return;
            }
            
            // 从池中获取 AudioSource
            var source = GetNextSFXSource();
            source.clip = clip;
            source.volume = _sfxVolume * volume * _masterVolume;
            source.Play();
        }
        
        /// <summary>
        /// 获取下一个可用的 SFX 源（循环复用）
        /// </summary>
        private AudioSource GetNextSFXSource()
        {
            var source = _sfxPool[_sfxPoolIndex];
            _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxPool.Count;
            return source;
        }
        
        #endregion
        
        #region 音量控制
        
        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            UpdateVolumes();
        }
        
        public void SetBGMVolume(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
            UpdateVolumes();
        }
        
        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            UpdateVolumes();
        }
        
        public void Mute(bool mute)
        {
            _isMuted = mute;
            AudioListener.volume = mute ? 0f : 1f;
        }
        
        private void UpdateVolumes()
        {
            if (_bgmSource != null)
            {
                _bgmSource.volume = _bgmVolume * _masterVolume;
            }
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 加载音频 Clip
        /// </summary>
        private AudioClip LoadClip(string name)
        {
            if (_clipCache.TryGetValue(name, out var cached))
            {
                return cached;
            }
            
            // 从 Resources 加载（简化实现）
            var clip = Resources.Load<AudioClip>($"Audio/{name}");
            
            if (clip != null)
            {
                _clipCache[name] = clip;
            }
            
            return clip;
        }
        
        /// <summary>
        /// 尝试解锁音频
        /// 文档位置：3.1.7 自动解锁
        /// </summary>
        private void TryUnlockAudio()
        {
            if (_audioUnlocked) return;
            
            #if UNITY_WEBGL || CY_WECHAT
            // WebGL/微信需要用户交互才能播放音频
            // 这里播放一个静音片段来解锁 AudioContext
            PlaySilentClip();
            #endif
            
            _audioUnlocked = true;
        }
        
        /// <summary>
        /// 播放静音片段解锁 AudioContext
        /// </summary>
        private void PlaySilentClip()
        {
            // 创建一个极短的静音 AudioClip
            var silentClip = AudioClip.Create("Silent", 1, 1, 44100, false);
            var source = GetNextSFXSource();
            source.clip = silentClip;
            source.volume = 0.001f;
            source.Play();
        }
        
        #endregion
    }
}

