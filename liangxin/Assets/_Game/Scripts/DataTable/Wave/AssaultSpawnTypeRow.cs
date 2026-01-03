// 引用数值解析命名空间，使用 CultureInfo
using System.Globalization; // 数值解析引用
// 引用数据表接口命名空间，使用 IDataRow
using CYFramework.Core.DataTable; // 数据表接口引用

/// <summary>
/// 奇袭生成类型数据表行（对应 AssaultSpawnType.csv）。
/// </summary>
public sealed class AssaultSpawnTypeRow : IDataRow // 奇袭生成类型数据行定义
{
    /// <summary>唯一 Id（DataTable 主键）。</summary>
    public int Id; // 行 Id
    /// <summary>所属奇袭波次 Id（触发时匹配）。</summary>
    public int WaveId; // 奇袭波次 Id
    /// <summary>生成类型权重（用于随机抽取）。</summary>
    public int Weight; // 权重
    /// <summary>准备阶段时长（秒）。</summary>
    public float PrepareDuration; // 准备阶段时长
    /// <summary>刷怪阶段时长（秒）。</summary>
    public float SpawnDuration; // 刷怪阶段时长
    /// <summary>单次刷新最小数量。</summary>
    public int SpawnCountMin; // 最小数量
    /// <summary>单次刷新最大数量。</summary>
    public int SpawnCountMax; // 最大数量
    /// <summary>刷新间隔最小值（秒）。</summary>
    public float IntervalMin; // 间隔最小值
    /// <summary>刷新间隔最大值（秒）。</summary>
    public float IntervalMax; // 间隔最大值
    /// <summary>刷新点 Id（对应 WaveSpawnPoint.PointId）。</summary>
    public string PointId; // 刷新点 Id
    /// <summary>敌人列表与权重（格式：EnemyId:Weight|EnemyId:Weight）。</summary>
    public string EnemyList; // 敌人列表文本

    int IDataRow.Id => Id; // IDataRow 主键映射

    /// <summary>
    /// CSV 解析（顺序需与 AssaultSpawnType.csv 表头一致）。
    /// </summary>
    /// <param name="values">字段字符串数组。</param>
    public void ParseRow(string[] values) // 解析行入口
    {
        Id = int.Parse(values[0]); // 解析 Id
        WaveId = int.Parse(values[1]); // 解析奇袭波次 Id
        Weight = int.Parse(values[2]); // 解析权重
        PrepareDuration = float.Parse(values[3], CultureInfo.InvariantCulture); // 解析准备时长
        SpawnDuration = float.Parse(values[4], CultureInfo.InvariantCulture); // 解析刷怪时长
        SpawnCountMin = int.Parse(values[5]); // 解析最小数量
        SpawnCountMax = int.Parse(values[6]); // 解析最大数量
        IntervalMin = float.Parse(values[7], CultureInfo.InvariantCulture); // 解析间隔最小值
        IntervalMax = float.Parse(values[8], CultureInfo.InvariantCulture); // 解析间隔最大值
        PointId = values[9]; // 解析刷新点 Id
        EnemyList = values[10]; // 解析敌人列表文本
    }
}
