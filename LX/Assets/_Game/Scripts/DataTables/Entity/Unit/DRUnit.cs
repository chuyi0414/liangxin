using UnityGameFramework.Runtime;

/// <summary>
/// 单位公共数据表行：抽取所有单位共有的字段。
/// </summary>
public class DRUnit : DataRowBase
{
    /// <summary>
    /// 主键 Id 的内部存储，对应数据表的 Id 列。
    /// </summary>
    protected int m_Id;
    /// <summary>
    /// 数据行唯一 Id。
    /// </summary>
    public override int Id => m_Id;
    /// <summary>
    /// 单位代码，用于逻辑侧索引或配置引用。
    /// </summary>
    public string Code { get;  set; }
    /// <summary>
    /// 单位显示名称，允许包含空格。
    /// </summary>
    public string Name { get;  set; }
    /// <summary>
    /// 阵营编号，用于敌我关系或阵营判定。
    /// </summary>
    public CAMP Camp { get;  set; }
    /// <summary>
    /// 单位预制体资源路径，用于加载实体。
    /// </summary>
    public string PrefabPath { get;  set; }
    /// <summary>
    /// 移动速度（配置字段 MoveSeep）。
    /// </summary>
    public float MoveSeep { get;  set; }
    /// <summary>
    /// 血量（单位基础数值）。
    /// </summary>
    public float HP { get;  set; }
    /// <summary>
    /// 攻击力（单位基础数值）。
    /// </summary>
    public float Attack { get;  set; }
    /// <summary>
    /// 攻击间隔（数值越小攻击越快）。
    /// </summary>
    public float AttackSpeed { get;  set; }
    /// <summary>
    /// 攻击范围（用于判定可攻击距离）。
    /// </summary>
    public float AttackRange { get;  set; }
    /// <summary>
    /// 视野范围（用于判定可发现距离）。
    /// </summary>
    public float VisualScope { get;  set; }
    /// <summary>
    /// 攻击类型（）
    /// </summary>
    public ATTACKTYPE AttackType { get;  set; }
    /// <summary>
    /// 子弹数据表 Id，用于关联子弹配置。
    /// </summary>
    public int ProjectileId { get;  set; }
    /// <summary>
    /// 子弹飞行速度。
    /// </summary>
    public float ProjectileSpeed { get;  set; }
}
