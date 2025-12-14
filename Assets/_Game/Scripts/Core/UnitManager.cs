using CYFramework;
using CYFramework.Infrastructure;
using UnityEngine;
using System.Collections.Generic;
using CYFramework.Core.Entity; // 需要引用实体系统

/// <summary>
/// 单位管理器 (原 RecruitmentManager)
/// 负责：管理所有友方单位(老板+员工)、招聘逻辑、单位索敌支持
/// </summary>
public class UnitManager : MonoBehaviour, IInitializable, IUpdateable, IDisposableEx
{
    // ═══════════ 配置 ═══════════
    [Header("基础配置")]
    [SerializeField] private GameObject _baseCamp; // 大本营位置

    // ═══════════ 运行时数据 ═══════════
    
    /// <summary>
    /// 大本营位置
    /// </summary>
    public Transform BaseCampPoint => _baseCamp != null ? _baseCamp.transform : null;

    /// <summary>
    /// 当前操控的老板(玩家)实体
    /// </summary>
    public PlayerEntity CurrentPlayer { get; private set; }

    /// <summary>
    /// 所有活跃的友方单位（老板 + 员工）
    /// 用于敌人 AI 索敌
    /// </summary>
    public List<EntityBase> ActiveFriendlyUnits { get; private set; } = new List<EntityBase>();

    private List<int> _deployedUnitIds = new List<int>(); // 旧的 ID 列表，暂时保留用于逻辑兼容

    // ═══════════ 框架生命周期 ═══════════
    /// <summary>
    /// 初始化顺序：120 (晚于 WaveManager，确保基础环境已就绪)
    /// </summary>
    public int InitOrder => 120;
    public int UpdateOrder => 0; 
    public int DisposeOrder => 120;

    /// <summary>
    /// 框架初始化
    /// </summary>
    public void Initialize()
    {
        CY.Log("[UnitManager] Initialize");
        ActiveFriendlyUnits.Clear();
        _deployedUnitIds.Clear();
        CurrentPlayer = null;
    }

    /// <summary>
    /// 框架每帧更新
    /// </summary>
    public void OnUpdate(float deltaTime)
    {
        // 自动增长经费等逻辑
        // 也可以在这里清理 ActiveFriendlyUnits 中已销毁的单位 (如果 Entity 回收没有回调的话)
    }

    /// <summary>
    /// 框架销毁清理
    /// </summary>
    public void Dispose()
    {
        CY.Log("[UnitManager] Dispose");
        ActiveFriendlyUnits.Clear();
        _deployedUnitIds.Clear();
        CurrentPlayer = null;
    }

    // ═══════════ Unity 桥接 ═══════════
    private void Awake()
    {
        // 自动注册到服务定位器
        if (!ServiceLocator.IsRegistered<UnitManager>())
        {
            ServiceLocator.RegisterInstance(this);
        }
        else
        {
            // 防止重复挂载
            Destroy(gameObject);
            Initialize();
        }
    }

    private void OnDestroy()
    {
        Dispose();
        if (ServiceLocator.IsRegistered<UnitManager>())
        {
            ServiceLocator.Unregister<UnitManager>();
        }
    }

    // ═══════════ 业务逻辑：老板 (Player) ═══════════

    public void SpawnPlayer(int? playerId = null)
    {
        // 如果已经有 Player，先销毁
        if (CurrentPlayer != null)
        {
            DespawnPlayer();
        }

        // 默认ID
        int id = playerId ?? 1; // 默认 1
        
        // 1. 读取配置表 (PlayerRow)
        // 修正：没有 GetRow<T> 扩展方法，需要先 GetDataTable<T> 再 GetRow
        var table = CY.Data.GetDataTable<PlayerRow>("Player");
        if (table == null)
        {
             CY.LogError($"[UnitManager] 找不到 Player 数据表！");
             return;
        }
        var playerRow = table.GetRow(id);
        if (playerRow == null)
        {
            CY.LogError($"[UnitManager] 找不到 Player ID: {id} 的配置数据！");
            return;
        }

        CY.Log($"[UnitManager] 准备生成玩家: {playerRow.Name}, Prefab: {playerRow.PrefabPath}");

        // 使用属性获取 Key，保持逻辑内聚
        var entity = CY.Entity.SpawnEntity<PlayerEntity>(playerRow.EntityKey, playerRow.PrefabPath, EntityGroup.Players, playerRow);

        CY.Log($"[UnitManager] 已请求生成玩家实体: {playerRow.EntityKey}");
    }

    public void DespawnPlayer()
    {
        if (CurrentPlayer != null)
        {
            ActiveFriendlyUnits.Remove(CurrentPlayer);
            // 必须使用 RecycleEntity 才能将对象放回池中复用
            // HideEntity 仅仅是隐藏，不会进入空闲池，再次 Spawn 会创建新对象导致泄漏
            CY.Entity.RecycleEntity(CurrentPlayer);
            CurrentPlayer = null;
        }
    }
    
    // ═══════════ 业务逻辑：员工 (Recruitment) ═══════════

    /// <summary>
    /// 注册单位到友方列表 (当单位出生时调用)
    /// </summary>
    public void RegisterUnit(EntityBase unit)
    {
        if (unit == null) return;
        if (!ActiveFriendlyUnits.Contains(unit))
        {
            ActiveFriendlyUnits.Add(unit);
            
            if (unit is PlayerEntity player)
            {
                CurrentPlayer = player;
            }
        }
    }

    public void UnregisterUnit(EntityBase unit)
    {
        if (unit != null)
        {
            ActiveFriendlyUnits.Remove(unit);
            if (unit == CurrentPlayer) CurrentPlayer = null;
        }
    }

    public bool TrySpendCost(int amount)
    {
        return CY.Department.TrySpendGold(amount);
    }

    public void AddCost(int amount)
    {
        CY.Department.ChangeGold(amount);
    }

    public void RecruitUnit(int unitDataId, Vector3 position)
    {
        // 1. 获取配置数据
        var table = CY.Data.GetDataTable<EmployeeRow>("Employees");
        if (table == null)
        {
            CY.LogError($"[UnitManager] 找不到 Employees 数据表！");
            return;
        }

        var empRow = table.GetRow(unitDataId);
        if (empRow == null)
        {
            CY.LogError($"[UnitManager] 找不到 Employee ID: {unitDataId} 的配置数据！");
            return;
        }

        // 2. 检查并扣除费用
        if (!CY.Department.HasGold(empRow.RecruitCost))
        {
            CY.UI.ShowToast($"资金不足！需要 {empRow.RecruitCost}");
            return;
        }

        if (CY.Department.TrySpendGold(empRow.RecruitCost))
        {
            // 3. 生成实体
            // 使用属性获取 Key，保持逻辑内聚
            var empEntity = CY.Entity.SpawnEntity<EmployeeEntity>(
                empRow.EntityKey, 
                empRow.PrefabPath, 
                EntityGroup.NPCs, // 归类为 NPC
                empRow
            );

            if (empEntity != null)
            {
                empEntity.GameObject.transform.position = position;
                CY.Log($"[UnitManager] 成功招聘员工: {empRow.JobTitle} (消耗 {empRow.RecruitCost} 金)，位置: {position}");
            }
            else
            {
                CY.LogError($"[UnitManager] 员工实体生成失败! Path: {empRow.PrefabPath}");
            }
        }
    }
}
