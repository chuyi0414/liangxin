// ============================================================================
// CYFramework 2.2 - 微信音频 JS 桥接
// 文档位置：3.1.7 音频系统 - 微信端特供处理
// BGM: wx.createInnerAudioContext (流式加载)
// SFX: WebAudio API (快速触发)
// ============================================================================

mergeInto(LibraryManager.library, {
    
    // 全局变量
    // _wxBgmContext: BGM 播放器
    // _wxSfxPool: SFX 池
    
    WX_InitAudio: function(sfxPoolSize) {
        try {
            // 初始化 BGM 播放器（全局单例）
            window._wxBgmContext = wx.createInnerAudioContext();
            window._wxBgmContext.autoplay = false;
            
            // 初始化 SFX 池
            window._wxSfxPool = [];
            window._wxSfxPoolIndex = 0;
            
            for (var i = 0; i < sfxPoolSize; i++) {
                var ctx = wx.createInnerAudioContext();
                ctx.autoplay = false;
                window._wxSfxPool.push(ctx);
            }
            
            console.log('[WeChatAudio] 初始化完成，SFX 池大小:', sfxPoolSize);
        } catch (e) {
            console.error('[WeChatAudio] 初始化失败:', e);
        }
    },
    
    WX_PlayBGM: function(namePtr, volume, loop) {
        var name = UTF8ToString(namePtr);
        try {
            var ctx = window._wxBgmContext;
            if (!ctx) return;
            
            ctx.stop();
            ctx.src = 'audio/' + name + '.mp3';  // 音频路径
            ctx.volume = volume;
            ctx.loop = loop;
            ctx.play();
            
            console.log('[WeChatAudio] 播放 BGM:', name);
        } catch (e) {
            console.error('[WeChatAudio] 播放 BGM 失败:', e);
        }
    },
    
    WX_StopBGM: function(fadeOut) {
        try {
            var ctx = window._wxBgmContext;
            if (!ctx) return;
            
            if (fadeOut > 0) {
                // 淡出效果
                var startVolume = ctx.volume;
                var steps = 10;
                var stepTime = fadeOut * 1000 / steps;
                var step = 0;
                
                var fadeInterval = setInterval(function() {
                    step++;
                    ctx.volume = startVolume * (1 - step / steps);
                    
                    if (step >= steps) {
                        clearInterval(fadeInterval);
                        ctx.stop();
                        ctx.volume = startVolume;
                    }
                }, stepTime);
            } else {
                ctx.stop();
            }
        } catch (e) {
            console.error('[WeChatAudio] 停止 BGM 失败:', e);
        }
    },
    
    WX_PauseBGM: function() {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.pause();
            }
        } catch (e) {}
    },
    
    WX_ResumeBGM: function() {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.play();
            }
        } catch (e) {}
    },
    
    WX_PlaySFX: function(namePtr, volume) {
        var name = UTF8ToString(namePtr);
        try {
            // 从池中获取（循环复用）
            var pool = window._wxSfxPool;
            if (!pool || pool.length === 0) return;
            
            var ctx = pool[window._wxSfxPoolIndex];
            window._wxSfxPoolIndex = (window._wxSfxPoolIndex + 1) % pool.length;
            
            ctx.stop();
            ctx.src = 'audio/' + name + '.mp3';
            ctx.volume = volume;
            ctx.loop = false;
            ctx.play();
        } catch (e) {
            console.error('[WeChatAudio] 播放 SFX 失败:', e);
        }
    },
    
    WX_SetMasterVolume: function(volume) {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.volume = volume;
            }
        } catch (e) {}
    },
    
    WX_Mute: function(mute) {
        try {
            var volume = mute ? 0 : 1;
            if (window._wxBgmContext) {
                window._wxBgmContext.volume = volume;
            }
            
            var pool = window._wxSfxPool;
            if (pool) {
                for (var i = 0; i < pool.length; i++) {
                    pool[i].volume = volume;
                }
            }
        } catch (e) {}
    },
    
    WX_UnlockAudio: function() {
        try {
            // 播放静音片段解锁 AudioContext
            var ctx = wx.createInnerAudioContext();
            ctx.src = 'audio/silent.mp3';  // 0.1s 静音文件
            ctx.volume = 0.01;
            ctx.play();
            
            ctx.onEnded(function() {
                ctx.destroy();
            });
            
            console.log('[WeChatAudio] 音频已解锁');
        } catch (e) {}
    },
    
    WX_PauseAllAudio: function() {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.pause();
            }
            
            var pool = window._wxSfxPool;
            if (pool) {
                for (var i = 0; i < pool.length; i++) {
                    pool[i].pause();
                }
            }
        } catch (e) {}
    },
    
    WX_ResumeAllAudio: function() {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.play();
            }
            // SFX 不自动恢复
        } catch (e) {}
    },
    
    WX_DisposeAudio: function() {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.destroy();
                window._wxBgmContext = null;
            }
            
            var pool = window._wxSfxPool;
            if (pool) {
                for (var i = 0; i < pool.length; i++) {
                    pool[i].destroy();
                }
                window._wxSfxPool = null;
            }
            
            console.log('[WeChatAudio] 已销毁');
        } catch (e) {}
    }
});
