// 引用数值解析命名空间，使用 CultureInfo
using System.Globalization; // 数值解析引用
// 引用数据表接口命名空间，使用 IDataRow
using CYFramework.Core.DataTable; // 数据表接口引用

/// <summary>
/// 波次轨道数据表行（对应 WaveTrack.csv）。
/// </summary>
public sealed class WaveTrackRow : IDataRow // 波次轨道数据行定义
{
    /// <summary>轨道 Id。</summary>
    public int TrackId; // 轨道 Id
    /// <summary>开始触发类型（数字枚举）。</summary>
    public int StartType; // 开始触发类型
    /// <summary>开始触发阈值（时间/数量）。</summary>
    public float StartValue; // 开始阈值
    /// <summary>开始触发标识（区域/事件 Id）。</summary>
    public string StartId; // 开始标识
    /// <summary>开始额外延迟（秒）。</summary>
    public float StartDelay; // 开始延迟
    /// <summary>结束触发类型（数字枚举）。</summary>
    public int EndType; // 结束触发类型
    /// <summary>结束触发阈值（时间/数量）。</summary>
    public float EndValue; // 结束阈值
    /// <summary>结束触发标识（区域/事件 Id）。</summary>
    public string EndId; // 结束标识
    /// <summary>刷怪组 Id。</summary>
    public int SpawnGroupId; // 刷怪组 Id
    /// <summary>同屏最大存活数（0 表示不限制）。</summary>
    public int MaxAlive; // 存活上限
    /// <summary>最大总刷怪数（0 表示不限制）。</summary>
    public int MaxTotalSpawn; // 总刷怪上限

    int IDataRow.Id => TrackId; // IDataRow 主键映射

    /// <summary>
    /// CSV 解析（顺序需与 WaveTrack.csv 表头一致）。
    /// </summary>
    /// <param name="values">字段字符串数组。</param>
    public void ParseRow(string[] values) // 解析行入口
    {
        TrackId = int.Parse(values[0]); // 解析轨道 Id
        StartType = int.Parse(values[1]); // 解析开始触发类型
        StartValue = float.Parse(values[2], CultureInfo.InvariantCulture); // 解析开始阈值
        StartId = values[3]; // 解析开始标识
        StartDelay = float.Parse(values[4], CultureInfo.InvariantCulture); // 解析开始延迟
        EndType = int.Parse(values[5]); // 解析结束触发类型
        EndValue = float.Parse(values[6], CultureInfo.InvariantCulture); // 解析结束阈值
        EndId = values[7]; // 解析结束标识
        SpawnGroupId = int.Parse(values[8]); // 解析刷怪组 Id
        MaxAlive = int.Parse(values[9]); // 解析存活上限
        MaxTotalSpawn = int.Parse(values[10]); // 解析总刷怪上限
    }
}
