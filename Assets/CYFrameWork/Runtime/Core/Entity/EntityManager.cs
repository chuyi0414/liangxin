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
        public abstract string EntityType { get; }
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
    public class EntityManager : ITickable, IUpdateable, ILateUpdateable, IDisposableEx
    {
        public int TickOrder => 0;
        public int UpdateOrder => 0;
        public int LateUpdateOrder => 0;
        public int DisposeOrder => 0;
        
        private readonly Dictionary<string, EntityInfo> _entityInfos = new();
        private readonly Dictionary<int, IEntity> _entities = new();
        private readonly Dictionary<string, List<IEntity>> _entityGroups = new();
        private readonly Dictionary<string, Queue<IEntity>> _entityPools = new();
        
        private int _nextEntityId = 1;
        private Transform _entityRoot;
        
        // 配置
        private string _entityPrefabPath = "Entities/";
        private int _defaultPreloadCount = 5;
        private int _maxPoolSize = 100;
        private string[] _entityGroupNames = { "Players", "Enemies", "NPCs", "Props", "Effects" };
        
        // 分组容器
        private readonly Dictionary<string, Transform> _groupContainers = new();
        
        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(Transform entityRoot = null)
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
                    GameObject.DontDestroyOnLoad(existingRoot);
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
        /// 注册实体类型
        /// </summary>
        public void RegisterEntity(string entityType, GameObject prefab, int preloadCount = 0, Transform parent = null)
        {
            if (_entityInfos.ContainsKey(entityType))
            {
                CYLog.Warning($"[EntityManager] 实体类型已注册: {entityType}");
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
        /// 显示实体
        /// </summary>
        public T ShowEntity<T>(string entityType, object userData = null) where T : class, IEntity
        {
            return ShowEntity(entityType, userData) as T;
        }
        
        /// <summary>
        /// 显示实体
        /// </summary>
        public IEntity ShowEntity(string entityType, object userData = null)
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
            entity.OnInit(entityId, userData);
            entity.OnShow(userData);
            
            _entities[entityId] = entity;
            _entityGroups[entityType].Add(entity);
            
            return entity;
        }
        
        /// <summary>
        /// 隐藏实体
        /// </summary>
        public void HideEntity(int entityId)
        {
            if (!_entities.TryGetValue(entityId, out var entity))
            {
                return;
            }
            
            HideEntityInternal(entity);
        }
        
        /// <summary>
        /// 隐藏实体
        /// </summary>
        public void HideEntity(IEntity entity)
        {
            if (entity == null) return;
            HideEntityInternal(entity);
        }
        
        private void HideEntityInternal(IEntity entity)
        {
            entity.OnHide();
            
            _entities.Remove(entity.Id);
            
            if (_entityGroups.TryGetValue(entity.EntityType, out var group))
            {
                group.Remove(entity);
            }
            
            // 回收到池
            if (_entityPools.TryGetValue(entity.EntityType, out var pool))
            {
                entity.OnRecycle();  // 回收前调用
                pool.Enqueue(entity);
            }
        }
        
        /// <summary>
        /// 隐藏所有指定类型的实体
        /// </summary>
        public void HideAllEntities(string entityType)
        {
            if (!_entityGroups.TryGetValue(entityType, out var group))
            {
                return;
            }
            
            // 复制列表避免迭代时修改
            var entities = new List<IEntity>(group);
            foreach (var entity in entities)
            {
                HideEntityInternal(entity);
            }
        }
        
        /// <summary>
        /// 隐藏所有实体
        /// </summary>
        public void HideAllEntities()
        {
            var entities = new List<IEntity>(_entities.Values);
            foreach (var entity in entities)
            {
                HideEntityInternal(entity);
            }
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
            foreach (var entity in _entities.Values)
            {
                entity.OnUpdate(deltaTime);
            }
        }
        
        // ILateUpdateable - 延迟更新（相机跟随等）
        public void OnLateUpdate(float deltaTime)
        {
            foreach (var entity in _entities.Values)
            {
                entity.OnLateUpdate(deltaTime);
            }
        }
        
        // IDisposableEx
        public void Dispose()
        {
            HideAllEntities();
            
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
            
            CYLog.Debug("[EntityManager] 已销毁");
        }
    }
}
