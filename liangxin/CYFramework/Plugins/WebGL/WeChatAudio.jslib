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
    // _wxSfxPoolIndex: SFX 池索引
    
    // 初始化音频系统
    WX_InitAudio: function(sfxPoolSize) {
        try {
            // 初始化 BGM 播放器（全局单例）
            window._wxBgmContext = wx.createInnerAudioContext();
            window._wxBgmContext.autoplay = false;
            
            // 初始化 SFX 池
            window._wxSfxPool = [];
            window._wxSfxPoolIndex = 0;
            
            for (var i = 0; i < sfxPoolSize; i++) { // i 为索引
                var ctx = wx.createInnerAudioContext(); // SFX 播放器
                ctx.autoplay = false;
                window._wxSfxPool.push(ctx);
            }
            
            console.log('[WeChatAudio] 初始化完成，SFX 池大小:', sfxPoolSize);
        } catch (e) {
            console.error('[WeChatAudio] 初始化失败:', e);
        }
    },
    
    // 播放 BGM
    WX_PlayBGM: function(namePtr, volume, loop) {
        var name = UTF8ToString(namePtr); // 音频名
        try {
            var ctx = window._wxBgmContext; // BGM 播放器
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
    
    // 停止 BGM
    WX_StopBGM: function(fadeOut) {
        try {
            var ctx = window._wxBgmContext; // BGM 播放器
            if (!ctx) return;
            
            if (fadeOut > 0) {
                // 淡出效果
                var startVolume = ctx.volume; // 初始音量
                var steps = 10; // 分步数量
                var stepTime = fadeOut * 1000 / steps; // 单步时长
                var step = 0; // 当前步
                
                var fadeInterval = setInterval(function() { // 淡出计时器
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
    
    // 暂停 BGM
    WX_PauseBGM: function() {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.pause();
            }
        } catch (e) {}
    },
    
    // 恢复 BGM
    WX_ResumeBGM: function() {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.play();
            }
        } catch (e) {}
    },
    
    // 播放 SFX
    WX_PlaySFX: function(namePtr, volume) {
        var name = UTF8ToString(namePtr); // 音频名
        try {
            // 从池中获取（循环复用）
            var pool = window._wxSfxPool; // SFX 池
            if (!pool || pool.length === 0) return;
            
            var ctx = pool[window._wxSfxPoolIndex]; // 当前 SFX 播放器
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
    
    // 设置主音量
    WX_SetMasterVolume: function(volume) {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.volume = volume;
            }
        } catch (e) {}
    },
    
    // 静音开关
    WX_Mute: function(mute) {
        try {
            var volume = mute ? 0 : 1; // 目标音量
            if (window._wxBgmContext) {
                window._wxBgmContext.volume = volume;
            }
            
            var pool = window._wxSfxPool; // SFX 池
            if (pool) {
                for (var i = 0; i < pool.length; i++) { // i 为索引
                    pool[i].volume = volume;
                }
            }
        } catch (e) {}
    },
    
    // 解锁音频上下文
    WX_UnlockAudio: function() {
        try {
            // 播放静音片段解锁 AudioContext
            var ctx = wx.createInnerAudioContext(); // 临时音频上下文
            ctx.src = 'audio/silent.mp3';  // 0.1s 静音文件
            ctx.volume = 0.01;
            ctx.play();
            
            ctx.onEnded(function() {
                ctx.destroy();
            });
            
            console.log('[WeChatAudio] 音频已解锁');
        } catch (e) {}
    },
    
    // 暂停全部音频
    WX_PauseAllAudio: function() {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.pause();
            }
            
            var pool = window._wxSfxPool; // SFX 池
            if (pool) {
                for (var i = 0; i < pool.length; i++) { // i 为索引
                    pool[i].pause();
                }
            }
        } catch (e) {}
    },
    
    // 恢复全部音频
    WX_ResumeAllAudio: function() {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.play();
            }
            // SFX 不自动恢复
        } catch (e) {}
    },
    
    // 销毁音频资源
    WX_DisposeAudio: function() {
        try {
            if (window._wxBgmContext) {
                window._wxBgmContext.destroy();
                window._wxBgmContext = null;
            }
            
            var pool = window._wxSfxPool; // SFX 池
            if (pool) {
                for (var i = 0; i < pool.length; i++) { // i 为索引
                    pool[i].destroy();
                }
                window._wxSfxPool = null;
            }
            
            console.log('[WeChatAudio] 已销毁');
        } catch (e) {}
    }
});
