// 引用 System.Collections.Generic 命名空间，使用 Dictionary
using System.Collections.Generic; // 字典容器引用
// 引用 UnityEngine 命名空间，使用 Collider2D
using UnityEngine; // Unity 基础类型引用

/// <summary>
/// 员工点击注册表：把员工的 Collider2D 映射到可控接口，用于鼠标点击快速定位员工。
/// 目的：支持“多个不同员工脚本（外卖员/保安等）”在同一套选中逻辑下工作。
/// </summary>
public static class EmployeeClickRegistry // 员工点击注册表
{
    /// <summary>
    /// 碰撞体到员工可控接口的映射表（静态缓存，避免每次点击遍历场景对象）。
    /// </summary>
    private static readonly Dictionary<Collider2D, IEmployeeControllable> ColliderToEmployee = // 碰撞体到员工映射表
        new Dictionary<Collider2D, IEmployeeControllable>(128); // 预分配容量减少扩容

    /// <summary>
    /// 注册员工：在员工 OnEntityShow 时调用。
    /// </summary>
    /// <param name="collider">员工主碰撞体。</param>
    /// <param name="employee">员工可控接口。</param>
    public static void Register(Collider2D collider, IEmployeeControllable employee) // 注册入口
    {
        if (collider == null) // 碰撞体为空判定
        {
            return; // 碰撞体为空时直接退出
        }

        if (employee == null) // 员工为空判定
        {
            return; // 员工为空时直接退出
        }

        ColliderToEmployee[collider] = employee; // 写入映射（覆盖旧值）
    }

    /// <summary>
    /// 反注册员工：在员工 OnEntityHide/OnEntityRecycle 时调用，避免池化复用后指向旧实例。
    /// </summary>
    /// <param name="collider">员工主碰撞体。</param>
    /// <param name="employee">员工可控接口（用于校验是否为同一实例）。</param>
    public static void Unregister(Collider2D collider, IEmployeeControllable employee) // 反注册入口
    {
        if (collider == null) // 碰撞体为空判定
        {
            return; // 碰撞体为空时直接退出
        }

        if (!ColliderToEmployee.TryGetValue(collider, out var current)) // 未注册判定
        {
            return; // 未注册时直接退出
        }

        if (current != employee) // 实例不一致判定
        {
            return; // 不是本实例时不移除（避免误删）
        }

        ColliderToEmployee.Remove(collider); // 从映射表移除
    }

    /// <summary>
    /// 通过碰撞体查询员工：在玩家点击逻辑中调用。
    /// </summary>
    /// <param name="collider">命中的碰撞体。</param>
    /// <param name="employee">输出员工接口。</param>
    /// <returns>是否找到有效员工。</returns>
    public static bool TryGetByCollider(Collider2D collider, out IEmployeeControllable employee) // 查询入口
    {
        employee = null; // 默认输出为空
        if (collider == null) // 碰撞体为空判定
        {
            return false; // 碰撞体为空时返回失败
        }

        if (!ColliderToEmployee.TryGetValue(collider, out employee)) // 字典查询失败判定
        {
            employee = null; // 查询失败时兜底清空输出
            return false; // 返回失败
        }

        if (employee == null) // 员工为空判定
        {
            return false; // 员工为空时返回失败
        }

        if (employee.Unit == null) // 单位实体为空判定
        {
            return false; // 单位为空时返回失败
        }

        return true; // 返回找到成功
    }
}

