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
    /// <summary>污染伤害最小值。</summary>
    public float PollutionDamageMin;
    /// <summary>污染伤害最大值。</summary>
    public float PollutionDamageMax;
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
        Id = int.Parse(values[0]); // 解析 Id
        Code = values[1]; // 解析单位编码
        Name = values[2]; // 解析单位名称
        Camp = (UnitCamp)int.Parse(values[3]); // 解析阵营
        LifeState = (UnitLifeState)int.Parse(values[4]); // 解析生命状态
        Level = int.Parse(values[5]); // 解析等级
        MaxHp = int.Parse(values[6]); // 解析最大生命值
        Attack = int.Parse(values[7]); // 解析攻击力
        PollutionDamageMin = float.Parse(values[8], CultureInfo.InvariantCulture); // 解析污染伤害最小值
        PollutionDamageMax = float.Parse(values[9], CultureInfo.InvariantCulture); // 解析污染伤害最大值
        Defense = int.Parse(values[10]); // 解析防御力
        DefensePenetration = int.Parse(values[11]); // 解析固定防御穿透
        DefensePenetrationRate = float.Parse(values[12], CultureInfo.InvariantCulture); // 解析百分比防御穿透
        CritRate = float.Parse(values[13], CultureInfo.InvariantCulture); // 解析暴击率
        DodgeRate = float.Parse(values[14], CultureInfo.InvariantCulture); // 解析闪避率
        IsRanged = bool.Parse(values[15]); // 解析远程标记
        MoveSpeed = float.Parse(values[16], CultureInfo.InvariantCulture); // 解析移动速度
        AttackRange = float.Parse(values[17], CultureInfo.InvariantCulture); // 解析攻击范围
        SightRange = float.Parse(values[18], CultureInfo.InvariantCulture); // 解析可视范围
        AttackInterval = float.Parse(values[19], CultureInfo.InvariantCulture); // 解析攻击间隔
        AttackStopDuration = float.Parse(values[20], CultureInfo.InvariantCulture); // 解析攻击停顿时长
    }
}
