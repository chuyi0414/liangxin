// 引用数值解析命名空间，使用 CultureInfo
using System.Globalization; // 数值解析引用
// 引用数据表接口命名空间，使用 IDataRow
using CYFramework.Core.DataTable; // 数据表接口引用

/// <summary>
/// 波次刷怪组数据表行（对应 WaveSpawnGroup.csv）。
/// </summary>
public sealed class WaveSpawnGroupRow : IDataRow // 刷怪组数据行定义
{
    /// <summary>刷怪组 Id。</summary>
    public int GroupId; // 刷怪组 Id
    /// <summary>敌人列表与权重（格式：EnemyId:Weight|EnemyId:Weight）。</summary>
    public string EnemyList; // 敌人列表文本
    /// <summary>单次刷新最小数量。</summary>
    public int SpawnCountMin; // 最小数量
    /// <summary>单次刷新最大数量。</summary>
    public int SpawnCountMax; // 最大数量
    /// <summary>刷新间隔最小值（秒）。</summary>
    public float IntervalMin; // 间隔最小值
    /// <summary>刷新间隔最大值（秒）。</summary>
    public float IntervalMax; // 间隔最大值
    /// <summary>刷新点模式（0=PointId，1=AreaId）。</summary>
    public int PointMode; // 刷新点模式
    /// <summary>刷新点 Id 列表（格式：A|B|C）。</summary>
    public string PointId; // 刷新点 Id
    /// <summary>区域 Id（PointMode=AreaId 时使用）。</summary>
    public string AreaId; // 区域 Id
    /// <summary>刷怪阵型（数字枚举）。</summary>
    public int Formation; // 阵型类型
    /// <summary>阵型参数 1。</summary>
    public float Param1; // 参数 1
    /// <summary>阵型参数 2。</summary>
    public float Param2; // 参数 2
    /// <summary>阵型参数 3。</summary>
    public float Param3; // 参数 3
    /// <summary>阵型参数 4。</summary>
    public float Param4; // 参数 4
    /// <summary>阵型朝向角度（世界角度）。</summary>
    public float DirectionAngle; // 朝向角度
    /// <summary>分布方式（0=Random，1=Uniform）。</summary>
    public int Distribution; // 分布方式

    int IDataRow.Id => GroupId; // IDataRow 主键映射

    /// <summary>
    /// CSV 解析（顺序需与 WaveSpawnGroup.csv 表头一致）。
    /// </summary>
    /// <param name="values">字段字符串数组。</param>
    public void ParseRow(string[] values) // 解析行入口
    {
        GroupId = int.Parse(values[0]); // 解析刷怪组 Id
        EnemyList = values[1]; // 解析敌人列表文本
        SpawnCountMin = int.Parse(values[2]); // 解析最小数量
        SpawnCountMax = int.Parse(values[3]); // 解析最大数量
        IntervalMin = float.Parse(values[4], CultureInfo.InvariantCulture); // 解析最小间隔
        IntervalMax = float.Parse(values[5], CultureInfo.InvariantCulture); // 解析最大间隔
        PointMode = int.Parse(values[6]); // 解析刷新点模式
        PointId = values[7]; // 解析刷新点 Id
        AreaId = values[8]; // 解析区域 Id
        Formation = int.Parse(values[9]); // 解析阵型类型
        Param1 = float.Parse(values[10], CultureInfo.InvariantCulture); // 解析参数 1
        Param2 = float.Parse(values[11], CultureInfo.InvariantCulture); // 解析参数 2
        Param3 = float.Parse(values[12], CultureInfo.InvariantCulture); // 解析参数 3
        Param4 = float.Parse(values[13], CultureInfo.InvariantCulture); // 解析参数 4
        DirectionAngle = float.Parse(values[14], CultureInfo.InvariantCulture); // 解析朝向角度
        Distribution = int.Parse(values[15]); // 解析分布方式
    }
}
