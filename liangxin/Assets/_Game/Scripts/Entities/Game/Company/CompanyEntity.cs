// 引用 CYFramework 命名空间，使用框架统一入口
using CYFramework; // CYFramework 入口引用
// 引用实体系统命名空间，使用 EntityBase 等类型
using CYFramework.Core.Entity; // 实体系统类型引用
// 引用 UnityEngine，使用 MonoBehaviour/Transform 等类型
using UnityEngine; // Unity 引擎基础类型引用

/// <summary>
/// 公司实体：提供公司位置与追击距离配置。
/// </summary>
[EntityPrefab("Prefabs/Entities/Game/CompanyEntity", "CompanyEntity", "Scene")] // 绑定实体预制体信息
public class CompanyEntity : EntityBase // 公司实体定义
{
    /// <summary>当前场景中的公司实体（方便敌人获取位置）。</summary>
    public static CompanyEntity Current { get; private set; } // 当前公司实体静态引用

    /// <summary>公司强制追击距离（<=该距离时敌人强制追公司）。</summary>
    [SerializeField] private float _forceChaseDistance = 2f; // 公司强制追击距离

    /// <summary>公司强制追击距离（只读）。</summary>
    public float ForceChaseDistance => _forceChaseDistance; // 对外只读访问

    /// <summary>
    /// 实体显示：注册当前公司实体引用。
    /// </summary>
    /// <param name="userData">显示时传入的数据。</param>
    protected override void OnEntityShow(object userData) // 实体显示入口
    {
        base.OnEntityShow(userData); // 调用父类显示
        Current = this; // 写入当前公司实体
    }

    /// <summary>
    /// 实体回收：清理当前公司实体引用。
    /// </summary>
    protected override void OnEntityRecycle() // 实体回收入口
    {
        if (Current == this)
        {
            Current = null; // 清理静态引用
        }

        base.OnEntityRecycle(); // 调用父类回收
    }
}
