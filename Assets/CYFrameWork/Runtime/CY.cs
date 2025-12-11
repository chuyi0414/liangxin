// ============================================================================
// CYFramework - 统一入口
// 类似 GameFramework 的 GameEntry，提供简洁的 API
// ============================================================================

using System;
using CYFramework.Core.Audio;
using CYFramework.Core.Event;
using CYFramework.Core.FSM;
using CYFramework.Core.DataTable;
using CYFramework.Core.Entity;
using CYFramework.Core.Network;
using CYFramework.Core.Pool;
using CYFramework.Core.Procedure;
using CYFramework.Core.Resource;
using CYFramework.Core.Save;
using CYFramework.Core.Scene;
using CYFramework.Core.Timer;
using CYFramework.Core.UI;
using CYFramework.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CYFramework
{
    /// <summary>
    /// CYFramework 统一入口
    /// 所有系统直接暴露 Manager/Service，无中间方法
    /// 使用 partial 支持游戏项目扩展自定义系统
    /// </summary>
    public static partial class CY
    {
        // ==================== 缓存字段 ====================
        private static EventBus _event;
        private static TimerManager _timer;
        private static ProcedureManager _procedure;
        private static EntityManager _entity;
        private static UIManager _ui;
        private static DataTableManager _data;
        private static IAudioService _audio;
        
        // ==================== 核心服务 ====================
        
        /// <summary>
        /// 事件系统 - 解耦模块间通信
        /// 场景：玩家死亡通知UI更新、敌人击杀触发成就、购买道具刷新背包
        /// 用法：CY.Event.Post(ref evt) 发布, Subscribe() 订阅
        /// </summary>
        public static EventBus Event => _event ??= ServiceLocator.Get<EventBus>();
        
        /// <summary>
        /// 计时器系统 - 延时/循环执行，不依赖MonoBehaviour
        /// 场景：技能冷却、Buff持续时间、定时刷怪、UI倒计时
        /// 用法：CY.Timer.Delay(2f, callback), Loop(1f, callback)
        /// </summary>
        public static TimerManager Timer => _timer ??= GetOrCreateTimerManager();
        
        /// <summary>
        /// 流程系统 - 管理游戏主流程状态机
        /// 场景：启动→菜单→准备→战斗→结算→菜单
        /// 用法：CY.Procedure.Start("Menu"), Change("Battle")
        /// </summary>
        public static ProcedureManager Procedure => _procedure ??= GetOrCreateProcedureManager();
        
        /// <summary>
        /// 实体系统 - 管理游戏中的动态对象（带对象池）
        /// 场景：敌人、子弹、特效、掉落物、NPC
        /// 用法：CY.Entity.ShowEntity("Enemy"), HideEntity(id), PauseEntity(id)
        /// </summary>
        public static EntityManager Entity => _entity ??= GetOrCreateEntityManager();
        
        /// <summary>
        /// UI系统 - 管理所有UI面板的生命周期
        /// 场景：主界面、背包、商店、设置、对话框、Toast提示
        /// 用法：CY.UI.Open&lt;ShopUI&gt;(), Close&lt;T&gt;(), ShowConfirm(), ShowToast()
        /// </summary>
        public static UIManager UI => _ui ??= GetOrCreateUIManager();
        
        /// <summary>
        /// 数据表系统 - 读取配置表数据
        /// 场景：怪物属性表、道具表、技能表、关卡配置
        /// 用法：CY.Data.LoadFromCsv&lt;ItemData&gt;(csv), GetDataTable&lt;T&gt;().GetRow(id)
        /// </summary>
        public static DataTableManager Data => _data ??= GetOrCreateDataTableManager();
        
        /// <summary>
        /// 音频系统 - 播放背景音乐和音效
        /// 场景：BGM切换、按钮点击音、技能音效、环境音
        /// 用法：CY.Audio.PlayBGM("battle"), PlaySFX("click"), SetBGMVolume(0.5f)
        /// </summary>
        public static IAudioService Audio => _audio ??= ServiceLocator.Get<IAudioService>();
        
        /// <summary>
        /// 存档系统 - 本地数据持久化（支持加密、版本迁移）
        /// 场景：玩家进度、设置选项、成就记录
        /// 用法：CY.Save.Save(key, data), Load&lt;T&gt;(key)
        /// </summary>
        public static SaveService Save => ServiceLocator.Get<SaveService>();
        
        /// <summary>
        /// 对象池系统 - 复用GameObject减少GC
        /// 场景：大量生成销毁的对象（子弹、特效、UI元素）
        /// 用法：CY.Pool.GetOrCreatePool("Bullet", prefab).Get(), Release(go)
        /// </summary>
        public static PoolManager Pool => ServiceLocator.Get<PoolManager>();
        
        /// <summary>
        /// 资源加载系统 - 统一资源加载（支持 Resources/Addressables/AssetBundle）
        /// 场景：加载预制体、音频、图片、配置文件
        /// 用法：CY.Resource.Load&lt;T&gt;(path), LoadAsync&lt;T&gt;(path, callback)
        /// </summary>
        public static IResourceLoader Resource => ServiceLocator.Get<IResourceLoader>();
        
        /// <summary>
        /// 场景加载系统 - 场景切换与管理
        /// 场景：关卡切换、主菜单跳转、Loading界面
        /// 用法：CY.Scene.LoadScene("Battle"), LoadSceneAsync("Menu", progress => {})
        /// </summary>
        public static SceneLoader Scene => ServiceLocator.Get<SceneLoader>();
        
        /// <summary>
        /// 网络服务 - HTTP/WebSocket 请求
        /// 场景：登录、排行榜、多人游戏、实时对战
        /// 用法：CY.Network.HttpGet(url, callback), WebSocketConnect(url)
        /// </summary>
        public static NetworkService Network => ServiceLocator.Get<NetworkService>();
        
        /// <summary>
        /// 有限状态机工厂 - 创建和管理 FSM
        /// 场景：角色AI、动画状态、游戏模式
        /// 用法：CY.FSM.Create&lt;T&gt;("PlayerFSM"), Get&lt;T&gt;("PlayerFSM")
        /// </summary>
        public static FSMManager FSM => ServiceLocator.Get<FSMManager>();
        
        /// <summary>
        /// 游戏入口 - 获取当前游戏实例
        /// 场景：访问游戏全局数据、自定义子系统
        /// 用法：CY.Game 获取 GameEntryBase 实例
        /// </summary>
        public static Core.GameEntryBase Game => Core.GameEntryBase.Instance;
        
        // ==================== 懒加载创建方法 ====================
        
        private static TimerManager GetOrCreateTimerManager()
        {
            if (!ServiceLocator.TryGet<TimerManager>(out var manager))
            {
                manager = new TimerManager();
                ServiceLocator.RegisterInstance(manager);
                CYBootstrap.Instance?.RegisterLifecycle(manager);
            }
            return manager;
        }
        
        private static ProcedureManager GetOrCreateProcedureManager()
        {
            if (!ServiceLocator.TryGet<ProcedureManager>(out var manager))
            {
                manager = new ProcedureManager();
                ServiceLocator.RegisterInstance(manager);
                CYBootstrap.Instance?.RegisterLifecycle(manager);
            }
            return manager;
        }
        
        private static EntityManager GetOrCreateEntityManager()
        {
            if (!ServiceLocator.TryGet<EntityManager>(out var manager))
            {
                manager = new EntityManager();
                manager.Initialize();
                ServiceLocator.RegisterInstance(manager);
                CYBootstrap.Instance?.RegisterLifecycle(manager);
            }
            return manager;
        }
        
        private static UIManager GetOrCreateUIManager()
        {
            if (!ServiceLocator.TryGet<UIManager>(out var manager))
            {
                manager = new UIManager();
                manager.Initialize();
                ServiceLocator.RegisterInstance(manager);
                CYBootstrap.Instance?.RegisterLifecycle(manager);
            }
            return manager;
        }
        
        private static DataTableManager GetOrCreateDataTableManager()
        {
            if (!ServiceLocator.TryGet<DataTableManager>(out var manager))
            {
                manager = new DataTableManager();
                ServiceLocator.RegisterInstance(manager);
                CYBootstrap.Instance?.RegisterLifecycle(manager);
            }
            return manager;
        }
        
        // ==================== 服务定位器快捷方法 ====================
        
        /// <summary>
        /// 获取服务
        /// </summary>
        public static T Get<T>() where T : class
        {
            return ServiceLocator.Get<T>();
        }
        
        /// <summary>
        /// 注册服务
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            ServiceLocator.RegisterInstance(service);
        }
        
        // ==================== 常用快捷方法 ====================
        
        #region 日志快捷方法
        
        /// <summary>日志 - Debug 级别</summary>
        public static void Log(string message) => CYLog.Debug(message);
        
        /// <summary>日志 - Info 级别</summary>
        public static void LogInfo(string message) => CYLog.Info(message);
        
        /// <summary>日志 - Warning 级别</summary>
        public static void LogWarning(string message) => CYLog.Warning(message);
        
        /// <summary>日志 - Error 级别</summary>
        public static void LogError(string message) => CYLog.Error(message);
        
        #endregion
        
        #region 计时器快捷方法
        
        /// <summary>
        /// 延时执行
        /// </summary>
        /// <param name="delay">延时秒数</param>
        /// <param name="callback">回调</param>
        /// <returns>计时器实例</returns>
        public static Core.Timer.Timer Delay(float delay, Action callback)
        {
            return Timer.Delay(delay, callback);
        }
        
        /// <summary>
        /// 循环执行
        /// </summary>
        /// <param name="interval">间隔秒数</param>
        /// <param name="callback">回调</param>
        /// <returns>计时器实例</returns>
        public static Core.Timer.Timer Loop(float interval, Action callback)
        {
            return Timer.Loop(interval, callback);
        }
        
        /// <summary>
        /// 下一帧执行
        /// </summary>
        public static Core.Timer.Timer NextFrame(Action callback)
        {
            return Timer.NextFrame(callback);
        }
        
        /// <summary>
        /// 取消计时器（通过ID）
        /// </summary>
        public static void CancelTimer(int timerId)
        {
            Timer.Cancel(timerId);
        }
        
        /// <summary>
        /// 取消计时器（通过实例）
        /// </summary>
        public static void CancelTimer(Core.Timer.Timer timer)
        {
            Timer.Cancel(timer);
        }
        
        #endregion
        
        #region 事件快捷方法
        
        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        /// <param name="target">订阅者对象（用于自动解绑）</param>
        public static void Subscribe<T>(Core.Event.EventHandler<T> handler, object target = null) where T : struct
        {
            Event.Subscribe(handler, target);
        }
        
        /// <summary>
        /// 取消订阅
        /// </summary>
        public static void Unsubscribe<T>(Core.Event.EventHandler<T> handler) where T : struct
        {
            Event.Unsubscribe(handler);
        }
        
        /// <summary>
        /// 发布事件
        /// </summary>
        public static void Publish<T>(ref T evt) where T : struct
        {
            Event.Post(ref evt);
        }
        
        #endregion
        
        #region 资源快捷方法
        
        /// <summary>
        /// 加载资源
        /// </summary>
        public static T Load<T>(string path) where T : Object
        {
            return Resource?.Load<T>(path);
        }
        
        /// <summary>
        /// 异步加载资源
        /// </summary>
        public static void LoadAsync<T>(string path, Action<T> callback) where T : Object
        {
            Resource?.LoadAsync(path, callback);
        }
        
        #endregion
        
        #region 音频快捷方法
        
        /// <summary>
        /// 播放背景音乐
        /// </summary>
        /// <param name="name">音乐名称</param>
        /// <param name="volume">音量 (0-1)</param>
        /// <param name="loop">是否循环</param>
        public static void PlayBGM(string name, float volume = 1f, bool loop = true)
        {
            Audio?.PlayBGM(name, volume, loop);
        }
        
        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="name">音效名称</param>
        /// <param name="volume">音量 (0-1)</param>
        public static void PlaySFX(string name, float volume = 1f)
        {
            Audio?.PlaySFX(name, volume);
        }
        
        /// <summary>
        /// 停止背景音乐
        /// </summary>
        /// <param name="fadeOut">淡出时间（秒）</param>
        public static void StopBGM(float fadeOut = 0.5f)
        {
            Audio?.StopBGM(fadeOut);
        }
        
        /// <summary>
        /// 暂停背景音乐
        /// </summary>
        public static void PauseBGM()
        {
            Audio?.PauseBGM();
        }
        
        /// <summary>
        /// 恢复背景音乐
        /// </summary>
        public static void ResumeBGM()
        {
            Audio?.ResumeBGM();
        }
        
        #endregion
        
        #region UI 快捷方法
        
        /// <summary>
        /// 显示 Toast 提示
        /// </summary>
        public static void Toast(string message)
        {
            UI?.ShowToast(message);
        }
        
        /// <summary>
        /// 显示确认对话框
        /// </summary>
        public static void Confirm(string title, string content, Action onConfirm, Action onCancel = null)
        {
            UI?.ShowConfirm(title, content, onConfirm, onCancel);
        }
        
        /// <summary>
        /// 显示提示对话框
        /// </summary>
        public static void Alert(string title, string content, Action onConfirm = null)
        {
            UI?.ShowAlert(title, content, onConfirm);
        }
        
        #endregion
    }
}
