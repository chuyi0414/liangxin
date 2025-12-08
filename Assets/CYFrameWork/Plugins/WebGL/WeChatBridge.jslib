// ============================================================================
// CYFramework 2.2 - 微信小游戏 JS 桥接
// 用于 WebGL/微信小游戏平台的 JS 原生调用
// ============================================================================

mergeInto(LibraryManager.library, {
    
    // ==================== 存储 API ====================
    
    WX_GetStorage: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        try {
            var value = wx.getStorageSync(key) || "";
            var bufferSize = lengthBytesUTF8(value) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(value, buffer, bufferSize);
            return buffer;
        } catch (e) {
            console.error("[WeChatBridge] WX_GetStorage error:", e);
            var empty = _malloc(1);
            HEAP8[empty] = 0;
            return empty;
        }
    },
    
    WX_SetStorage: function(keyPtr, valuePtr) {
        var key = UTF8ToString(keyPtr);
        var value = UTF8ToString(valuePtr);
        try {
            wx.setStorageSync(key, value);
        } catch (e) {
            console.error("[WeChatBridge] WX_SetStorage error:", e);
        }
    },
    
    WX_RemoveStorage: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        try {
            wx.removeStorageSync(key);
        } catch (e) {
            console.error("[WeChatBridge] WX_RemoveStorage error:", e);
        }
    },
    
    WX_ClearStorage: function() {
        try {
            wx.clearStorageSync();
        } catch (e) {
            console.error("[WeChatBridge] WX_ClearStorage error:", e);
        }
    },
    
    WX_GetStorageInfoUsed: function() {
        try {
            var info = wx.getStorageInfoSync();
            return info.currentSize * 1024; // KB -> Bytes
        } catch (e) {
            console.error("[WeChatBridge] WX_GetStorageInfoUsed error:", e);
            return 0;
        }
    },
    
    WX_HasStorageKey: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        try {
            var value = wx.getStorageSync(key);
            return value !== undefined && value !== null && value !== "";
        } catch (e) {
            return false;
        }
    },
    
    // ==================== 文件系统 API ====================
    
    WX_FileExists: function(pathPtr) {
        var path = UTF8ToString(pathPtr);
        try {
            var fs = wx.getFileSystemManager();
            fs.accessSync(path);
            return true;
        } catch (e) {
            return false;
        }
    },
    
    WX_ReadFile: function(pathPtr) {
        var path = UTF8ToString(pathPtr);
        try {
            var fs = wx.getFileSystemManager();
            var content = fs.readFileSync(path, 'utf8');
            var bufferSize = lengthBytesUTF8(content) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(content, buffer, bufferSize);
            return buffer;
        } catch (e) {
            console.error("[WeChatBridge] WX_ReadFile error:", e);
            var empty = _malloc(1);
            HEAP8[empty] = 0;
            return empty;
        }
    },
    
    WX_WriteFile: function(pathPtr, contentPtr) {
        var path = UTF8ToString(pathPtr);
        var content = UTF8ToString(contentPtr);
        try {
            var fs = wx.getFileSystemManager();
            // 确保目录存在
            var dir = path.substring(0, path.lastIndexOf('/'));
            if (dir) {
                try { fs.mkdirSync(dir, true); } catch (e) {}
            }
            fs.writeFileSync(path, content, 'utf8');
            return true;
        } catch (e) {
            console.error("[WeChatBridge] WX_WriteFile error:", e);
            return false;
        }
    },
    
    WX_DeleteFile: function(pathPtr) {
        var path = UTF8ToString(pathPtr);
        try {
            var fs = wx.getFileSystemManager();
            fs.unlinkSync(path);
            return true;
        } catch (e) {
            console.error("[WeChatBridge] WX_DeleteFile error:", e);
            return false;
        }
    },
    
    // ==================== 网络 API ====================
    
    WX_HttpRequest: function(urlPtr, methodPtr, dataPtr, headersPtr, callbackId) {
        var url = UTF8ToString(urlPtr);
        var method = UTF8ToString(methodPtr);
        var data = UTF8ToString(dataPtr);
        var headers = JSON.parse(UTF8ToString(headersPtr) || '{}');
        
        try {
            wx.request({
                url: url,
                method: method,
                data: data,
                header: headers,
                success: function(res) {
                    var result = JSON.stringify(res.data);
                    // 调用 C# 回调
                    SendMessage('WeChatBridge', 'OnHttpResponse', callbackId + '|' + result);
                },
                fail: function(err) {
                    SendMessage('WeChatBridge', 'OnHttpResponse', callbackId + '|ERROR:' + err.errMsg);
                }
            });
        } catch (e) {
            console.error('[WeChatBridge] WX_HttpRequest error:', e);
        }
    },
    
    WX_ConnectSocket: function(urlPtr, callbackId) {
        var url = UTF8ToString(urlPtr);
        
        try {
            var socket = wx.connectSocket({
                url: url,
                success: function() {
                    SendMessage('WeChatBridge', 'OnSocketOpen', callbackId.toString());
                },
                fail: function(err) {
                    SendMessage('WeChatBridge', 'OnSocketError', callbackId + '|' + err.errMsg);
                }
            });
            
            socket.onMessage(function(res) {
                SendMessage('WeChatBridge', 'OnSocketMessage', res.data);
            });
            
            socket.onClose(function(res) {
                SendMessage('WeChatBridge', 'OnSocketClose', res.reason || 'closed');
            });
            
            socket.onError(function(err) {
                SendMessage('WeChatBridge', 'OnSocketError', callbackId + '|' + err.errMsg);
            });
            
            window._wxSocket = socket;
        } catch (e) {
            console.error('[WeChatBridge] WX_ConnectSocket error:', e);
        }
    },
    
    WX_SendSocketMessage: function(messagePtr) {
        var message = UTF8ToString(messagePtr);
        try {
            if (window._wxSocket) {
                window._wxSocket.send({ data: message });
            }
        } catch (e) {
            console.error('[WeChatBridge] WX_SendSocketMessage error:', e);
        }
    },
    
    WX_CloseSocket: function() {
        try {
            if (window._wxSocket) {
                window._wxSocket.close();
                window._wxSocket = null;
            }
        } catch (e) {
            console.error('[WeChatBridge] WX_CloseSocket error:', e);
        }
    },
    
    WX_GetNetworkType: function() {
        try {
            var networkType = 'unknown';
            wx.getNetworkType({
                success: function(res) {
                    networkType = res.networkType;
                }
            });
            var bufferSize = lengthBytesUTF8(networkType) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(networkType, buffer, bufferSize);
            return buffer;
        } catch (e) {
            var unknown = 'unknown';
            var bufferSize = lengthBytesUTF8(unknown) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(unknown, buffer, bufferSize);
            return buffer;
        }
    },
    
    // ==================== 系统 API ====================
    
    WX_VibrateShort: function() {
        try {
            wx.vibrateShort({ type: 'medium' });
        } catch (e) {}
    },
    
    WX_VibrateLong: function() {
        try {
            wx.vibrateLong();
        } catch (e) {}
    },
    
    WX_GetSystemInfo: function() {
        try {
            var info = wx.getSystemInfoSync();
            var json = JSON.stringify(info);
            var bufferSize = lengthBytesUTF8(json) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(json, buffer, bufferSize);
            return buffer;
        } catch (e) {
            var empty = _malloc(1);
            HEAP8[empty] = 0;
            return empty;
        }
    }
});
