using CYFramework;
using CYFramework.Infrastructure;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// 资源与部门管理器
/// 负责：
/// 1. 核心资源 (Gold, Conscience, Corruption)
/// 2. 部门强度计算 (用于总监大招)
/// </summary>
public class DepartmentManager : MonoBehaviour, IInitializable, IUpdateable, IDisposableEx
{
    // ═══════════ 配置 ═══════════
    // ═══════════ 配置 (从 GlobalConfig 表读取) ═══════════
    // ═══════════ 配置 (从 GlobalConfig 表读取) ═══════════
    /// <summary>
    /// 公司良心值上限 (内部存储)
    /// </summary>
    private int _maxConscience = 100;
    
    /// <summary>
    /// 公司黑心值上限 (内部存储)
    /// </summary>
    private int _maxCompanyCorruption = 100;

    /// <summary>
    /// 公司良心值上限
    /// </summary>
    public int MaxConscience => _maxConscience;
    
    /// <summary>
    /// 公司黑心值上限
    /// </summary>
    public int MaxCompanyCorruption => _maxCompanyCorruption;

    // ═══════════ 运行时数据 (资源) ═══════════
    
    public class RuntimeData
    {
        /// <summary>
        /// 当前资金 (用于招募员工、购买道具)
        /// </summary>
        public int Gold;
        
        /// <summary>
        /// 当前良心资源 (玩家持有，用于安抚员工、特殊选项)
        /// </summary>
        public int ConscienceResource;
        
        /// <summary>
        /// 当前黑心资源 (玩家持有，通过击杀特定怪/污染区域获得)
        /// </summary>
        public int DarkHeart;
        
        /// <summary>
        /// 当前公司良心值 (生存血条，归零BadEnd)
        /// </summary>
        public int CompanyConscience;
        
        /// <summary>
        /// 当前公司黑心值/污染度 (异常状态，满值可能导致严重后果)
        /// </summary>
        public int CompanyCorruption;
    }

    /// <summary>
    /// 运行时数据访问入口
    /// </summary>
    public RuntimeData Data { get; private set; } = new RuntimeData();

    // ═══════════ 运行时数据 (部门) ═══════════
    /// <summary>
    /// 部门状态缓存 (DepartmentType -> Info)
    /// </summary>
    private Dictionary<DepartmentType, DepartmentInfo> _departments = new Dictionary<DepartmentType, DepartmentInfo>();

    /// <summary>
    /// 部门运行时信息
    /// </summary>
    public class DepartmentInfo
    {
        /// <summary>该部门当前在场员工总数</summary>
        public int EmployeeCount; 
        
        /// <summary>该部门当前在场员工的总等级之和</summary>
        public int TotalLevel;    
        
        /// <summary>
        /// 部门大招强度倍率
        /// 公式: 1.0 + (人数 * 10%) + (总等级 * 5%)
        /// </summary>
        public float PowerMultiplier => 1.0f + (EmployeeCount * 0.1f) + (TotalLevel * 0.05f);
    }

    // ═══════════ 框架生命周期 ═══════════
    public int InitOrder => 80;
    public int UpdateOrder => 0;
    public int DisposeOrder => 80;

    public void Initialize()
    {
        CY.Log("[DepartmentManager] Initialize");
        
        // 1. 初始化资源 (从 GlobalConfig 读取)
        Data.Gold = GetGlobalConfigInt("InitGold", 1000);
        Data.CompanyConscience = GetGlobalConfigInt("InitCompanyConscience", 100);
        Data.ConscienceResource = GetGlobalConfigInt("InitConscienceResource", 0);
        Data.DarkHeart = GetGlobalConfigInt("InitDarkHeart", 0);
        
        _maxConscience = GetGlobalConfigInt("MaxConscience", 100);
        _maxCompanyCorruption = GetGlobalConfigInt("MaxCompanyCorruption", 100);
        
        Data.CompanyCorruption = 0; // 公司黑心初始默认为0

        // 2. 初始化部门
        _departments.Clear();
        foreach (DepartmentType type in Enum.GetValues(typeof(DepartmentType)))
        {
            if (type != DepartmentType.None)
            {
                _departments[type] = new DepartmentInfo();
            }
        }
    }

    public void OnUpdate(float deltaTime)
    {
    }

    public void Dispose()
    {
        CY.Log("[DepartmentManager] Dispose");
        _departments.Clear();
    }

    // ═══════════ Unity 桥接 ═══════════
    private void Awake()
    {
        if (!ServiceLocator.IsRegistered<DepartmentManager>())
        {
            ServiceLocator.RegisterInstance(this);
        }
        else
        {
            Destroy(gameObject);
            Initialize(); // 重载场景时可能需要重新初始化
        }
    }

    private void OnDestroy()
    {
        Dispose();
        if (ServiceLocator.IsRegistered<DepartmentManager>())
        {
            ServiceLocator.Unregister<DepartmentManager>();
        }
    }

    // ═══════════ 资源操作接口 ═══════════

    // --- 资金 Gold ---
    public bool HasGold(int amount) => Data.Gold >= amount;
    
    public bool TrySpendGold(int amount)
    {
        if (Data.Gold >= amount)
        {
            ChangeGold(-amount);
            return true;
        }
        return false;
    }

    public void ChangeGold(int fullAmount)
    {
        // 说明：
        // - 资源变化属于低频事件，但 UI 若在 OnUpdate 每帧轮询会产生不必要的字符串分配与 UI 重建开销。
        // - 这里在“数值真正变化”时派发结构体事件，BattleUI 等监听者即可事件驱动刷新（零 GC）。
        int oldValue = Data.Gold;
        int newValue = Mathf.Max(0, oldValue + fullAmount);
        if (newValue == oldValue)
        {
            return;
        }

        Data.Gold = newValue;
        CY.Log($"[Resource] Gold Changed: {Data.Gold} ({fullAmount})");

        PostDepartmentResourceChangedEvent();
    }

    // --- 良心资源 ConscienceResource ---
    public void ChangeConscienceResource(int amount)
    {
        int oldValue = Data.ConscienceResource;
        int newValue = Mathf.Max(0, oldValue + amount);
        if (newValue == oldValue)
        {
            return;
        }

        Data.ConscienceResource = newValue;
        CY.Log($"[Resource] ConscienceResource: {Data.ConscienceResource}");

        PostDepartmentResourceChangedEvent();
    }

    // --- 黑心资源 DarkHeart ---
    public void ChangeDarkHeart(int amount)
    {
        int oldValue = Data.DarkHeart;
        int newValue = Mathf.Max(0, oldValue + amount);
        if (newValue == oldValue)
        {
            return;
        }

        Data.DarkHeart = newValue;
        CY.Log($"[Resource] DarkHeart: {Data.DarkHeart}");

        PostDepartmentResourceChangedEvent();
    }

    // --- 公司良心状态 CompanyConscience ---
    public void ChangeCompanyConscience(int amount)
    {
        // 良心无下限（负数导致失败），上限为 MaxConscience
        Data.CompanyConscience = Mathf.Min(Data.CompanyConscience + amount, MaxConscience);

        CY.Log($"[Resource] CompanyConscience: {Data.CompanyConscience}");

        if (Data.CompanyConscience < 0)
        {
            CY.LogError("[Game Over] 良心值已耗尽！公司破产！");
            // TODO: 调用游戏结束接口
        }

        PostDepartmentResourceChangedEvent();
    }

    // --- 公司黑心状态 CompanyCorruption ---
    public void ChangeCompanyCorruption(int amount)
    {
        Data.CompanyCorruption = Mathf.Clamp(Data.CompanyCorruption + amount, 0, MaxCompanyCorruption);
        CY.Log($"[Resource] CompanyCorruption: {Data.CompanyCorruption}");

        PostDepartmentResourceChangedEvent();
    }

    /// <summary>
    /// 派发“部门资源变化”事件（战斗 HUD 等模块监听后按需刷新）。
    /// 注意：事件必须为 struct，且发布必须使用 CY.Event.Post(ref evt)（零 GC）。
    /// </summary>
    private void PostDepartmentResourceChangedEvent()
    {
        DepartmentResourceChangedEvent evt = new DepartmentResourceChangedEvent
        {
            Gold = Data.Gold,
            ConscienceResource = Data.ConscienceResource,
            DarkHeart = Data.DarkHeart,
            CompanyConscience = Data.CompanyConscience,
            CompanyCorruption = Data.CompanyCorruption,
        };
        CY.Event.Post(ref evt);
    }

    // ═══════════ 部门管理接口 ═══════════

    /// <summary>
    /// 当有员工入职/升级/离职时调用此方法刷新部门数据
    /// </summary>
    public void UpdateDepartmentStats(DepartmentType type, int employeeCount, int totalLevel)
    {
        if (_departments.TryGetValue(type, out var info))
        {
            info.EmployeeCount = employeeCount;
            info.TotalLevel = totalLevel;
            CY.Log($"[Department] {type} 更新: 人数{employeeCount}, 总等级{totalLevel}, 强度倍率{info.PowerMultiplier:F2}");
        }
    }

    /// <summary>
    /// 获取部门当前的大招强度倍率
    /// </summary>
    public float GetDepartmentPower(DepartmentType type)
    {
        if (_departments.TryGetValue(type, out var info))
        {
            return info.PowerMultiplier;
        }
        return 1.0f;
    }

    /// <summary>
    /// 获取全局配置 Int 值
    /// </summary>
    private int GetGlobalConfigInt(string key, int defaultValue)
    {
        if (CY.Data.HasDataTable("GlobalConfig"))
        {
            var table = CY.Data.GetDataTable<GlobalConfigRow>("GlobalConfig");
            // 使用 Predicate 查询
            var row = table.GetRow(r => r.Key == key);
            if (row != null)
            {
                return row.ValueInt;
            }
        }
        return defaultValue;
    }
}
