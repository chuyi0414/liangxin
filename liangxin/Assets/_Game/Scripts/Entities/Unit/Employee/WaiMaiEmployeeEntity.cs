// 引用 CYFramework 命名空间，使用 CY 日志
using CYFramework; // 框架入口引用
using CYFramework.Core.Entity;

// 引用 UnityEngine 命名空间，使用 RequireComponent 等特性
using UnityEngine; // Unity 基础类型引用

/// <summary>
/// 外卖员员工实体：远程攻击单位，复用 EmployeeEntityBase 的通用实现，并补充远程子弹配置。
/// </summary>
[RequireComponent(typeof(HybridNavigationAgent))] // 约束必须挂载导航组件（用于右键移动）
[EntityPrefab("Prefabs/Entities/Unit/Employee/WaiMaiEmployeeEntity", "WaiMaiEmployeeEntity", "Employees")] // 绑定默认实体预制体信息（兜底）
public sealed class WaiMaiEmployeeEntity : EmployeeEntityBase // 外卖员员工实体定义
{
    /// <summary>
    /// 外卖员子弹预制体路径（Resources 相对路径，无扩展名）。
    /// </summary>
    private const string WaiMaiBulletPrefabPath = "Prefabs/Entities/Projectiles/Unit/Player/WaiMaiBullet"; // 外卖员子弹预制体路径常量

    /// <summary>
    /// 外卖员子弹路径数组（供 UnitEntity 远程发射使用）。
    /// </summary>
    private static readonly string[] WaiMaiBulletPrefabPaths = { WaiMaiBulletPrefabPath }; // 外卖员子弹路径数组缓存（避免运行时分配）

    /// <summary>
    /// 外卖员强制为远程单位：覆盖数据表的 IsRanged 配置。
    /// </summary>
    protected override bool? ForceIsRanged => true; // 远程覆盖开关

    /// <summary>
    /// 输出“员工数据行缺失”的警告日志。
    /// </summary>
    protected override void LogMissingEmployeeDataRow() // 缺少数据行日志输出入口
    {
        CY.LogWarning("[WaiMaiEmployeeEntity] 缺少员工数据行，使用默认属性。"); // 输出警告日志
    }

    /// <summary>
    /// 当员工数据行缺失时：仍然需要给外卖员配置远程子弹，避免远程攻击逻辑缺少子弹配置。
    /// </summary>
    protected override void OnEmployeeDataMissing() // 数据缺失回调入口
    {
        ApplyWaiMaiBulletConfig(); // 兜底应用外卖员子弹配置
    }

    /// <summary>
    /// 当员工数据行已成功应用后：为外卖员配置远程子弹数组。
    /// </summary>
    /// <param name="row">已应用的数据行。</param>
    protected override void OnAfterEmployeeDataApplied(EmployeeUnitRow row) // 数据应用后回调入口
    {
        ApplyWaiMaiBulletConfig(); // 应用外卖员子弹配置
    }

    /// <summary>
    /// 应用外卖员子弹配置：包含子弹速度与子弹数组。
    /// </summary>
    private void ApplyWaiMaiBulletConfig() // 外卖员子弹配置应用入口
    {
        ApplyBulletSpeed(0f); // 子弹速度为 0 表示使用子弹预制体默认速度
        ApplyBulletArrayConfig(BulletSelectRule.Random, WaiMaiBulletPrefabPaths); // 配置外卖员子弹数组（单发固定子弹）
    }
}

