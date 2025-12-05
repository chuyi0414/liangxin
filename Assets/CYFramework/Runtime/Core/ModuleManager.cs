// ============================================================================
// CYFramework - 模块管理器
// 负责所有模块的注册、初始化、更新和关闭
// 设计要点：
// - 使用 List 维护模块，按优先级排序更新
// - 只在启动时注册模块，运行时不频繁增删
// - 业务层应缓存常用模块引用，减少 GetModule 调用
// ============================================================================

using System;
using System.Collections.Generic;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 模块管理器
    /// 统一管理所有框架模块的生命周期
    /// </summary>
    public class ModuleManager
    {
        // 所有已注册的模块列表
        private readonly List<IModule> _modules = new List<IModule>();
        
        // 需要每帧更新的模块列表（缓存，避免每帧判断 NeedUpdate）
        private readonly List<IModule> _updateModules = new List<IModule>();
        
        // 模块类型到实例的映射，用于快速查找
        private readonly Dictionary<Type, IModule> _moduleMap = new Dictionary<Type, IModule>();
        
        // 是否已初始化
        private bool _initialized = false;

        /// <summary>
        /// 注册模块
        /// 必须在 Initialize 之前调用
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <param name="module">模块实例</param>
        public void RegisterModule<T>(T module) where T : class, IModule
        {
            if (_initialized)
            {
                UnityEngine.Debug.LogError("[CYFramework] 不能在初始化后注册模块");
                return;
            }

            Type type = typeof(T);
            if (_moduleMap.ContainsKey(type))
            {
                UnityEngine.Debug.LogWarning($"[CYFramework] 模块 {type.Name} 已注册，将被覆盖");
                // 移除旧模块
                IModule oldModule = _moduleMap[type];
                _modules.Remove(oldModule);
            }

            _modules.Add(module);
            _moduleMap[type] = module;
        }

        /// <summary>
        /// 获取模块
        /// 注意：高频调用场景应在外部缓存引用，避免字典查找开销
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns>模块实例，未找到返回 null</returns>
        public T GetModule<T>() where T : class, IModule
        {
            Type type = typeof(T);
            if (_moduleMap.TryGetValue(type, out IModule module))
            {
                return module as T;
            }
            return null;
        }

        /// <summary>
        /// 初始化所有模块
        /// 按优先级从小到大顺序调用各模块的 Initialize
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                UnityEngine.Debug.LogWarning("[CYFramework] ModuleManager 已经初始化过了");
                return;
            }

            // 按优先级排序（数值小的优先）
            _modules.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            // 初始化所有模块
            for (int i = 0; i < _modules.Count; i++)
            {
                try
                {
                    _modules[i].Initialize();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[CYFramework] 模块 {_modules[i].GetType().Name} 初始化失败: {e}");
                }
            }

            // 缓存需要更新的模块
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i].NeedUpdate)
                {
                    _updateModules.Add(_modules[i]);
                }
            }

            _initialized = true;
            UnityEngine.Debug.Log($"[CYFramework] ModuleManager 初始化完成，共 {_modules.Count} 个模块，{_updateModules.Count} 个需要更新");
        }

        /// <summary>
        /// 更新所有需要更新的模块
        /// 每帧由 CYFrameworkEntry 调用
        /// </summary>
        /// <param name="deltaTime">距离上一帧的时间（秒）</param>
        public void Update(float deltaTime)
        {
            if (!_initialized) return;

            for (int i = 0; i < _updateModules.Count; i++)
            {
                try
                {
                    _updateModules[i].Update(deltaTime);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[CYFramework] 模块 {_updateModules[i].GetType().Name} 更新失败: {e}");
                }
            }
        }

        /// <summary>
        /// 关闭所有模块
        /// 按优先级从大到小顺序调用各模块的 Shutdown（与初始化顺序相反）
        /// </summary>
        public void Shutdown()
        {
            if (!_initialized) return;

            // 逆序关闭（后初始化的先关闭）
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                try
                {
                    _modules[i].Shutdown();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[CYFramework] 模块 {_modules[i].GetType().Name} 关闭失败: {e}");
                }
            }

            _modules.Clear();
            _updateModules.Clear();
            _moduleMap.Clear();
            _initialized = false;

            UnityEngine.Debug.Log("[CYFramework] ModuleManager 已关闭");
        }
    }
}
