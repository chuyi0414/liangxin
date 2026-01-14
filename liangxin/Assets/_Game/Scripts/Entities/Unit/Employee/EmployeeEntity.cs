// 引用 CYFramework 命名空间，使用 CY 日志
using CYFramework; // 框架入口引用
using CYFramework.Core.Entity;

// 引用 UnityEngine 命名空间，使用 RequireComponent 等特性
using UnityEngine; // Unity 基础类型引用

/// <summary>
/// 通用员工实体：复用 EmployeeEntityBase 的通用实现。
/// 说明：该员工类型是否远程由数据表 Employee.csv 的 IsRanged 决定。
/// </summary>
[RequireComponent(typeof(HybridNavigationAgent))] // 约束必须挂载导航组件（项目单位统一要求）
[EntityPrefab("Prefabs/Entities/Unit/Employee/EmployeeEntity", "EmployeeEntity", "Employees")] // 绑定实体预制体信息
public sealed class EmployeeEntity : EmployeeEntityBase // 通用员工实体定义
{
    /// <summary>
    /// 输出“员工数据行缺失”的警告日志。
    /// </summary>
    protected override void LogMissingEmployeeDataRow() // 缺少数据行日志输出入口
    {
        CY.LogWarning("[EmployeeEntity] 缺少员工数据行，使用默认属性。"); // 输出警告日志
    }
}

