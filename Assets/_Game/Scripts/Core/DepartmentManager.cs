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
    [Header("初始资源")]
    /// <summary>
    /// 初始资金 (用于招募员工、购买道具)
    /// </summary>
    public int InitGold = 1000;
    
    /// <summary>
    /// 初始良心值 (用于安抚员工、特殊选项，过低会导致BadEnd)
    /// </summary>
    public int InitConscience = 50;
    
    /// <summary>
    /// 初始黑心值 (通过击杀特定怪/污染区域获得，可净化转换为良心值)
    /// </summary>
    public int InitCorruption = 0;
    
    /// <summary>
    /// 黑心值上限 (默认100，溢出无效)
    /// </summary>
    public int MaxCorruption = 100;

    // ═══════════ 运行时数据 (资源) ═══════════
    /// <summary>
    /// 当前资金
    /// </summary>
    public int Gold { get; private set; }
    
    /// <summary>
    /// 当前良心值
    /// </summary>
    public int Conscience { get; private set; }
    
    /// <summary>
    /// 当前黑心值
    /// </summary>
    public int Corruption { get; private set; }

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
        
        // 1. 初始化资源
        Gold = InitGold;
        Conscience = InitConscience;
        Corruption = InitCorruption;

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
            Initialize();
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
    public bool HasGold(int amount) => Gold >= amount;
    
    public bool TrySpendGold(int amount)
    {
        if (Gold >= amount)
        {
            ChangeGold(-amount);
            return true;
        }
        return false;
    }

    public void ChangeGold(int fullAmount)
    {
        Gold = Mathf.Max(0, Gold + fullAmount);
        // CY.Event.Post(...)
        CY.Log($"[Resource] Gold Changed: {Gold} ({fullAmount})");
    }

    // --- 良心 Conscience ---
    public void ChangeConscience(int amount)
    {
        Conscience = Mathf.Clamp(Conscience + amount, 0, 999);
        CY.Log($"[Resource] Conscience: {Conscience}");
    }

    // --- 黑心 Corruption ---
    public void ChangeCorruption(int amount)
    {
        Corruption = Mathf.Clamp(Corruption + amount, 0, MaxCorruption);
        CY.Log($"[Resource] Corruption: {Corruption}");
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
}
