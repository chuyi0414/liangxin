using System.Globalization;
using CYFramework.Core.DataTable;

/// <summary>
/// 敌人数据表行（对应 Enemy.csv）。
/// </summary>
public sealed class EnemyUnitRow : IDataRow
{
    /// <summary>唯一 Id（DataTable 主键）。</summary>
    public int Id;
    /// <summary>单位编码。</summary>
    public string Code;
    /// <summary>单位名称。</summary>
    public string Name;
    /// <summary>单位阵营。</summary>
    public UnitCamp Camp;
    /// <summary>单位状态。</summary>
    public UnitLifeState LifeState;
    /// <summary>单位等级。</summary>
    public int Level;
    /// <summary>最大生命值。</summary>
    public int MaxHp;
    /// <summary>攻击力。</summary>
    public int Attack;
    /// <summary>防御力。</summary>
    public int Defense;
    /// <summary>固定防御穿透值。</summary>
    public int DefensePenetration;
    /// <summary>百分比防御穿透（0-1）。</summary>
    public float DefensePenetrationRate;
    /// <summary>暴击率（0-1）。</summary>
    public float CritRate;
    /// <summary>闪避率（0-1）。</summary>
    public float DodgeRate;
    /// <summary>是否远程单位。</summary>
    public bool IsRanged;
    /// <summary>移动速度。</summary>
    public float MoveSpeed;
    /// <summary>攻击距离。</summary>
    public float AttackRange;
    /// <summary>可视范围。</summary>
    public float SightRange;
    /// <summary>攻击间隔（秒）。</summary>
    public float AttackInterval;
    /// <summary>攻击停顿时长（秒）。</summary>
    public float AttackStopDuration;

    int IDataRow.Id => Id;

    /// <summary>
    /// CSV 解析（顺序需与 Enemy.csv 表头一致）。
    /// </summary>
    public void ParseRow(string[] values)
    {
        Id = int.Parse(values[0]);
        Code = values[1];
        Name = values[2];
        Camp = (UnitCamp)int.Parse(values[3]);
        LifeState = (UnitLifeState)int.Parse(values[4]);
        Level = int.Parse(values[5]);
        MaxHp = int.Parse(values[6]);
        Attack = int.Parse(values[7]);
        Defense = int.Parse(values[8]);
        DefensePenetration = int.Parse(values[9]);
        DefensePenetrationRate = float.Parse(values[10], CultureInfo.InvariantCulture);
        CritRate = float.Parse(values[11], CultureInfo.InvariantCulture);
        DodgeRate = float.Parse(values[12], CultureInfo.InvariantCulture);
        IsRanged = bool.Parse(values[13]);
        MoveSpeed = float.Parse(values[14], CultureInfo.InvariantCulture);
        AttackRange = float.Parse(values[15], CultureInfo.InvariantCulture);
        SightRange = float.Parse(values[16], CultureInfo.InvariantCulture);
        AttackInterval = float.Parse(values[17], CultureInfo.InvariantCulture);
        AttackStopDuration = float.Parse(values[18], CultureInfo.InvariantCulture);
    }
}
