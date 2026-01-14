// 引用 CYFramework 命名空间，使用 CY 日志
using CYFramework; // 框架入口引用
using CYFramework.Core.Entity;

// 引用 UnityEngine 命名空间，使用 RequireComponent 等特性
using UnityEngine; // Unity 基础类型引用

/// <summary>
/// 保安员工实体：近战单位（不发射子弹），复用 EmployeeEntityBase 的通用实现。
/// </summary>
[RequireComponent(typeof(HybridNavigationAgent))] // 约束必须挂载导航组件（用于右键移动）
[EntityPrefab("Prefabs/Entities/Unit/Employee/SecurityEmployeeEntity", "SecurityEmployeeEntity", "Employees")] // 绑定默认实体预制体信息（兜底）
public sealed class SecurityEmployeeEntity : EmployeeEntityBase // 保安员工实体定义
{
    /// <summary>
    /// 保安强制为近战单位：覆盖数据表的 IsRanged 配置。
    /// </summary>
    protected override bool? ForceIsRanged => false; // 近战覆盖开关

    /// <summary>
    /// 输出“员工数据行缺失”的警告日志。
    /// </summary>
    protected override void LogMissingEmployeeDataRow() // 缺少数据行日志输出入口
    {
        CY.LogWarning("[SecurityEmployeeEntity] 缺少员工数据行，使用默认属性。"); // 输出警告日志
    }
}

