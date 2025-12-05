// ============================================================================
// CYFramework - 声音模块
// 提供 BGM 和音效的播放管理
// 
// 设计要点：
// - BGM 支持淡入淡出切换
// - 音效支持对象池复用
// - 独立的音量控制
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 声音模块
    /// </summary>
    public class SoundModule : IModule
    {
        public int Priority => 25;
        public bool NeedUpdate => true;

        // 音源根节点
        private GameObject _soundRoot;
        
        // BGM 音源
        private AudioSource _bgmSource;
        private AudioSource _bgmSourceFade; // 用于淡入淡出
        
        // 音效音源池
        private List<AudioSource> _sfxSources;
        private int _sfxPoolSize = 16;
        
        // 音量设置
        private float _masterVolume = 1f;
        private float _bgmVolume = 1f;
        private float _sfxVolume = 1f;
        
        // 静音设置
        private bool _muteBGM = false;
        private bool _muteSFX = false;
        
        // BGM 淡入淡出
        private bool _isFading;
        private float _fadeTimer;
        private float _fadeDuration;
        private AudioClip _pendingBgm;
        private bool _pendingLoop;

        public void Initialize()
        {
            // 创建音源根节点
            _soundRoot = new GameObject("[CYFramework.Sound]");
            Object.DontDestroyOnLoad(_soundRoot);

            // 创建 BGM 音源
            _bgmSource = CreateAudioSource("BGM");
            _bgmSourceFade = CreateAudioSource("BGM_Fade");

            // 创建音效音源池
            _sfxSources = new List<AudioSource>(_sfxPoolSize);
            for (int i = 0; i < _sfxPoolSize; i++)
            {
                _sfxSources.Add(CreateAudioSource($"SFX_{i}"));
            }

            Log.I("SoundModule", "初始化完成");
        }

        public void Update(float deltaTime)
        {
            // 处理 BGM 淡入淡出
            if (_isFading)
            {
                _fadeTimer += deltaTime;
                float t = Mathf.Clamp01(_fadeTimer / _fadeDuration);

                // 旧 BGM 淡出
                _bgmSource.volume = (1f - t) * _bgmVolume * _masterVolume;

                // 新 BGM 淡入
                _bgmSourceFade.volume = t * _bgmVolume * _masterVolume;

                if (t >= 1f)
                {
                    // 淡入淡出完成，交换音源
                    _bgmSource.Stop();
                    var temp = _bgmSource;
                    _bgmSource = _bgmSourceFade;
                    _bgmSourceFade = temp;
                    _isFading = false;
                }
            }
        }

        public void Shutdown()
        {
            StopAll();
            if (_soundRoot != null)
            {
                Object.Destroy(_soundRoot);
            }
            Log.I("SoundModule", "已关闭");
        }

        // ====================================================================
        // BGM
        // ====================================================================

        /// <summary>
        /// 播放 BGM
        /// </summary>
        /// <param name="clip">音频剪辑</param>
        /// <param name="loop">是否循环</param>
        /// <param name="fadeDuration">淡入淡出时间（0 表示立即切换）</param>
        public void PlayBGM(AudioClip clip, bool loop = true, float fadeDuration = 0.5f)
        {
            if (clip == null) return;

            if (fadeDuration > 0 && _bgmSource.isPlaying)
            {
                // 淡入淡出切换
                _isFading = true;
                _fadeTimer = 0f;
                _fadeDuration = fadeDuration;

                _bgmSourceFade.clip = clip;
                _bgmSourceFade.loop = loop;
                _bgmSourceFade.volume = 0f;
                _bgmSourceFade.Play();
            }
            else
            {
                // 立即切换
                _bgmSource.clip = clip;
                _bgmSource.loop = loop;
                _bgmSource.volume = _bgmVolume * _masterVolume;
                _bgmSource.Play();
            }
        }

        /// <summary>
        /// 通过路径播放 BGM
        /// </summary>
        public void PlayBGM(string path, bool loop = true, float fadeDuration = 0.5f)
        {
            var resource = CYFrameworkEntry.Instance?.GetModule<ResourceModule>();
            if (resource == null) return;

            AudioClip clip = resource.Load<AudioClip>(path);
            PlayBGM(clip, loop, fadeDuration);
        }

        /// <summary>
        /// 停止 BGM
        /// </summary>
        public void StopBGM(float fadeDuration = 0.5f)
        {
            if (fadeDuration > 0 && _bgmSource.isPlaying)
            {
                CYFrameworkEntry.Instance.StartCoroutine(FadeOutBGM(fadeDuration));
            }
            else
            {
                _bgmSource.Stop();
            }
        }

        private IEnumerator FadeOutBGM(float duration)
        {
            float startVolume = _bgmSource.volume;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / duration);
                yield return null;
            }

            _bgmSource.Stop();
            _bgmSource.volume = _bgmVolume * _masterVolume;
        }

        /// <summary>
        /// 暂停 BGM
        /// </summary>
        public void PauseBGM()
        {
            _bgmSource.Pause();
        }

        /// <summary>
        /// 恢复 BGM
        /// </summary>
        public void ResumeBGM()
        {
            _bgmSource.UnPause();
        }

        // ====================================================================
        // 音效
        // ====================================================================

        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="clip">音频剪辑</param>
        /// <param name="volumeScale">音量缩放（0-1）</param>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;

            AudioSource source = GetAvailableSFXSource();
            if (source != null)
            {
                source.clip = clip;
                source.volume = _sfxVolume * _masterVolume * volumeScale;
                source.Play();
            }
        }

        /// <summary>
        /// 通过路径播放音效
        /// </summary>
        public void PlaySFX(string path, float volumeScale = 1f)
        {
            var resource = CYFrameworkEntry.Instance?.GetModule<ResourceModule>();
            if (resource == null) return;

            AudioClip clip = resource.Load<AudioClip>(path);
            PlaySFX(clip, volumeScale);
        }

        /// <summary>
        /// 在指定位置播放 3D 音效
        /// </summary>
        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, _sfxVolume * _masterVolume * volumeScale);
        }

        private AudioSource GetAvailableSFXSource()
        {
            // 找一个空闲的音源
            for (int i = 0; i < _sfxSources.Count; i++)
            {
                if (!_sfxSources[i].isPlaying)
                {
                    return _sfxSources[i];
                }
            }

            // 如果都在播放，返回第一个（会被覆盖）
            return _sfxSources.Count > 0 ? _sfxSources[0] : null;
        }

        // ====================================================================
        // 音量控制
        // ====================================================================

        /// <summary>
        /// 设置主音量
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            ApplyVolume();
        }

        /// <summary>
        /// 设置 BGM 音量
        /// </summary>
        public void SetBGMVolume(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
            ApplyVolume();
        }

        /// <summary>
        /// 设置音效音量
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
        }

        public float MasterVolume
        {
            get => _masterVolume;
            set => SetMasterVolume(value);
        }

        public float BGMVolume
        {
            get => _bgmVolume;
            set => SetBGMVolume(value);
        }

        public float SFXVolume
        {
            get => _sfxVolume;
            set => SetSFXVolume(value);
        }

        public bool MuteBGM
        {
            get => _muteBGM;
            set
            {
                _muteBGM = value;
                _bgmSource.mute = value;
                _bgmSourceFade.mute = value;
            }
        }

        public bool MuteSFX
        {
            get => _muteSFX;
            set
            {
                _muteSFX = value;
                foreach (var source in _sfxSources)
                {
                    source.mute = value;
                }
            }
        }

        private void ApplyVolume()
        {
            if (!_isFading)
            {
                _bgmSource.volume = _bgmVolume * _masterVolume;
            }
        }

        /// <summary>
        /// 停止所有声音
        /// </summary>
        public void StopAll()
        {
            _bgmSource?.Stop();
            _bgmSourceFade?.Stop();
            foreach (var source in _sfxSources)
            {
                source?.Stop();
            }
        }

        private AudioSource CreateAudioSource(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_soundRoot.transform);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }
    }
}
