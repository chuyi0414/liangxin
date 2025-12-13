using CYFramework;
using CYFramework.Infrastructure;
using UnityEngine;

/// <summary>
/// 老板管理器 [混合生命周期]
/// 既是 MonoBehaviour 可以挂载配置
/// 又是 CY 服务，实现了标准生命周期接口
/// </summary>
public class PlayerManager : MonoBehaviour, IInitializable, IUpdateable, IDisposableEx
{
    // ═══════════ Inspector 配置 ═══════════
    [Header("出生配置")]
    public Transform SpawnPoint;
    public int DefaultPlayerId = 1;

    // ═══════════ 运行时数据 ═══════════
    public GameObject Instance { get; private set; }
    public PlayerRow Data { get; private set; }
    public bool IsSpawned => Instance != null;

    // ═══════════ 框架生命周期接口 ═══════════
    public int InitOrder => 100;
    public int UpdateOrder => 0;
    public int DisposeOrder => 100;

    /// <summary>
    /// 框架初始化（由 Awake 驱动）
    /// </summary>
    public void Initialize()
    {
        CY.Log("[PlayerManager] Initialize (CY生命周期)");
        // 在这里做初始化逻辑，例如预加载
    }

    /// <summary>
    /// 框架每帧更新（由 Update 驱动）
    /// </summary>
    public void OnUpdate(float deltaTime)
    {
        // 如果有帧逻辑写在这里
    }

    /// <summary>
    /// 框架销毁（由 OnDestroy 驱动）
    /// </summary>
    public void Dispose()
    {
        Despawn();
        CY.Log("[PlayerManager] Dispose (CY生命周期)");
    }

    // ═══════════ Unity 生命周期 (桥接层) ═══════════
    
    private void Awake()
    {
        // 1. 注册服务
        // 注册时会触发 ServiceLocator 事件 -> 通知 CYBootstrap -> 自动将我加入 Update 循环
        if (!ServiceLocator.IsRegistered<PlayerManager>())
        {
            ServiceLocator.RegisterInstance(this);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 2. 初始化
        Initialize();
    }

    // 注意：不再需要 Unity Update 驱动，框架会自动调用 OnUpdate
    // private void Update() { }

    private void OnDestroy()
    {
        // 1. 销毁
        Dispose();
        
        // 2. 注销服务（自动触发 CYBootstrap 移除 Update 循环）
        if (ServiceLocator.IsRegistered<PlayerManager>())
        {
            ServiceLocator.Unregister<PlayerManager>();
        }
    }
    
    // ═══════════ 业务方法 ═══════════

    public void Spawn(int? playerId = null)
    {
        if (IsSpawned) return;

        int id = playerId ?? DefaultPlayerId;
        var table = CY.Data.GetDataTable<PlayerRow>("Player");
        if (table == null) return;
        
        Data = table.GetRow(id);
        if (Data == null) return;
        
        // 1. 准备并显示实体 (EntityManager 自动处理加载与注册)
        string entityKey = $"Player_{Data.Id}"; 
        
        var entity = CY.Entity.SpawnEntity<PlayerEntity>(entityKey, Data.PrefabPath, CYFramework.Core.Entity.EntityGroup.Players, Data);
        
        if (entity != null)
        {
            Instance = entity.gameObject;
            
            // 3. 设置出生位置
            Vector3 spawnPos = SpawnPoint != null ? SpawnPoint.position : transform.position;
            Quaternion spawnRot = SpawnPoint != null ? SpawnPoint.rotation : transform.rotation;
            
            // 如果 Entity 内部有 Warp 方法最好，否则直接设置 Transform
            Instance.transform.position = spawnPos;
            Instance.transform.rotation = spawnRot;
            Instance.name = $"Player_{Data.Name}";
            
            CY.Log($"[PlayerManager] Spawn Success: {Data.Name}");
        }
    }
    
    public void Despawn()
    {
        if (Instance != null)
        {
            // 通过 Entity 系统回收
            var entity = Instance.GetComponent<PlayerEntity>();
            if (entity != null)
            {
                CY.Entity.RecycleEntity(entity);
            }
            else
            {
                // 如果找不到实体脚本（异常情况），强制销毁
                Destroy(Instance);
            }
            
            Instance = null;
            Data = null;
        }
    }
}

