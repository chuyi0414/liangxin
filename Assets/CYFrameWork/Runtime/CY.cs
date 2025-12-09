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
using CYFramework.Core.Pool;
using CYFramework.Core.Procedure;
using CYFramework.Core.Save;
using CYFramework.Core.Timer;
using CYFramework.Core.UI;
using CYFramework.Infrastructure;
using UnityEngine;

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
    }
}
