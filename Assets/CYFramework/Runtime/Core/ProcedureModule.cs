// ============================================================================
// CYFramework - 流程模块
// 管理游戏的流程状态（如启动、登录、主菜单、游戏中等）
// 
// 设计要点：
// - 基于状态机实现
// - 流程之间可以传递数据
// - 支持流程的生命周期管理
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using CYFramework.Runtime.Core.FSM;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 流程基类
    /// </summary>
    public abstract class ProcedureBase : IState<ProcedureModule>
    {
        /// <summary>
        /// 流程进入
        /// </summary>
        public virtual void OnEnter(ProcedureModule owner)
        {
            Log.I("Procedure", $"进入流程: {GetType().Name}");
        }

        /// <summary>
        /// 流程更新
        /// </summary>
        public virtual void OnUpdate(ProcedureModule owner, float deltaTime) { }

        /// <summary>
        /// 流程退出
        /// </summary>
        public virtual void OnExit(ProcedureModule owner)
        {
            Log.I("Procedure", $"退出流程: {GetType().Name}");
        }

        /// <summary>
        /// 切换到其他流程
        /// </summary>
        protected void ChangeProcedure<T>() where T : ProcedureBase
        {
            CYFrameworkEntry.Instance?.GetModule<ProcedureModule>()?.ChangeProcedure<T>();
        }
    }

    /// <summary>
    /// 流程模块
    /// </summary>
    public class ProcedureModule : IModule
    {
        public int Priority => 30;
        public bool NeedUpdate => true;

        private StateMachine<ProcedureModule> _fsm;
        private Dictionary<string, object> _procedureData;

        /// <summary>
        /// 是否自动注册所有 ProcedureBase 子类
        /// 默认关闭以保持轻量级，需要时可手动开启
        /// </summary>
        public bool AutoRegister { get; set; } = false;

        public void Initialize()
        {
            _fsm = new StateMachine<ProcedureModule>(this);
            _procedureData = new Dictionary<string, object>();
            
            // 自动注册所有 ProcedureBase 子类
            if (AutoRegister)
            {
                AutoRegisterProcedures();
            }
            
            Log.I("ProcedureModule", "初始化完成");
        }

        /// <summary>
        /// 自动注册所有 ProcedureBase 的非抽象子类
        /// 使用反射扫描当前程序集中的所有流程类
        /// </summary>
        private void AutoRegisterProcedures()
        {
            int count = 0;
            
            // 获取所有已加载的程序集
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                // 跳过系统程序集
                string name = assembly.GetName().Name;
                if (name.StartsWith("System") || name.StartsWith("Unity") || 
                    name.StartsWith("mscorlib") || name.StartsWith("netstandard"))
                    continue;
                
                try
                {
                    // 查找所有 ProcedureBase 的子类
                    var procedureTypes = assembly.GetTypes()
                        .Where(t => t.IsClass && 
                                    !t.IsAbstract && 
                                    typeof(ProcedureBase).IsAssignableFrom(t));
                    
                    foreach (var type in procedureTypes)
                    {
                        try
                        {
                            // 使用无参构造函数创建实例
                            var procedure = Activator.CreateInstance(type) as ProcedureBase;
                            if (procedure != null)
                            {
                                // 使用反射调用泛型注册方法
                                _fsm.RegisterState(type, procedure);
                                count++;
                                Log.D("ProcedureModule", $"自动注册流程: {type.Name}");
                            }
                        }
                        catch (Exception e)
                        {
                            Log.W("ProcedureModule", $"无法注册流程 {type.Name}: {e.Message}");
                        }
                    }
                }
                catch
                {
                    // 忽略无法加载类型的程序集
                }
            }
            
            Log.I("ProcedureModule", $"自动注册了 {count} 个流程");
        }

        public void Update(float deltaTime)
        {
            _fsm?.Update(deltaTime);
        }

        public void Shutdown()
        {
            _fsm?.Shutdown();
            _procedureData?.Clear();
            Log.I("ProcedureModule", "已关闭");
        }

        // ====================================================================
        // 流程管理
        // ====================================================================

        /// <summary>
        /// 注册流程
        /// </summary>
        public void RegisterProcedure<T>(T procedure) where T : ProcedureBase
        {
            _fsm.RegisterState(procedure);
        }

        /// <summary>
        /// 切换流程
        /// </summary>
        public void ChangeProcedure<T>() where T : ProcedureBase
        {
            _fsm.ChangeState<T>();
        }

        /// <summary>
        /// 启动并进入初始流程
        /// </summary>
        public void StartProcedure<T>() where T : ProcedureBase
        {
            _fsm.ChangeState<T>();
        }

        /// <summary>
        /// 获取当前流程
        /// </summary>
        public ProcedureBase CurrentProcedure => _fsm?.CurrentState as ProcedureBase;

        /// <summary>
        /// 获取当前流程类型
        /// </summary>
        public Type CurrentProcedureType => _fsm?.CurrentStateType;

        /// <summary>
        /// 检查是否在指定流程
        /// </summary>
        public bool IsInProcedure<T>() where T : ProcedureBase
        {
            return _fsm != null && _fsm.IsInState<T>();
        }

        // ====================================================================
        // 流程数据传递
        // ====================================================================

        /// <summary>
        /// 设置流程数据
        /// </summary>
        public void SetData(string key, object value)
        {
            _procedureData[key] = value;
        }

        /// <summary>
        /// 获取流程数据
        /// </summary>
        public T GetData<T>(string key, T defaultValue = default)
        {
            if (_procedureData.TryGetValue(key, out object value))
            {
                return (T)value;
            }
            return defaultValue;
        }

        /// <summary>
        /// 移除流程数据
        /// </summary>
        public void RemoveData(string key)
        {
            _procedureData.Remove(key);
        }

        /// <summary>
        /// 清空所有流程数据
        /// </summary>
        public void ClearData()
        {
            _procedureData.Clear();
        }
    }

    // ========================================================================
    // 示例流程（实际项目中应放到单独文件）
    // ========================================================================

    /// <summary>
    /// 示例：启动流程
    /// 负责初始化检查，完成后自动进入主菜单
    /// </summary>
    public class ProcedureLaunch : ProcedureBase
    {
        private float _initTime;
        private bool _initComplete;

        public override void OnEnter(ProcedureModule owner)
        {
            base.OnEnter(owner);
            _initTime = 0f;
            _initComplete = false;
            
            // 执行初始化检查（模拟）
            Log.I("ProcedureLaunch", "开始初始化检查...");
        }

        public override void OnUpdate(ProcedureModule owner, float deltaTime)
        {
            if (_initComplete) return;

            _initTime += deltaTime;
            
            // 模拟初始化耗时 1 秒
            if (_initTime >= 1f)
            {
                _initComplete = true;
                Log.I("ProcedureLaunch", "初始化完成，准备进入主菜单");
                ChangeProcedure<ProcedureMainMenu>();
            }
        }
    }

    /// <summary>
    /// 示例：主菜单流程
    /// 等待玩家开始游戏
    /// </summary>
    public class ProcedureMainMenu : ProcedureBase
    {
        public override void OnEnter(ProcedureModule owner)
        {
            base.OnEnter(owner);
            Log.I("ProcedureMainMenu", "主菜单已显示，按 Enter 开始游戏");
        }

        public override void OnUpdate(ProcedureModule owner, float deltaTime)
        {
            // 按 Enter 键开始游戏
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return))
            {
                Log.I("ProcedureMainMenu", "开始游戏");
                ChangeProcedure<ProcedureGame>();
            }
        }
    }

    /// <summary>
    /// 示例：游戏中流程
    /// 游戏主逻辑
    /// </summary>
    public class ProcedureGame : ProcedureBase
    {
        public override void OnEnter(ProcedureModule owner)
        {
            base.OnEnter(owner);
            Log.I("ProcedureGame", "进入游戏，按 Escape 返回主菜单");
            
            // 启动玩法世界
            CYFrameworkEntry.Instance?.GameplayWorld?.Resume();
        }

        public override void OnUpdate(ProcedureModule owner, float deltaTime)
        {
            // 按 Escape 返回主菜单
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
            {
                Log.I("ProcedureGame", "返回主菜单");
                ChangeProcedure<ProcedureMainMenu>();
            }
        }

        public override void OnExit(ProcedureModule owner)
        {
            base.OnExit(owner);
            
            // 暂停玩法世界
            CYFrameworkEntry.Instance?.GameplayWorld?.Pause();
        }
    }
}
