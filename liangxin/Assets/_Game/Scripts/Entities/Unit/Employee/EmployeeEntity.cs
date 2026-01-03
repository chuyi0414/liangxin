using CYFramework;
using UnityEngine;

/// <summary>
/// 员工单位实体（继承通用 UnitEntity）。
/// 目前暂无额外逻辑，行为由外部系统驱动。
/// </summary>
[RequireComponent(typeof(HybridNavigationAgent))]
public sealed class EmployeeEntity : UnitEntity
{
}
