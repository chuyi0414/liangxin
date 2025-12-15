// ============================================================================
// CYFramework 2.2 - 音频服务
// 文档位置：3.1.7 音频系统 (Audio System)
// 功能：跨平台音频、微信特供处理、生命周期挂起
// ============================================================================

using System;
using System.Collections.Generic;
using CYFramework.Core.Config;
using CYFramework.Core.Pool;
using CYFramework.Core.Resource;
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
        /// <summary>
        /// 播放背景音乐
        /// </summary>
        /// <param name="name">BGM 名称或路径</param>
        /// <param name="volume">音量缩放（0~1）</param>
        /// <param name="loop">是否循环</param>
        void PlayBGM(string name, float volume = 1f, bool loop = true);
        /// <summary>
        /// 停止 BGM。
        /// </summary>
        /// <param name="fadeOut">
        /// 淡出时长（秒）：
        /// - &lt; 0：使用配置 <see cref="AudioConfig.BGMFadeTime"/>
        /// - = 0：立即停止
        /// - &gt; 0：按指定时长淡出
        /// </param>
        void StopBGM(float fadeOut = -1f);
        void PauseBGM();
        void ResumeBGM();
        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="name">SFX 名称或路径</param>
        /// <param name="volume">音量缩放（0~1）</param>
        void PlaySFX(string name, float volume = 1f);
        /// <summary>
        /// 设置主音量 (0~1)
        /// </summary>
        void SetMasterVolume(float volume);
        
        /// <summary>
        /// 设置 BGM 音量 (0~1)
        /// </summary>
        void SetBGMVolume(float volume);
        
        /// <summary>
        /// 设置 SFX 音量 (0~1)
        /// </summary>
        void SetSFXVolume(float volume);
        
        /// <summary>
        /// 静音所有音频
        /// </summary>
        void Mute(bool mute);
        bool IsMuted { get; }
        
        void PreloadBGM(string name);
        void PreloadSFX(string name);
        void PreloadAsync(string[] names, Action onComplete = null);
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
    public class UnityAudioService : IAudioService, IInitializable, IUpdateable, IPausable, IDisposableEx
    {
        private AudioConfig _config;
        
        // BGM
        private AudioSource _bgmSource;
        private string _currentBGM;
        private float _bgmVolume;
        private bool _isBGMPaused;
        
        // 淡出相关
        private bool _isFadingOut;
        private float _fadeOutDuration;
        private float _fadeOutTimer;
        private float _fadeStartVolume;
        
        // SFX 池
        private readonly List<AudioSource> _sfxPool = new();
        private int _sfxPoolIndex;
        private float _sfxVolume;
        
        // 主音量
        private float _masterVolume = 1f;
        private bool _isMuted;
        
        // 音频解锁状态（iOS WebAudio 限制）
        private bool _audioUnlocked;
        
        // 资源加载器
        private IResourceLoader _resourceLoader;
        
        // 资源缓存
        private readonly Dictionary<string, AudioClip> _clipCache = new();
        
        // 音频资源路径前缀
        private string _audioPath = "Audio/";
        private string _bgmPath = "Audio/BGM/";
        private string _sfxPath = "Audio/SFX/";
        
        public bool IsMuted => _isMuted;
        
        public int InitOrder => 30;
        public int UpdateOrder => 100;
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
            // 获取资源加载器
            _resourceLoader = ServiceLocator.Get<IResourceLoader>();
            
            // 从 CYConfigurator 读取配置
            var configurator = CYConfigurator.Instance;
            
            // 读取资源路径配置
            if (configurator != null)
            {
                var resourceConfig = configurator.GetConfig<ResourceLoaderConfig>();
                if (resourceConfig != null)
                {
                    _audioPath = EnsureEndsWithSlash(resourceConfig.AudioPath);
                    _bgmPath = EnsureEndsWithSlash(resourceConfig.BGMPath);
                    _sfxPath = EnsureEndsWithSlash(resourceConfig.SFXPath);
                }
            }
            if (configurator != null)
            {
                var externalConfig = configurator.GetConfig<AudioConfig>();
                if (externalConfig != null)
                {
                    _config = externalConfig;
                    _bgmVolume = _config.DefaultBGMVolume;
                    _sfxVolume = _config.DefaultSFXVolume;
                    CYLog.Debug("[AudioService] 使用 CYConfigurator 配置");
                }
            }
            
            // 先尝试查找场景中已存在的 AudioService
            var existingRoot = GameObject.Find("AudioService");
            GameObject audioRoot;
            
            if (existingRoot != null)
            {
                audioRoot = existingRoot;
                if (audioRoot.transform.parent == null)
                {
                    UnityEngine.Object.DontDestroyOnLoad(audioRoot);
                }
                
                // 查找已存在的 AudioSource 组件
                var existingSources = audioRoot.GetComponentsInChildren<AudioSource>();
                if (existingSources.Length > 0)
                {
                    _bgmSource = existingSources[0];
                    for (int i = 1; i < existingSources.Length && _sfxPool.Count < _config.SFXPoolSize; i++)
                    {
                        _sfxPool.Add(existingSources[i]);
                    }
                }
                
                // 如果 BGM 源不存在，创建它
                if (_bgmSource == null)
                {
                    _bgmSource = audioRoot.AddComponent<AudioSource>();
                    _bgmSource.playOnAwake = false;
                    _bgmSource.loop = true;
                }
                
                // 补充 SFX 池
                while (_sfxPool.Count < _config.SFXPoolSize)
                {
                    var sfxSource = audioRoot.AddComponent<AudioSource>();
                    sfxSource.playOnAwake = false;
                    sfxSource.loop = false;
                    _sfxPool.Add(sfxSource);
                }
                
                CYLog.Debug("[AudioService] 使用场景中已存在的 AudioService");
            }
            else
            {
                // 创建音频根节点
                audioRoot = new GameObject("AudioService");
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
                
                CYLog.Debug("[AudioService] 音频根节点创建完成");
            }
            
            UpdateVolumes();
            
            CYLog.Debug($"[AudioService] 初始化完成，SFX 池大小: {_sfxPool.Count}");
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
            
            var clip = LoadClip(BuildAudioPath(name, _bgmPath));
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
        
        public void StopBGM(float fadeOut = -1f)
        {
            if (_bgmSource == null || !_bgmSource.isPlaying) return;

            // <0 表示使用配置默认淡出时间（避免 StopBGM() 还写死常量）
            if (fadeOut < 0f)
            {
                fadeOut = _config != null ? _config.BGMFadeTime : 0.5f;
            }
            
            if (fadeOut <= 0)
            {
                // 立即停止
                _bgmSource.Stop();
                _currentBGM = null;
            }
            else
            {
                // 开始淡出
                _isFadingOut = true;
                _fadeOutDuration = fadeOut;
                _fadeOutTimer = 0f;
                _fadeStartVolume = _bgmSource.volume;
            }
            
            CYLog.Debug($"[AudioService] 停止 BGM, 淡出: {fadeOut}s");
        }
        
        /// <summary>
        /// IUpdateable 实现 - 驱动 BGM 淡出
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            if (!_isFadingOut) return;
            
            _fadeOutTimer += deltaTime;
            float t = _fadeOutTimer / _fadeOutDuration;
            
            if (t >= 1f)
            {
                // 淡出完成
                _bgmSource.Stop();
                _bgmSource.volume = _fadeStartVolume;
                _currentBGM = null;
                _isFadingOut = false;
            }
            else
            {
                // 线性淡出
                _bgmSource.volume = Mathf.Lerp(_fadeStartVolume, 0f, t);
            }
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
            
            var clip = LoadClip(BuildAudioPath(name, _sfxPath));
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
        
        #region 预加载 API
        
        /// <summary>
        /// 预加载 BGM
        /// </summary>
        public void PreloadBGM(string name)
        {
            LoadClip(BuildAudioPath(name, _bgmPath));
        }
        
        /// <summary>
        /// 预加载 SFX
        /// </summary>
        public void PreloadSFX(string name)
        {
            LoadClip(BuildAudioPath(name, _sfxPath));
        }
        
        /// <summary>
        /// 批量预加载
        /// </summary>
        public void PreloadAsync(string[] names, Action onComplete = null)
        {
            if (names == null || names.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }
            
            int total = names.Length;
            int loaded = 0;
            
            foreach (var name in names)
            {
                var path = BuildAudioPath(name, _audioPath);
                _resourceLoader?.LoadAsync<AudioClip>(path, clip =>
                {
                    if (clip != null)
                    {
                        _clipCache[path] = clip;
                    }
                    
                    loaded++;
                    if (loaded >= total)
                    {
                        onComplete?.Invoke();
                    }
                });
            }
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 加载音频 Clip
        /// </summary>
        private AudioClip LoadClip(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (_clipCache.TryGetValue(path, out var cached))
            {
                return cached;
            }
            
            // 通过 ResourceLoader 统一加载
            var clip = _resourceLoader?.Load<AudioClip>(path);
            
            if (clip != null)
            {
                _clipCache[path] = clip;
            }
            
            return clip;
        }

        private static string BuildAudioPath(string nameOrPath, string prefix)
        {
            if (string.IsNullOrEmpty(nameOrPath)) return null;

            // 允许传入完整路径（例如 "Audio/BGM/MainTheme"）
            if (nameOrPath.IndexOf('/') >= 0 || nameOrPath.IndexOf('\\') >= 0)
            {
                return nameOrPath;
            }

            return prefix + nameOrPath;
        }

        private static string EnsureEndsWithSlash(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return path.EndsWith("/") ? path : path + "/";
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
