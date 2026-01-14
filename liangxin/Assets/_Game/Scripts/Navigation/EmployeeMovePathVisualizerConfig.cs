// 引用 UnityEngine 命名空间，使用 ScriptableObject/Color/Material 等类型
using UnityEngine; // Unity 引擎基础类型引用

/// <summary>
/// 员工移动路径可视化全局配置：用于给“自动添加的 EmployeeMovePathVisualizer”提供统一默认值。
/// 说明：当某个员工手动挂载 EmployeeMovePathVisualizer 组件时，该员工默认使用自身 Inspector 配置，不读取本全局配置。
/// </summary>
[CreateAssetMenu(fileName = "EmployeeMovePathVisualizerConfig", menuName = "_Game/Navigation/Employee Move Path Visualizer Config", order = 0)] // 允许在 Unity 菜单中创建配置资源
public sealed class EmployeeMovePathVisualizerConfig : ScriptableObject // 员工移动路径可视化全局配置资源
{
    /// <summary>
    /// 路径刷新间隔（秒）：用于在移动过程中同步导航重算后的路径变化。
    /// </summary>
    public float RefreshInterval = 0.1f; // 刷新间隔

    /// <summary>
    /// 路径线宽度（世界单位）。
    /// </summary>
    public float LineWidth = 0.05f; // 线宽

    /// <summary>
    /// 路径点显示大小（世界单位）：用于显示拐点/路径点。
    /// </summary>
    public float PointSize = 0.12f; // 点大小

    /// <summary>
    /// 路径可视化最多显示的点数量：用于限制绘制成本，避免极端路径导致点数过多。
    /// </summary>
    public int MaxVisiblePoints = 64; // 最大点数

    /// <summary>
    /// 绘制层 Z 偏移：用于避免与地面/角色精灵完全重叠导致闪烁。
    /// </summary>
    public float ZOffset = -0.1f; // Z 偏移

    /// <summary>
    /// 路径线颜色（含透明度）。
    /// </summary>
    public Color LineColor = new Color(0.2f, 0.9f, 1f, 0.85f); // 路径线颜色

    /// <summary>
    /// 路径点颜色（含透明度）。
    /// </summary>
    public Color PointColor = new Color(0.2f, 0.9f, 1f, 0.95f); // 路径点颜色

    /// <summary>
    /// 路径线排序值：用于控制 LineRenderer 的渲染顺序（2D 项目常用）。
    /// </summary>
    public int LineSortingOrder = 999; // 线排序值

    /// <summary>
    /// 路径点排序值：用于控制 SpriteRenderer 的渲染顺序（2D 项目常用）。
    /// </summary>
    public int PointSortingOrder = 1000; // 点排序值

    /// <summary>
    /// 路径线材质覆盖：用于自定义 LineRenderer 的材质（例如发光/渐变等）。
    /// </summary>
    public Material LineMaterialOverride; // 线材质覆盖

    /// <summary>
    /// 路径点材质覆盖：用于自定义 SpriteRenderer 的材质（例如发光/软边等）。
    /// </summary>
    public Material PointMaterialOverride; // 点材质覆盖
}

