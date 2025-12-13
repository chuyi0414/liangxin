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
        int Id { get; }
        string EntityType { get; }
        bool IsVisible { get; }
        bool IsPaused { get; }
        GameObject GameObject { get; }
        
        void OnInit(int id, object userData);
        void OnShow(object userData);
        void OnHide();
        void OnPause();
        void OnResume();
        void OnFixedUpdate(float deltaTime);
        void OnUpdate(float deltaTime);
        void OnLateUpdate(float deltaTime);
        void OnRecycle();
    }
    
    /// <summary>
    /// 实体基类
    /// </summary>
    public abstract class EntityBase : MonoBehaviour, IEntity, IPoolable
    {
        public int Id { get; private set; }
        
        private string _entityType;
        public string EntityType 
        { 
            get => _entityType; 
            protected set => _entityType = value; 
        }

        public void SetEntityType(string type) => _entityType = type;

        public bool IsVisible { get; private set; }
        public bool IsPaused { get; private set; }
        public GameObject GameObject => gameObject;
        
        protected object UserData { get; private set; }
        
        public void OnInit(int id, object userData)
        {
            Id = id;
            UserData = userData;
            IsPaused = false;
            OnEntityInit(userData);
        }
        
        public void OnShow(object userData)
        {
            UserData = userData;
            IsVisible = true;
            IsPaused = false;
            gameObject.SetActive(true);
            OnEntityShow(userData);
        }
        
        public void OnHide()
        {
            IsVisible = false;
            OnEntityHide();
            gameObject.SetActive(false);
        }
        
        public void OnPause()
        {
            if (IsPaused) return;
            IsPaused = true;
            OnEntityPause();
        }
        
        public void OnResume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            OnEntityResume();
        }
        
        public void OnFixedUpdate(float deltaTime)
        {
            if (IsVisible && !IsPaused)
            {
                OnEntityFixedUpdate(deltaTime);
            }
        }
        
        public void OnUpdate(float deltaTime)
        {
            if (IsVisible && !IsPaused)
            {
                OnEntityUpdate(deltaTime);
            }
        }
        
        public void OnLateUpdate(float deltaTime)
        {
            if (IsVisible && !IsPaused)
            {
                OnEntityLateUpdate(deltaTime);
            }
        }
        
        public void OnRecycle()
        {
            OnEntityRecycle();
            Id = 0;
            UserData = null;
            IsPaused = false;
        }
        
        // IPoolable
        public void OnSpawn() { }
        public void OnDespawn() => OnRecycle();
        
        // 子类重写
        protected virtual void OnEntityInit(object userData) { }
        protected virtual void OnEntityShow(object userData) { }
        protected virtual void OnEntityHide() { }
        protected virtual void OnEntityPause() { }   // 实体暂停（暂停动画/特效）
        protected virtual void OnEntityResume() { }  // 实体恢复
        protected virtual void OnEntityFixedUpdate(float deltaTime) { }  // 物理/AI 逻辑
        protected virtual void OnEntityUpdate(float deltaTime) { }       // 常规更新
        protected virtual void OnEntityLateUpdate(float deltaTime) { }   // 相机跟随等
        protected virtual void OnEntityRecycle() { }
    }
    
    /// <summary>
    /// 实体信息
    /// </summary>
    public class EntityInfo
    {
        public string EntityType;
        public GameObject Prefab;
        public int PreloadCount;
        public Transform Parent;
    }
    
    /// <summary>
    /// 实体管理器
    /// </summary>
    public class EntityManager : IInitializable, ITickable, IUpdateable, ILateUpdateable, IDisposableEx
    {
        public int InitOrder => 60;  // 在 UIManager 之后初始化
        public int TickOrder => 0;
        public int UpdateOrder => 0;
        public int LateUpdateOrder => 0;
        public int DisposeOrder => 0;
        
        private readonly Dictionary<string, EntityInfo> _entityInfos = new();
        private readonly Dictionary<int, IEntity> _entities = new();
        private readonly Dictionary<string, List<IEntity>> _entityGroups = new();
        private readonly Dictionary<string, Queue<IEntity>> _entityPools = new();

        // HideAllEntities/HideAllEntities(string) 使用的复用缓冲，避免每次 new List 产生 GC
        private readonly List<IEntity> _hideBuffer = new(64);
        
        private int _nextEntityId = 1;
        private Transform _entityRoot;
        private Transform _poolRoot; // 实体回收站根节点
        
        // 配置
        private string _entityPrefabPath = "Entities/";
        private int _defaultPreloadCount = 5;
        private int _maxPoolSize = 100;
        private int _updateInterval = 1;
        private string[] _entityGroupNames = { "Players", "Enemies", "NPCs", "Props", "Effects" };

        // Update/LateUpdate 帧计数：用于 UpdateInterval 跳帧
        private int _updateFrameCount;
        
        // 分组容器
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
             var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                // 读取资源路径配置
                var resourceConfig = configurator.GetConfig<ResourceLoaderConfig>();
                if (resourceConfig != null)
                {
                    _entityPrefabPath = resourceConfig.EntityPath;
                }
                
                // 读取实体管理器配置
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
                    var go = new GameObject("[Entities]");
                    GameObject.DontDestroyOnLoad(go);
                    _entityRoot = go.transform;
                    CYLog.Debug("[EntityManager] 创建新的 [Entities] 根节点");
                }
            }
            
            // 创建回收池根节点（独立根节点，与 [UIPools] 保持一致）
            if (_poolRoot == null)
            {
                var poolGo = new GameObject("[EntityPools]");
                UnityEngine.Object.DontDestroyOnLoad(poolGo);
                _poolRoot = poolGo.transform;
                _poolRoot.gameObject.SetActive(false);
            }
            
            // 合并枚举分组到当前分组列表（去重）
            var finalGroups = new HashSet<string>(_entityGroupNames);
            foreach (var name in Enum.GetNames(typeof(EntityGroup)))
            {
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
                // 先查找已存在的分组
                var existing = _entityRoot.Find(groupName);
                if (existing != null)
                {
                    _groupContainers[groupName] = existing;
                }
                else
                {
                    // 创建新分组
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
            if (_groupContainers.TryGetValue(groupName, out var existing))
            {
                CYLog.Warning($"[EntityManager] 分组已存在: {groupName}");
                return existing;
            }
            
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
            for (int i = 0; i < preloadCount; i++)
            {
                var entity = CreateEntityInstance(info);
                entity.OnHide();
                _entityPools[entityType].Enqueue(entity);
            }
            
            CYLog.Debug($"[EntityManager] 注册实体: {entityType}, 预加载: {preloadCount}");
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

            var loader = ServiceLocator.Get<CYFramework.Core.Resource.IResourceLoader>();
            if (loader == null)
            {
                CYLog.Error("[EntityManager] 自动注册失败：找不到 IResourceLoader 服务");
                return false;
            }

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
        /// 生成/显示实体（泛型版，推荐使用）
        /// </summary>
        public T SpawnEntity<T>(string entityType, object userData = null) where T : class, IEntity
        {
            return SpawnEntity(entityType, userData) as T;
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

        // 重载：为了方便，如果不传 groupName
        public T SpawnEntity<T>(string entityType, string assetPath, object userData = null) where T : class, IEntity
        {
            return SpawnEntity<T>(entityType, assetPath, null, userData);
        }
        
        /// <summary>
        /// 生成实体（基础实现）
        /// </summary>
        /// <param name="entityType">实体类型</param>
        /// <param name="userData">用户数据</param>
        /// <returns>实体接口</returns>
        public IEntity SpawnEntity(string entityType, object userData = null)
        {
            if (!_entityInfos.TryGetValue(entityType, out var info))
            {
                CYLog.Error($"[EntityManager] 未注册的实体类型: {entityType}");
                return null;
            }
            
            IEntity entity;
            
            // 从池中获取或创建新实体
            if (_entityPools[entityType].Count > 0)
            {
                entity = _entityPools[entityType].Dequeue();
            }
            else
            {
                entity = CreateEntityInstance(info);
            }
            
            // 初始化并显示
            int entityId = _nextEntityId++;
            
            // 确保父节点正确（如果是从池里取出来的，它可能在 PoolRoot 下）
            if (entity.GameObject.transform.parent != info.Parent)
            {
                entity.GameObject.transform.SetParent(info.Parent);
            }
            
            entity.OnInit(entityId, userData);
            // Spawn 时默认显示
            entity.OnShow(userData);
            
            _entities[entityId] = entity;
            _entityGroups[entityType].Add(entity);
            
            return entity;
        }

        /// <summary>
        /// 回收实体（放回对象池）
        /// </summary>
        /// <param name="entityId">实体ID</param>
        public void RecycleEntity(int entityId)
        {
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
        
        private void RecycleEntityInternal(IEntity entity)
        {
            // 回收前先隐藏（如果还没隐藏）
            if (entity.IsVisible)
            {
                entity.OnHide();
            }
            
            // 触发回收回调
            entity.OnRecycle();
            
            _entities.Remove(entity.Id);
            
            if (_entityGroups.TryGetValue(entity.EntityType, out var group))
            {
                group.Remove(entity);
            }
            
            // 移入回收站节点（保持 Hierarchy 整洁）
            if (_poolRoot != null)
            {
                entity.GameObject.transform.SetParent(_poolRoot);
            }

            // 回收到池
            if (_entityPools.TryGetValue(entity.EntityType, out var pool))
            {
                pool.Enqueue(entity);
            }
        }
        
        /// <summary>
        /// 仅隐藏实体（即使不回收）
        /// 保持 Entity 实例在内存中，不放回池，只是 SetActive(false)
        /// </summary>
        public void HideEntity(IEntity entity)
        {
            if (entity == null || !entity.IsVisible) return;
            entity.OnHide();
        }

        /// <summary>
        /// 仅显示实体（即使不回收）
        /// 将已隐藏的实体 SetActive(true)
        /// </summary>
        public void ShowEntity(IEntity entity, object userData = null)
        {
            if (entity == null || entity.IsVisible) return;
            entity.OnShow(userData);
        }

        /// <summary>
        /// 回收所有指定类型的实体
        /// </summary>
        public void RecycleAllEntities(string entityType)
        {
            if (!_entityGroups.TryGetValue(entityType, out var group))
            {
                return;
            }
            
            _hideBuffer.Clear();
            _hideBuffer.AddRange(group);
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
            for (int i = 0; i < _hideBuffer.Count; i++)
            {
                RecycleEntityInternal(_hideBuffer[i]);
            }
        }

        // --- 旧 API 废弃/重命名映射 ---
        
        [Obsolete("请使用 RecycleEntity")]
        public void HideAllEntities(string entityType) => RecycleAllEntities(entityType);
        
        [Obsolete("请使用 RecycleAllEntities")]
        public void HideAllEntities() => RecycleAllEntities();

        [Obsolete("请使用 RecycleEntity")]
        public bool HideIfExists(int entityId)
        {
            if (!_entities.TryGetValue(entityId, out var entity)) return false;
            RecycleEntityInternal(entity);
            return true;
        }
        
        /// <summary>
        /// 获取实体
        /// </summary>
        public IEntity GetEntity(int entityId)
        {
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
                entity.OnResume();
            }
        }
        
        /// <summary>
        /// 暂停指定类型的所有实体（分组暂停）
        /// </summary>
        public void PauseEntities(string entityType)
        {
            if (_entityGroups.TryGetValue(entityType, out var group))
            {
                foreach (var entity in group)
                {
                    entity.OnPause();
                }
            }
        }
        
        /// <summary>
        /// 恢复指定类型的所有实体（分组恢复）
        /// </summary>
        public void ResumeEntities(string entityType)
        {
            if (_entityGroups.TryGetValue(entityType, out var group))
            {
                foreach (var entity in group)
                {
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
                entity.OnResume();
            }
        }
        
        private IEntity CreateEntityInstance(EntityInfo info)
        {
            var go = GameObject.Instantiate(info.Prefab, info.Parent);
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
                entityBase.SetEntityType(info.EntityType);
            }
            
            return entity;
        }
        
        // ITickable - 固定帧更新（物理/AI）
        public void Tick(float deltaTime)
        {
            foreach (var entity in _entities.Values)
            {
                entity.OnFixedUpdate(deltaTime);
            }
        }
        
        // IUpdateable - 每帧更新
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

            foreach (var entity in _entities.Values)
            {
                entity.OnUpdate(deltaTime);
            }
        }
        
        // ILateUpdateable - 延迟更新（相机跟随等）
        public void OnLateUpdate(float deltaTime)
        {
            if (_updateInterval > 1 && (_updateFrameCount % _updateInterval) != 0)
            {
                return;
            }

            foreach (var entity in _entities.Values)
            {
                entity.OnLateUpdate(deltaTime);
            }
        }
        
        // IDisposableEx
        public void Dispose()
        {
            RecycleAllEntities();
            
            // 销毁池中的实体
            foreach (var pool in _entityPools.Values)
            {
                while (pool.Count > 0)
                {
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
