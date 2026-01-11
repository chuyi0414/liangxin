// 引用 UnityEngine 命名空间，使用 Vector2
using UnityEngine; // Unity 基础类型引用

/// <summary>
/// 员工可控接口：用于“玩家点击选中 + 右键下发移动命令”的统一入口（不依赖具体员工脚本类型）。
/// </summary>
public interface IEmployeeControllable // 员工可控接口定义
{
    /// <summary>
    /// 获取对应的单位实体：用于读取 LifeState/Camp 等基础属性。
    /// </summary>
    UnitEntity Unit { get; } // 对应的单位实体只读访问

    /// <summary>
    /// 下发移动命令：由员工自身决定如何处理（例如目标点占用偏移、导航模式等）。
    /// </summary>
    /// <param name="destination">目标世界坐标（XY）。</param>
    /// <returns>是否命令成功。</returns>
    bool TryCommandMove(Vector2 destination); // 员工移动命令入口
}

