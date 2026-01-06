// ============================================================================
// CYFramework - 实体管理器
// 统一管理游戏中的动态实体（敌人、子弹、单位等）
// ============================================================================
using System;
using System.Collections.Generic;
using CYFramework.Core.Config;
using CYFramework.Core.Pool;
using CYFramework.Infrastructure;
using UnityEngine;
namespace CYFramework.Core.Entity
{
    /// <summary>
    /// 实体分组枚举
    /// </summary>
    public enum EntityGroup
    {
        /// <summary>
        /// 默认（杂项）
        /// </summary>
        Default = 0,
        /// <summary>
        /// 玩家
        /// </summary>
        Players,
        /// <summary>
        /// 敌人
        /// </summary>
        Enemies,
        /// <summary>
        /// NPC
        /// </summary>
        NPCs,
        /// <summary>
        /// 道具/场景物件
        /// </summary>
        Props,
        /// <summary>
        /// 特效
        /// </summary>
        Effects,
        /// <summary>
        /// 子弹/投射物
        /// </summary>
        Projectiles,
        
        /// <summary>
        /// 掉落物
        /// </summary>
        Items
    }

    /// <summary>
    /// 实体接口
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// 实体唯一 ID（由 EntityManager 分配）
        /// </summary>
        int Id { get; }
        /// <summary>
        /// 实体类型标识（通常是注册时的 Key）
        /// </summary>
        string EntityType { get; }
        /// <summary>
        /// 实体是否可见
        /// </summary>
        bool IsVisible { get; }
        /// <summary>
        /// 实体是否暂停
        /// </summary>
        bool IsPaused { get; }
        /// <summary>
        /// 实体对应的 GameObject
        /// </summary>
        GameObject GameObject { get; }
        
        /// <summary>
        /// 初始化实体
        /// </summary>
        void OnInit(int id, object userData);
        /// <summary>
        /// 显示实体
        /// </summary>
        void OnShow(object userData);
        /// <summary>
        /// 隐藏实体
        /// </summary>
        void OnHide();
        /// <summary>
        /// 暂停实体
        /// </summary>
        void OnPause();
        /// <summary>
        /// 恢复实体
        /// </summary>
        void OnResume();
        /// <summary>
        /// 固定帧更新
        /// </summary>
        void OnFixedUpdate(float deltaTime);
        /// <summary>
        /// 每帧更新
        /// </summary>
        void OnUpdate(float deltaTime);
        /// <summary>
        /// 延迟更新
        /// </summary>
        void OnLateUpdate(float deltaTime);
        /// <summary>
        /// 回收实体
        /// </summary>
        void OnRecycle();
    }
    


    /// <summary>
    /// 实体预显示接口（用于在激活前应用位置与朝向）。
    /// </summary>
    public interface IEntityPreShowTransform // 实体预显示变换接口
    {
        /// <summary>
        /// 获取预显示位置（世界坐标）。
        /// </summary>
        /// <param name="position">输出位置。</param>
        /// <returns>是否提供有效位置。</returns>
        bool TryGetPreShowPosition(out Vector3 position); // 预显示位置获取

        /// <summary>
        /// 获取预显示旋转。
        /// </summary>
        /// <param name="rotation">输出旋转。</param>
        /// <returns>是否提供有效旋转。</returns>
        bool TryGetPreShowRotation(out Quaternion rotation); // 预显示旋转获取
    }

    /// <summary>
    /// 实体预显示数据接口（用于无装箱注入出生数据）。
    /// </summary>
    /// <typeparam name="TData">出生数据类型。</typeparam>
    public interface IEntityPreShowData<TData> where TData : struct // 实体预显示数据接口
    {
        /// <summary>
        /// 应用预显示数据（激活前调用）。
        /// </summary>
        /// <param name="data">出生数据（引用传递）。</param>
        void ApplyPreShowData(ref TData data); // 预显示数据应用
    }
    /// <summary>
    /// 实体基类
    /// </summary>
    public abstract class EntityBase : MonoBehaviour, IEntity, IPoolable
    {
        /// <summary>
        /// 实体唯一 ID
        /// </summary>
        public int Id { get; private set; }
        
        /// <summary>
        /// 实体类型标识
        /// </summary>
        private string _entityType;
        /// <summary>
        /// 实体类型标识（可由子类保护性设置）
        /// </summary>
        public string EntityType 
        { 
            get => _entityType; 
            // 保护性设置
            protected set => _entityType = value; 
        }

        /// <summary>
        /// 设置实体类型标识（供管理器注入）
        /// </summary>
        public void SetEntityType(string type) => _entityType = type;

        /// <summary>
        /// 实体是否可见
        /// </summary>
        public bool IsVisible { get; private set; }
        /// <summary>
        /// 实体是否暂停
        /// </summary>
        public bool IsPaused { get; private set; }
        /// <summary>
        /// 实体对应的 GameObject
        /// </summary>
        public GameObject GameObject => gameObject;
        
        /// <summary>
        /// 用户数据缓存
        /// </summary>
        protected object UserData { get; private set; }

        /// <summary>
        /// 渲染组件缓存。
        /// </summary>
        private Renderer[] _cachedRenderers; // 渲染组件缓存
        /// <summary>
        /// 渲染组件默认启用状态缓存。
        /// </summary>
        private bool[] _cachedRendererDefaultStates; // 渲染默认状态缓存
        /// <summary>
        /// 渲染缓存是否已初始化。
        /// </summary>
        private bool _rendererCacheReady; // 渲染缓存就绪标记
        /// <summary>
        /// 是否自动恢复渲染组件可见性。
        /// </summary>
        private bool _autoRestoreRenderers = true; // 自动恢复渲染开关
        /// <summary>
        /// 回收隐藏位置（用于对象池回收后远离场景）。
        /// </summary>
        private static readonly Vector3 HiddenWorldPosition = new Vector3(100000f, 100000f, 0f); // 回收隐藏位置

        /// <summary>
        /// 是否自动恢复渲染组件可见性（供子类控制）。
        /// </summary>
        protected bool AutoRestoreRenderers // 自动恢复渲染属性
        {
            get => _autoRestoreRenderers; // 读取自动恢复开关
            set => _autoRestoreRenderers = value; // 写入自动恢复开关
        }
        
        /// <summary>
        /// 初始化实体
        /// </summary>
        public void OnInit(int id, object userData)
        {
            Id = id;
            UserData = userData;
            IsPaused = false;
            CacheRenderersIfNeeded(); // 缓存渲染默认状态
            OnEntityInit(userData);
        }
        
        /// <summary>
        /// 显示实体
        /// </summary>
        public void OnShow(object userData)
        {
            UserData = userData;
            IsVisible = true;
            IsPaused = false;
            ApplyPreShowTransform(userData); // 应用预显示变换
            OnEntityPreShow(userData); // 预显示钩子
            DisableCachedRenderers(); // 先隐藏渲染避免旧状态闪烁
            gameObject.SetActive(true);
            OnEntityShow(userData);
            if (_autoRestoreRenderers)
            {
                RestoreCachedRenderersToDefault(); // 自动恢复渲染默认可见性
            }
        }
        
        /// <summary>
        /// 隐藏实体
        /// </summary>
        public void OnHide()
        {
            IsVisible = false;
            OnEntityHide();
            DisableCachedRenderers(); // 隐藏渲染避免回收残留
            MoveToHiddenPosition(); // 移动到隐藏位置避免残留碰撞
            gameObject.SetActive(false);
        }
        
        /// <summary>
        /// 暂停实体
        /// </summary>
        public void OnPause()
        {
            if (IsPaused) return;
            IsPaused = true;
            OnEntityPause();
        }
        
        /// <summary>
        /// 恢复实体
        /// </summary>
        public void OnResume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            OnEntityResume();
        }
        
        /// <summary>
        /// 固定帧更新
        /// </summary>
        public void OnFixedUpdate(float deltaTime)
        {
            if (IsVisible && !IsPaused)
            {
                OnEntityFixedUpdate(deltaTime);
            }
        }
        
        /// <summary>
        /// 每帧更新
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            if (IsVisible && !IsPaused)
            {
                OnEntityUpdate(deltaTime);
            }
        }
        
        /// <summary>
        /// 延迟更新
        /// </summary>
        public void OnLateUpdate(float deltaTime)
        {
            if (IsVisible && !IsPaused)
            {
                OnEntityLateUpdate(deltaTime);
            }
        }
        
        /// <summary>
        /// 回收实体
        /// </summary>
        public void OnRecycle()
        {
            OnEntityRecycle();
            DisableCachedRenderers(); // 回收前隐藏渲染
            MoveToHiddenPosition(); // 回收时移动到隐藏位置
            Id = 0;
            UserData = null;
            IsPaused = false;
        }
        
        // IPoolable
        /// <summary>
        /// 对象池生成回调
        /// </summary>
        public void OnSpawn() { }
        /// <summary>
        /// 对象池回收回调
        /// </summary>
        public void OnDespawn() => OnRecycle();

        /// <summary>
        /// 应用预显示变换（由 userData 驱动）。
        /// </summary>
        /// <param name="userData">显示阶段用户数据。</param>
        private void ApplyPreShowTransform(object userData) // 预显示变换应用入口
        {
            if (userData is not IEntityPreShowTransform preShowTransform)
            {
                return; // 未实现接口时直接返回
            }

            var t = transform; // 获取 Transform
            if (t == null)
            {
                return; // Transform 为空时返回
            }

            if (preShowTransform.TryGetPreShowPosition(out var position))
            {
                t.position = position; // 设置预显示位置
            }

            if (preShowTransform.TryGetPreShowRotation(out var rotation))
            {
                t.rotation = rotation; // 设置预显示旋转
            }
        }

        /// <summary>
        /// 缓存渲染组件与默认启用状态（仅初始化一次）。
        /// </summary>
        private void CacheRenderersIfNeeded() // 渲染缓存初始化入口
        {
            if (_rendererCacheReady)
            {
                return; // 已缓存时直接返回
            }

            _cachedRenderers = GetComponentsInChildren<Renderer>(true); // 获取渲染组件集合
            if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            {
                _cachedRenderers = Array.Empty<Renderer>(); // 置空渲染缓存
                _cachedRendererDefaultStates = Array.Empty<bool>(); // 置空默认状态缓存
                _rendererCacheReady = true; // 标记缓存完成
                return; // 无渲染组件时退出
            }

            _cachedRendererDefaultStates = new bool[_cachedRenderers.Length]; // 分配默认状态缓存
            for (int i = 0; i < _cachedRenderers.Length; i++) // i 为索引
            {
                var renderer = _cachedRenderers[i]; // 获取当前渲染组件
                _cachedRendererDefaultStates[i] = renderer != null && renderer.enabled; // 记录默认启用状态
            }

            _rendererCacheReady = true; // 标记缓存完成
        }

        /// <summary>
        /// 禁用缓存中的所有渲染组件。
        /// </summary>
        protected void DisableCachedRenderers() // 渲染禁用入口
        {
            CacheRenderersIfNeeded(); // 确保渲染缓存可用
            if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            {
                return; // 无渲染组件时返回
            }

            for (int i = 0; i < _cachedRenderers.Length; i++) // i 为索引
            {
                var renderer = _cachedRenderers[i]; // 获取当前渲染组件
                if (renderer == null)
                {
                    continue; // 空组件时跳过
                }

                renderer.enabled = false; // 关闭渲染组件
            }
        }

        /// <summary>
        /// 恢复缓存中的渲染组件到默认启用状态。
        /// </summary>
        protected void RestoreCachedRenderersToDefault() // 渲染恢复入口
        {
            CacheRenderersIfNeeded(); // 确保渲染缓存可用
            if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            {
                return; // 无渲染组件时返回
            }

            var defaultStates = _cachedRendererDefaultStates; // 读取默认状态缓存
            if (defaultStates == null || defaultStates.Length == 0)
            {
                return; // 默认状态无效时返回
            }

            for (int i = 0; i < _cachedRenderers.Length; i++) // i 为索引
            {
                var renderer = _cachedRenderers[i]; // 获取当前渲染组件
                if (renderer == null)
                {
                    continue; // 空组件时跳过
                }

                renderer.enabled = defaultStates[i]; // 恢复默认启用状态
            }
        }

        /// <summary>
        /// 将实体移动到隐藏位置，避免对象池复用闪烁与误碰撞。
        /// </summary>
        private void MoveToHiddenPosition() // 隐藏位置移动入口
        {
            var t = transform; // 获取 Transform
            if (t == null)
            {
                return; // Transform 为空时返回
            }

            var pos = t.position; // 获取当前世界坐标
            pos.x = HiddenWorldPosition.x; // 写入隐藏 X
            pos.y = HiddenWorldPosition.y; // 写入隐藏 Y
            t.position = pos; // 写回世界坐标
        }
        
        // 子类重写
        /// <summary>
        /// 实体初始化（子类重写）
        /// </summary>
        protected virtual void OnEntityInit(object userData) { }
        /// <summary>
        /// 实体预显示（子类重写）。
        /// </summary>
        /// <param name="userData">显示阶段用户数据。</param>
        protected virtual void OnEntityPreShow(object userData) { }
        /// <summary>
        /// 实体显示（子类重写）
        /// </summary>
        protected virtual void OnEntityShow(object userData) { }
        /// <summary>
        /// 实体隐藏（子类重写）
        /// </summary>
        protected virtual void OnEntityHide() { }
        /// <summary>
        /// 实体暂停（子类重写）
        /// </summary>
        protected virtual void OnEntityPause() { }   // 实体暂停（暂停动画/特效）
        /// <summary>
        /// 实体恢复（子类重写）
        /// </summary>
        protected virtual void OnEntityResume() { }  // 实体恢复
        /// <summary>
        /// 固定帧更新（子类重写）
        /// </summary>
        protected virtual void OnEntityFixedUpdate(float deltaTime) { }  // 物理/AI 逻辑
        /// <summary>
        /// 每帧更新（子类重写）
        /// </summary>
        protected virtual void OnEntityUpdate(float deltaTime) { }       // 常规更新
        /// <summary>
        /// 延迟更新（子类重写）
        /// </summary>
        protected virtual void OnEntityLateUpdate(float deltaTime) { }   // 相机跟随等
        /// <summary>
        /// 实体回收（子类重写）
        /// </summary>
        protected virtual void OnEntityRecycle() { }
    }
    
    /// <summary>
    /// 实体信息
    /// </summary>
    public class EntityInfo
    {
        /// <summary>
        /// 实体类型标识
        /// </summary>
        public string EntityType;
        /// <summary>
        /// 实体预制体
        /// </summary>
        public GameObject Prefab;
        /// <summary>
        /// 预加载数量
        /// </summary>
        public int PreloadCount;
        /// <summary>
        /// 实体父节点
        /// </summary>
        public Transform Parent;
    }
    
    /// <summary>
    /// 实体管理器
    /// </summary>
    public class EntityManager : IInitializable, ITickable, IUpdateable, ILateUpdateable, IDisposableEx
    {
        /// <summary>
        /// 初始化顺序
        /// </summary>
        public int InitOrder => 60;  // 在 UIManager 之后初始化
        /// <summary>
        /// Tick 顺序
        /// </summary>
        public int TickOrder => 0;
        /// <summary>
        /// Update 顺序
        /// </summary>
        public int UpdateOrder => 0;
        /// <summary>
        /// LateUpdate 顺序
        /// </summary>
        public int LateUpdateOrder => 0;
        /// <summary>
        /// 释放顺序
        /// </summary>
        public int DisposeOrder => 0;
        
        /// <summary>
        /// 实体类型信息表
        /// </summary>
        private readonly Dictionary<string, EntityInfo> _entityInfos = new();
        /// <summary>
        /// 实体实例表（ID -> 实例）
        /// </summary>
        private readonly Dictionary<int, IEntity> _entities = new();
        /// <summary>
        /// 实体分组（类型 -> 实例列表）
        /// </summary>
        private readonly Dictionary<string, List<IEntity>> _entityGroups = new();
        /// <summary>
        /// 实体对象池（类型 -> 队列）
        /// </summary>
        private readonly Dictionary<string, Queue<IEntity>> _entityPools = new();

        /// <summary>
        /// 实体预制体元信息（路径/类型/分组）
        /// </summary>
        private struct EntityPrefabMeta
        {
            public string Path;
            public string EntityType;
            public string GroupName;
        }

        /// <summary>
        /// 空预显示数据结构（用于无预显示数据的统一入口）。
        /// </summary>
        private struct EmptyPreShowData // 空预显示数据结构
        {
        }

        /// <summary>
        /// 实体预制体元信息缓存（实体组件类型 -> 元信息，避免频繁反射）
        /// </summary>
        private readonly Dictionary<Type, EntityPrefabMeta> _entityPrefabAttributeCache = new(32);

        // HideAllEntities/HideAllEntities(string) 使用的复用缓冲，避免每次 new List 产生 GC
        private readonly List<IEntity> _hideBuffer = new(64);

        // 更新遍历缓冲：避免在实体 Update/Tick 中增删实体导致 Dictionary 遍历抛异常（InvalidOperationException）。
        // 说明：EntityManager 允许在实体逻辑内 Spawn/Recycle 其他实体，因此必须避免直接 foreach Dictionary.Values。
        private readonly List<IEntity> _updateBuffer = new(256);
        
        /// <summary>
        /// 下一个实体 ID
        /// </summary>
        private int _nextEntityId = 1;
        /// <summary>
        /// 实体根节点
        /// </summary>
        private Transform _entityRoot;
        /// <summary>
        /// 实体回收池根节点
        /// </summary>
        private Transform _poolRoot; // 实体回收站根节点
        
        // 配置
        /// <summary>
        /// 实体预制体路径前缀
        /// </summary>
        private string _entityPrefabPath = "Entities/";
        /// <summary>
        /// 默认预加载数量
        /// </summary>
        private int _defaultPreloadCount = 5;
        /// <summary>
        /// 对象池最大容量
        /// </summary>
        private int _maxPoolSize = 100;
        /// <summary>
        /// Update 间隔（跳帧倍数）
        /// </summary>
        private int _updateInterval = 1;
        /// <summary>
        /// 实体分组名称列表
        /// </summary>
        private string[] _entityGroupNames = { "Players", "Enemies", "NPCs", "Props", "Effects" };

        // Update/LateUpdate 帧计数：用于 UpdateInterval 跳帧
        private int _updateFrameCount;
        
        // 分组容器
        /// <summary>
        /// 分组容器（分组名 -> Transform）
        /// </summary>
        private readonly Dictionary<string, Transform> _groupContainers = new();
        
        /// <summary>
        /// 初始化（IInitializable 接口）
        /// </summary>
        public void Initialize()
        {
            Initialize(null);
        }

        /// <summary>
        /// 初始化（带实体根节点参数）
        /// </summary>
        /// <param name="entityRoot">实体的根节点（可选）。如果为 null，将查找或创建名为 [Entities] 的 DDOL 节点。</param>
        public void Initialize(Transform entityRoot)
        {
            // 从 CYConfigurator 读取配置
            // 配置中心
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                // 读取资源路径配置
                // 资源加载器配置
                var resourceConfig = configurator.GetConfig<ResourceLoaderConfig>();
                if (resourceConfig != null)
                {
                    _entityPrefabPath = resourceConfig.EntityPath;
                }
                
                // 读取实体管理器配置
                // 实体管理器配置
                var config = configurator.GetConfig<EntityManagerConfig>();
                if (config != null)
                {
                    _defaultPreloadCount = config.DefaultPreloadCount;
                    _maxPoolSize = config.MaxPoolSize;
                    _updateInterval = Mathf.Max(1, config.UpdateInterval);
                    if (config.EntityGroups != null && config.EntityGroups.Length > 0)
                    {
                        _entityGroupNames = config.EntityGroups;
                    }
                    CYLog.Debug("[EntityManager] 使用 CYConfigurator 配置");
                }
            }
            
            _entityRoot = entityRoot;
            if (_entityRoot == null)
            {
                // 先尝试查找场景中已存在的实体根节点
                // 场景中已有的实体根节点
                var existingRoot = GameObject.Find("[Entities]");
                if (existingRoot != null)
                {
                    _entityRoot = existingRoot.transform;
                    if (existingRoot.transform.parent == null)
                    {
                        GameObject.DontDestroyOnLoad(existingRoot);
                    }
                    CYLog.Debug("[EntityManager] 使用场景中已存在的 [Entities]");
                }
                else
                {
                    // 新创建的根节点对象
                    var go = new GameObject("[Entities]");
                    GameObject.DontDestroyOnLoad(go);
                    _entityRoot = go.transform;
                    CYLog.Debug("[EntityManager] 创建新的 [Entities] 根节点");
                }
            }
            
            // 创建回收池根节点（独立根节点，与 [UIPools] 保持一致）
            if (_poolRoot == null)
            {
                // 回收池根对象
                var poolGo = new GameObject("[EntityPools]");
                UnityEngine.Object.DontDestroyOnLoad(poolGo);
                _poolRoot = poolGo.transform;
                _poolRoot.gameObject.SetActive(false);
            }
            
            // 合并枚举分组到当前分组列表（去重）
            // 最终分组集合
            var finalGroups = new HashSet<string>(_entityGroupNames);
            foreach (var name in Enum.GetNames(typeof(EntityGroup)))
            {
                // 枚举分组名称
                // Default 通常作为根节点下的散养实体，或者不需要专门容器，看情况。这里如果是 Default 就不创建名为 "Default" 的节点了
                if (name != "Default") 
                {
                    finalGroups.Add(name);
                }
            }
            _entityGroupNames = new string[finalGroups.Count];
            finalGroups.CopyTo(_entityGroupNames);
            
            // 创建实体分组容器
            CreateEntityGroupContainers();
            
            CYLog.Debug("[EntityManager] 初始化完成");
        }
        
        /// <summary>
        /// 创建实体分组容器
        /// </summary>
        private void CreateEntityGroupContainers()
        {
            foreach (var groupName in _entityGroupNames)
            {
                // 当前分组名称
                // 先查找已存在的分组
                // 已存在的分组容器
                var existing = _entityRoot.Find(groupName);
                if (existing != null)
                {
                    _groupContainers[groupName] = existing;
                }
                else
                {
                    // 创建新分组
                    // 分组节点对象
                    var groupGo = new GameObject(groupName);
                    groupGo.transform.SetParent(_entityRoot);
                    _groupContainers[groupName] = groupGo.transform;
                }
            }
            CYLog.Debug($"[EntityManager] 已创建 {_groupContainers.Count} 个实体分组");
        }
        
        /// <summary>
        /// 获取实体分组容器
        /// </summary>
        public Transform GetGroupContainer(string groupName)
        {
            // 分组容器
            if (_groupContainers.TryGetValue(groupName, out var container))
            {
                return container;
            }
            
            // 如果分组不存在，动态创建
            return CreateGroup(groupName);
        }
        
        /// <summary>
        /// 创建新的实体分组
        /// </summary>
        /// <param name="groupName">分组名称</param>
        /// <returns>分组 Transform</returns>
        public Transform CreateGroup(string groupName)
        {
            // 已存在的分组容器
            if (_groupContainers.TryGetValue(groupName, out var existing))
            {
                CYLog.Warning($"[EntityManager] 分组已存在: {groupName}");
                return existing;
            }
            
            // 分组节点对象
            var groupGo = new GameObject(groupName);
            groupGo.transform.SetParent(_entityRoot);
            _groupContainers[groupName] = groupGo.transform;
            CYLog.Debug($"[EntityManager] 创建分组: {groupName}");
            return groupGo.transform;
        }
        
        /// <summary>
        /// 批量创建实体分组
        /// </summary>
        /// <param name="groupNames">分组名称数组</param>
        public void CreateGroups(params string[] groupNames)
        {
            foreach (var name in groupNames)
            {
                // 当前分组名称
                CreateGroup(name);
            }
        }
        
        /// <summary>
        /// 检查分组是否存在
        /// </summary>
        public bool HasGroup(string groupName)
        {
            return _groupContainers.ContainsKey(groupName);
        }
        
        /// <summary>
        /// 获取所有分组名称
        /// </summary>
        public string[] GetAllGroupNames()
        {
            // 分组名称数组
            var names = new string[_groupContainers.Count];
            _groupContainers.Keys.CopyTo(names, 0);
            return names;
        }
        
        /// <summary>
        /// 注册实体类型（直接传 Prefab）
        /// </summary>
        /// <param name="entityType">实体类型唯一标识符（Key）</param>
        /// <param name="prefab">实体预制体</param>
        /// <param name="preloadCount">预加载数量（放入对象池）</param>
        /// <param name="parent">父节点（可选，不传则使用默认或分组节点）</param>
        public void RegisterEntity(string entityType, GameObject prefab, int preloadCount = 0, Transform parent = null)
        {
            if (_entityInfos.ContainsKey(entityType))
            {
                // CYLog.Warning($"[EntityManager] 实体类型已注册: {entityType}"); // 允许重复注册，忽略即可
                return;
            }
            
            // 实体类型信息
            var info = new EntityInfo
            {
                EntityType = entityType,
                Prefab = prefab,
                PreloadCount = preloadCount,
                Parent = parent ?? _entityRoot
            };
            
            _entityInfos[entityType] = info;
            _entityGroups[entityType] = new List<IEntity>();
            _entityPools[entityType] = new Queue<IEntity>();
            
            // 预加载
            // 注意：预加载本质上是“预创建并放入池”，也受池容量上限影响，避免误配置导致内存暴涨。
            // 预热数量（受池上限限制）
            int warmupCount = preloadCount;
            if (_maxPoolSize > 0)
            {
                warmupCount = Mathf.Min(preloadCount, _maxPoolSize);
            }

            // i 为索引
            for (int i = 0; i < warmupCount; i++)
            {
                // 预创建的实体实例
                var entity = CreateEntityInstance(info);
                if (entity == null)
                {
                    continue;
                }
                entity.OnHide();
                _entityPools[entityType].Enqueue(entity);
            }
            
            if (warmupCount != preloadCount)
            {
                CYLog.Warning($"[EntityManager] 预加载数量被池上限截断: {entityType}, preload={preloadCount}, maxPool={_maxPoolSize}");
            }

            CYLog.Debug($"[EntityManager] 注册实体: {entityType}, 预加载: {warmupCount}");
        }

        /// <summary>
        /// 注册实体类型（自动加载资源）
        /// </summary>
        /// <param name="entityType">实体类型唯一标识符（Key）</param>
        /// <param name="assetPath">资源路径（相对于 Resources 或 Addressables，取决于加载器）</param>
        /// <param name="groupName">分组名称（如 "Players"），用于在 Hierarchy 中归类</param>
        /// <param name="preloadCount">预加载数量</param>
        /// <returns>是否注册成功</returns>
        public bool RegisterEntity(string entityType, string assetPath, string groupName = null, int preloadCount = 0)
        {
            if (_entityInfos.ContainsKey(entityType)) return true;

            // 资源加载器
            var loader = ServiceLocator.Get<CYFramework.Core.Resource.IResourceLoader>();
            if (loader == null)
            {
                CYLog.Error("[EntityManager] 自动注册失败：找不到 IResourceLoader 服务");
                return false;
            }

            // 加载的预制体
            var prefab = loader.Load<GameObject>(assetPath);
            if (prefab == null)
            {
                CYLog.Error($"[EntityManager] 自动注册失败：无法加载路径 {assetPath}");
                return false;
            }

            if (prefab.GetComponent<IEntity>() == null)
            {
                CYLog.Error($"[EntityManager] 自动注册失败：Prefab 未挂载 IEntity 组件 ({assetPath})");
                return false;
            }

            // 确定父节点：如果指定了 groupName，尝试获取/创建分组容器
            // 目标父节点
            Transform parent = null;
            if (!string.IsNullOrEmpty(groupName))
            {
                parent = GetGroupContainer(groupName);
            }

            RegisterEntity(entityType, prefab, preloadCount, parent);
            return true;
        }

        /// <summary>
        /// 注册实体类型（使用枚举分组）
        /// </summary>
        public bool RegisterEntity(string entityType, string assetPath, EntityGroup group, int preloadCount = 0)
        {
            return RegisterEntity(entityType, assetPath, group.ToString(), preloadCount);
        }

        /// <summary>
        /// 尝试从 EntityPrefabAttribute 获取元信息，并进行缓存
        /// </summary>
        private bool TryGetEntityPrefabMeta(Type entityComponentType, out EntityPrefabMeta meta)
        {
            if (_entityPrefabAttributeCache.TryGetValue(entityComponentType, out meta))
            {
                return !string.IsNullOrEmpty(meta.Path);
            }

            var attr = Attribute.GetCustomAttribute(entityComponentType, typeof(EntityPrefabAttribute)) as EntityPrefabAttribute;
            meta.Path = attr != null ? attr.Path : string.Empty;
            meta.EntityType = attr != null ? attr.EntityType : string.Empty;
            meta.GroupName = attr != null ? attr.GroupName : string.Empty;
            _entityPrefabAttributeCache[entityComponentType] = meta;
            return !string.IsNullOrEmpty(meta.Path);
        }

        /// <summary>
        /// 生成实体（使用枚举分组）
        /// </summary>
        /// <typeparam name="T">实体组件类型</typeparam>
        /// <param name="entityType">实体类型/Key</param>
        /// <param name="assetPath">资源路径</param>
        /// <param name="group">实体分组枚举</param>
        /// <param name="userData">用户数据（传递给 OnInit/OnShow）</param>
        /// <returns>实体实例</returns>
        public T SpawnEntity<T>(string entityType, string assetPath, EntityGroup group, object userData = null) where T : class, IEntity
        {
            return SpawnEntity<T>(entityType, assetPath, group.ToString(), userData);
        }

        /// <summary>
        /// 生成实体（使用枚举分组，预显示数据版）
        /// </summary>
        /// <typeparam name="T">实体组件类型</typeparam>
        /// <typeparam name="TData">预显示数据类型</typeparam>
        /// <param name="entityType">实体类型/Key</param>
        /// <param name="assetPath">资源路径</param>
        /// <param name="group">实体分组枚举</param>
        /// <param name="data">预显示数据（引用传递）</param>
        /// <param name="userData">用户数据（传递给 OnInit/OnShow）</param>
        /// <returns>实体实例</returns>
        public T SpawnEntity<T, TData>(string entityType, string assetPath, EntityGroup group, ref TData data, object userData = null)
            where T : class, IEntity
            where TData : struct
        {
            return SpawnEntity<T, TData>(entityType, assetPath, group.ToString(), ref data, userData); // 转发到字符串分组版本
        }

        /// <summary>
        /// 生成/显示实体（泛型版，推荐使用）
        /// </summary>
        public T SpawnEntity<T>(string entityType, object userData = null) where T : class, IEntity
        {
            EntityPrefabMeta meta = default;
            var hasMeta = false;
            var finalType = entityType;

            if (string.IsNullOrEmpty(finalType))
            {
                hasMeta = TryGetEntityPrefabMeta(typeof(T), out meta);
                finalType = !string.IsNullOrEmpty(meta.EntityType) ? meta.EntityType : typeof(T).Name;
            }

            if (!_entityInfos.ContainsKey(finalType))
            {
                if (!hasMeta && !TryGetEntityPrefabMeta(typeof(T), out meta))
                {
                    CYLog.Error($"[EntityManager] 未注册实体且未配置 EntityPrefabAttribute: {finalType}, Component={typeof(T).Name}");
                    return null;
                }

                var groupName = string.IsNullOrEmpty(meta.GroupName) ? null : meta.GroupName;
                if (!RegisterEntity(finalType, meta.Path, groupName))
                {
                    return null;
                }
            }
            return SpawnEntity(finalType, userData) as T;
        }

        /// <summary>
        /// 生成/显示实体（泛型版，预显示数据版）
        /// </summary>
        /// <typeparam name="T">实体组件类型</typeparam>
        /// <typeparam name="TData">预显示数据类型</typeparam>
        /// <param name="entityType">实体类型/Key</param>
        /// <param name="data">预显示数据（引用传递）</param>
        /// <param name="userData">用户数据（传递给 OnInit/OnShow）</param>
        /// <returns>实体实例</returns>
        public T SpawnEntity<T, TData>(string entityType, ref TData data, object userData = null)
            where T : class, IEntity
            where TData : struct
        {
            EntityPrefabMeta meta = default; // 预制体元信息
            var hasMeta = false; // 是否已获取元信息
            var finalType = entityType; // 最终实体类型

            if (string.IsNullOrEmpty(finalType))
            {
                hasMeta = TryGetEntityPrefabMeta(typeof(T), out meta); // 尝试获取元信息
                finalType = !string.IsNullOrEmpty(meta.EntityType) ? meta.EntityType : typeof(T).Name; // 生成最终类型
            }

            if (!_entityInfos.ContainsKey(finalType))
            {
                if (!hasMeta && !TryGetEntityPrefabMeta(typeof(T), out meta))
                {
                    CYLog.Error($"[EntityManager] 未注册实体且未配置 EntityPrefabAttribute: {finalType}, Component={typeof(T).Name}"); // 输出错误日志
                    return null; // 返回空实例
                }

                var groupName = string.IsNullOrEmpty(meta.GroupName) ? null : meta.GroupName; // 获取分组名称
                if (!RegisterEntity(finalType, meta.Path, groupName))
                {
                    return null; // 注册失败时返回空
                }
            }

            return SpawnEntityInternal(finalType, userData, ref data, true) as T; // 使用预显示数据生成实体
        }

        /// <summary>
        /// 生成/显示实体（直接使用组件类型名作为 EntityType）
        /// </summary>
        public T SpawnEntity<T>(object userData = null) where T : class, IEntity
        {
            return SpawnEntity<T>(string.Empty, userData);
        }

        /// <summary>
        /// 生成/显示实体（直接使用组件类型名作为 EntityType，预显示数据版）
        /// </summary>
        /// <typeparam name="T">实体组件类型</typeparam>
        /// <typeparam name="TData">预显示数据类型</typeparam>
        /// <param name="data">预显示数据（引用传递）</param>
        /// <param name="userData">用户数据（传递给 OnInit/OnShow）</param>
        /// <returns>实体实例</returns>
        public T SpawnEntity<T, TData>(ref TData data, object userData = null)
            where T : class, IEntity
            where TData : struct
        {
            return SpawnEntity<T, TData>(string.Empty, ref data, userData); // 转发到实体类型版本
        }

        /// <summary>
        /// 生成/显示实体（自动加载版，强烈推荐！）
        /// 从对象池获取或创建新实体
        /// </summary>
        /// <typeparam name="T">实体组件类型</typeparam>
        /// <param name="entityType">实体类型/Key</param>
        /// <param name="assetPath">资源路径（若未注册则自动注册）</param>
        /// <param name="groupName">分组名称</param>
        /// <param name="userData">用户数据</param>
        /// <returns>实体实例</returns>
        public T SpawnEntity<T>(string entityType, string assetPath, string groupName, object userData = null) where T : class, IEntity
        {
            // 尝试自动注册
            if (!_entityInfos.ContainsKey(entityType))
            {
                if (!RegisterEntity(entityType, assetPath, groupName))
                {
                    return null;
                }
            }
            return SpawnEntity<T>(entityType, userData);
        }

        /// <summary>
        /// 生成/显示实体（自动加载版，预显示数据版）
        /// 从对象池获取或创建新实体
        /// </summary>
        /// <typeparam name="T">实体组件类型</typeparam>
        /// <typeparam name="TData">预显示数据类型</typeparam>
        /// <param name="entityType">实体类型/Key</param>
        /// <param name="assetPath">资源路径（若未注册则自动注册）</param>
        /// <param name="groupName">分组名称</param>
        /// <param name="data">预显示数据（引用传递）</param>
        /// <param name="userData">用户数据</param>
        /// <returns>实体实例</returns>
        public T SpawnEntity<T, TData>(string entityType, string assetPath, string groupName, ref TData data, object userData = null)
            where T : class, IEntity
            where TData : struct
        {
            // 尝试自动注册
            if (!_entityInfos.ContainsKey(entityType))
            {
                if (!RegisterEntity(entityType, assetPath, groupName))
                {
                    return null; // 注册失败时返回空
                }
            }
            return SpawnEntity<T, TData>(entityType, ref data, userData); // 使用预显示数据生成实体
        }

        /// <summary>
        /// 生成/显示实体（自动加载版，默认分组）
        /// </summary>
        public T SpawnEntity<T>(string entityType, string assetPath, object userData = null) where T : class, IEntity
        {
            return SpawnEntity<T>(entityType, assetPath, null, userData);
        }

        /// <summary>
        /// 生成/显示实体（自动加载版，默认分组，预显示数据版）
        /// </summary>
        /// <typeparam name="T">实体组件类型</typeparam>
        /// <typeparam name="TData">预显示数据类型</typeparam>
        /// <param name="entityType">实体类型/Key</param>
        /// <param name="assetPath">资源路径（若未注册则自动注册）</param>
        /// <param name="data">预显示数据（引用传递）</param>
        /// <param name="userData">用户数据</param>
        /// <returns>实体实例</returns>
        public T SpawnEntity<T, TData>(string entityType, string assetPath, ref TData data, object userData = null)
            where T : class, IEntity
            where TData : struct
        {
            return SpawnEntity<T, TData>(entityType, assetPath, null, ref data, userData); // 转发到分组版本
        }

        /// <summary>
        /// 生成实体（基础实现）
        /// </summary>
        /// <param name="entityType">实体类型</param>
        /// <param name="userData">用户数据</param>
        /// <returns>实体接口</returns>
        public IEntity SpawnEntity(string entityType, object userData = null)
        {
            var emptyData = default(EmptyPreShowData); // 空预显示数据占位
            return SpawnEntityInternal(entityType, userData, ref emptyData, false); // 使用统一入口生成实体
        }

        /// <summary>
        /// 生成实体（内部实现，支持预显示数据注入）。
        /// </summary>
        /// <typeparam name="TData">预显示数据类型</typeparam>
        /// <param name="entityType">实体类型</param>
        /// <param name="userData">用户数据</param>
        /// <param name="data">预显示数据（引用传递）</param>
        /// <param name="applyPreShowData">是否应用预显示数据</param>
        /// <returns>实体接口</returns>
        private IEntity SpawnEntityInternal<TData>(string entityType, object userData, ref TData data, bool applyPreShowData)
            where TData : struct
        {
            // 实体类型信息
            if (!_entityInfos.TryGetValue(entityType, out var info))
            {
                CYLog.Error($"[EntityManager] 未注册的实体类型: {entityType}"); // 输出未注册错误
                return null; // 返回空实体
            }

            // 实体实例
            IEntity entity; // 目标实体实例

            // 从池中获取或创建新实体
            if (_entityPools[entityType].Count > 0)
            {
                entity = _entityPools[entityType].Dequeue(); // 从对象池取出实体
            }
            else
            {
                entity = CreateEntityInstance(info); // 创建新实体实例
            }

            // 初始化并显示
            // 分配实体 ID
            int entityId = _nextEntityId++; // 生成新的实体 ID

            // 确保父节点正确（如果是从池里取出来的，它可能在 PoolRoot 下）
            if (entity.GameObject.transform.parent != info.Parent)
            {
                entity.GameObject.transform.SetParent(info.Parent); // 修正实体父节点
            }

            entity.OnInit(entityId, userData); // 触发实体初始化

            if (applyPreShowData && entity is IEntityPreShowData<TData> preShowData)
            {
                preShowData.ApplyPreShowData(ref data); // 激活前应用预显示数据
            }

            // Spawn 时默认显示
            entity.OnShow(userData); // 触发实体显示

            _entities[entityId] = entity; // 注册实体到实例表
            _entityGroups[entityType].Add(entity); // 注册实体到分组列表

            return entity; // 返回实体实例
        }

        /// <summary>
        /// 回收实体（放回对象池）
        /// </summary>
        /// <param name="entityId">实体ID</param>
        public void RecycleEntity(int entityId)
        {
            // 目标实体实例
            if (!_entities.TryGetValue(entityId, out var entity))
            {
                return;
            }
            RecycleEntityInternal(entity);
        }
        
        /// <summary>
        /// 回收实体（放回对象池）
        /// </summary>
        /// <param name="entity">要回收的实体实例</param>
        public void RecycleEntity(IEntity entity)
        {
            if (entity == null) return;
            RecycleEntityInternal(entity);
        }
        
        /// <summary>
        /// 回收实体的内部流程
        /// </summary>
        private void RecycleEntityInternal(IEntity entity)
        {
            // 重要：回收时不能先调用 entity.OnRecycle()，因为 EntityBase.OnRecycle 会把 Id 重置为 0，
            // 若先重置再从 _entities 删除，会导致实体表残留（严重内存/逻辑错误）。
            // 实体 ID
            var entityId = entity.Id;
            // 实体类型
            var entityType = entity.EntityType;

            // 回收前先隐藏（如果还没隐藏）
            if (entity.IsVisible)
            {
                entity.OnHide();
            }

            // 先从管理器数据结构移除，保证后续 Update/Tick 不会再驱动到该实体。
            _entities.Remove(entityId);

            if (!string.IsNullOrEmpty(entityType) && _entityGroups.TryGetValue(entityType, out var group))
            {
                // 对应类型分组
                group.Remove(entity);
            }

            // 再触发回收回调（内部可安全重置 Id/UserData 等）。
            entity.OnRecycle();
            
            // 移入回收站节点（保持 Hierarchy 整洁）
            if (_poolRoot != null && entity.GameObject != null)
            {
                entity.GameObject.transform.SetParent(_poolRoot);
            }

            // 回收到池
            if (!string.IsNullOrEmpty(entityType) && _entityPools.TryGetValue(entityType, out var pool))
            {
                // 对应类型对象池
                // 池容量控制：超出上限直接销毁，避免长时间运行池无限增长。
                if (_maxPoolSize > 0 && pool.Count >= _maxPoolSize)
                {
                    if (entity.GameObject != null)
                    {
                        GameObject.Destroy(entity.GameObject);
                    }
                }
                else
                {
                    pool.Enqueue(entity);
                }
            }
            else
            {
                // 未知类型：为安全起见直接销毁，避免泄漏。
                if (entity.GameObject != null)
                {
                    GameObject.Destroy(entity.GameObject);
                }
            }
        }
        
        /// <summary>
        /// 仅隐藏实体（不回收）：保持 Entity 实例仍受管理器管理，但 SetActive(false) 且不再驱动 Update/Tick。
        /// </summary>
        public void HideEntityInstance(IEntity entity)
        {
            if (entity == null || !entity.IsVisible) return;
            entity.OnHide();
        }

        /// <summary>
        /// 仅显示实体（不回收）：将已隐藏且仍在管理器中管理的实体 SetActive(true)。
        /// </summary>
        /// <remarks>
        /// 注意：该方法只适用于“仍在 _entities 表内的隐藏实体”。如果实体已被 <see cref="RecycleEntity"/> 回收，
        /// 它的 Id 会被重置且不会再被管理器驱动，此时请通过 <see cref="SpawnEntity"/>/<see cref="SpawnEntity(string,object)"/> 重新生成。
        /// </summary>
        public void ShowEntity(IEntity entity, object userData = null)
        {
            if (entity == null || entity.IsVisible) return;

            // 防御：避免对“已回收（不再受管理器管理）”的实体直接 OnShow，造成状态与管理器表不一致。
            if (entity.Id <= 0 || !_entities.ContainsKey(entity.Id))
            {
                CYLog.Warning($"[EntityManager] ShowEntity 失败：实体不在管理器中（可能已回收），type={entity.EntityType}");
                return;
            }
            entity.OnShow(userData);
        }

        /// <summary>
        /// 设置实体可见性（不回收）。
        /// </summary>
        /// <remarks>
        /// 适用场景：你希望临时隐藏但保留实体的“管理器引用关系”（例如某些特殊逻辑需要仍能查询到该实体）。
        /// 如果你的目标是“从场景移除并等待复用”，请使用 <see cref="RecycleEntity(int)"/> 或 <see cref="RecycleEntity(IEntity)"/>。
        /// </remarks>
        public void SetEntityVisible(IEntity entity, bool visible, object userData = null)
        {
            if (entity == null) return;

            if (visible)
            {
                ShowEntity(entity, userData);
            }
            else
            {
                HideEntityInstance(entity);
            }
        }

        /// <summary>
        /// 回收所有指定类型的实体
        /// </summary>
        public void RecycleAllEntities(string entityType)
        {
            // 指定类型分组
            if (!_entityGroups.TryGetValue(entityType, out var group))
            {
                return;
            }
            
            _hideBuffer.Clear();
            _hideBuffer.AddRange(group);
            // i 为索引
            for (int i = 0; i < _hideBuffer.Count; i++)
            {
                RecycleEntityInternal(_hideBuffer[i]);
            }
        }
        
        /// <summary>
        /// 回收所有实体
        /// </summary>
        public void RecycleAllEntities()
        {
            _hideBuffer.Clear();
            _hideBuffer.AddRange(_entities.Values);
            // i 为索引
            for (int i = 0; i < _hideBuffer.Count; i++)
            {
                RecycleEntityInternal(_hideBuffer[i]);
            }
        }
        
        /// <summary>
        /// 获取实体
        /// </summary>
        public IEntity GetEntity(int entityId)
        {
            // 实体实例
            return _entities.TryGetValue(entityId, out var entity) ? entity : null;
        }
        
        /// <summary>
        /// 获取实体
        /// </summary>
        public T GetEntity<T>(int entityId) where T : class, IEntity
        {
            return GetEntity(entityId) as T;
        }
        
        /// <summary>
        /// 获取所有指定类型的实体
        /// </summary>
        public IReadOnlyList<IEntity> GetEntities(string entityType)
        {
            // 实体分组列表
            return _entityGroups.TryGetValue(entityType, out var group) ? group : Array.Empty<IEntity>();
        }
        
        /// <summary>
        /// 获取实体数量
        /// </summary>
        public int GetEntityCount(string entityType = null)
        {
            if (string.IsNullOrEmpty(entityType))
            {
                return _entities.Count;
            }
            
            // 实体分组列表
            return _entityGroups.TryGetValue(entityType, out var group) ? group.Count : 0;
        }
        
        /// <summary>
        /// 是否存在实体
        /// </summary>
        public bool HasEntity(int entityId)
        {
            return _entities.ContainsKey(entityId);
        }
        
        /// <summary>
        /// 暂停单个实体
        /// </summary>
        public void PauseEntity(int entityId)
        {
            if (_entities.TryGetValue(entityId, out var entity))
            {
                // 目标实体实例
                entity.OnPause();
            }
        }
        
        /// <summary>
        /// 恢复单个实体
        /// </summary>
        public void ResumeEntity(int entityId)
        {
            if (_entities.TryGetValue(entityId, out var entity))
            {
                // 目标实体实例
                entity.OnResume();
            }
        }
        
        /// <summary>
        /// 暂停指定类型的所有实体（分组暂停）
        /// </summary>
        public void PauseEntities(string entityType)
        {
            // 指定类型分组
            if (_entityGroups.TryGetValue(entityType, out var group))
            {
                foreach (var entity in group)
                {
                    // 分组中的实体
                    entity.OnPause();
                }
            }
        }
        
        /// <summary>
        /// 恢复指定类型的所有实体（分组恢复）
        /// </summary>
        public void ResumeEntities(string entityType)
        {
            // 指定类型分组
            if (_entityGroups.TryGetValue(entityType, out var group))
            {
                foreach (var entity in group)
                {
                    // 分组中的实体
                    entity.OnResume();
                }
            }
        }
        
        /// <summary>
        /// 暂停所有实体
        /// </summary>
        public void PauseAllEntities()
        {
            foreach (var entity in _entities.Values)
            {
                // 已注册的实体
                entity.OnPause();
            }
        }
        
        /// <summary>
        /// 恢复所有实体
        /// </summary>
        public void ResumeAllEntities()
        {
            foreach (var entity in _entities.Values)
            {
                // 已注册的实体
                entity.OnResume();
            }
        }
        
        /// <summary>
        /// 创建实体实例（实例化预制体）
        /// </summary>
        private IEntity CreateEntityInstance(EntityInfo info)
        {
            // 实体 GameObject
            var go = GameObject.Instantiate(info.Prefab, info.Parent);
            // 实体组件
            var entity = go.GetComponent<IEntity>();
            
            if (entity == null)
            {
                CYLog.Error($"[EntityManager] 预制体缺少 IEntity 组件: {info.EntityType}");
                GameObject.Destroy(go);
                return null;
            }
            
            // 注入 EntityType (解决对象池 Key 不一致问题)
            if (entity is EntityBase entityBase)
            {
                // EntityBase 实例
                entityBase.SetEntityType(info.EntityType);
            }
            
            return entity;
        }
        
        /// <summary>
        /// 固定帧更新（物理/AI）
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_entities.Count == 0) return;

            _updateBuffer.Clear();
            _updateBuffer.AddRange(_entities.Values);

            // i 为索引
            for (int i = 0; i < _updateBuffer.Count; i++)
            {
                // 当前实体
                var entity = _updateBuffer[i];
                if (entity == null) continue;
                if (entity.Id <= 0) continue; // 已回收
                if (!entity.IsVisible || entity.IsPaused) continue;
                entity.OnFixedUpdate(deltaTime);
            }
        }
        
        /// <summary>
        /// 每帧更新
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            if (_updateInterval > 1)
            {
                _updateFrameCount++;
                if ((_updateFrameCount % _updateInterval) != 0)
                {
                    return;
                }
            }

            if (_entities.Count == 0) return;

            _updateBuffer.Clear();
            _updateBuffer.AddRange(_entities.Values);

            // i 为索引
            for (int i = 0; i < _updateBuffer.Count; i++)
            {
                // 当前实体
                var entity = _updateBuffer[i];
                if (entity == null) continue;
                if (entity.Id <= 0) continue; // 已回收
                if (!entity.IsVisible || entity.IsPaused) continue;
                entity.OnUpdate(deltaTime);
            }
        }
        
        /// <summary>
        /// 延迟更新（相机跟随等）
        /// </summary>
        public void OnLateUpdate(float deltaTime)
        {
            if (_updateInterval > 1 && (_updateFrameCount % _updateInterval) != 0)
            {
                return;
            }

            if (_entities.Count == 0) return;

            _updateBuffer.Clear();
            _updateBuffer.AddRange(_entities.Values);

            // i 为索引
            for (int i = 0; i < _updateBuffer.Count; i++)
            {
                // 当前实体
                var entity = _updateBuffer[i];
                if (entity == null) continue;
                if (entity.Id <= 0) continue; // 已回收
                if (!entity.IsVisible || entity.IsPaused) continue;
                entity.OnLateUpdate(deltaTime);
            }
        }
        
        /// <summary>
        /// 释放实体管理器
        /// </summary>
        public void Dispose()
        {
            RecycleAllEntities();
            
            // 销毁池中的实体
            foreach (var pool in _entityPools.Values)
            {
                // 当前对象池
                while (pool.Count > 0)
                {
                    // 池中的实体
                    var entity = pool.Dequeue();
                    if (entity.GameObject != null)
                    {
                        GameObject.Destroy(entity.GameObject);
                    }
                }
            }
            
            _entityInfos.Clear();
            _entities.Clear();
            _entityGroups.Clear();
            _entityPools.Clear();
            
            if (_entityRoot != null)
            {
                GameObject.Destroy(_entityRoot.gameObject);
            }

            if (_poolRoot != null)
            {
                GameObject.Destroy(_poolRoot.gameObject);
            }
            
            CYLog.Debug("[EntityManager] 已销毁");
        }
    }
}
