// 引用数据表接口命名空间，使用 IDataRow
using CYFramework.Core.DataTable; // 数据表接口引用

/// <summary>
/// 波次计划数据表行（对应 WavePlan.csv）。
/// </summary>
public sealed class WavePlanRow : IDataRow // 波次计划数据行定义
{
    /// <summary>波次 Id。</summary>
    public int WaveId; // 波次 Id
    /// <summary>波次名称（用于 UI 展示）。</summary>
    public string Name; // 波次名称
    /// <summary>波次结束类型（数字枚举）。</summary>
    public int EndType; // 结束类型
    /// <summary>波次结束阈值（时间/数量）。</summary>
    public float EndValue; // 结束阈值
    /// <summary>波次结束标识（区域/事件 Id）。</summary>
    public string EndId; // 结束标识
    /// <summary>是否自动推进下一波（0/1）。</summary>
    public int AutoAdvance; // 自动推进标记
    /// <summary>下一波波次 Id（0 表示不推进）。</summary>
    public int NextWaveId; // 下一波 Id
    /// <summary>轨道 Id 列表（格式：TrackId|TrackId）。</summary>
    public string TrackIds; // 轨道 Id 列表
    /// <summary>解锁波次阈值（已完成波数达到后进入随机池）。</summary>
    public int UnlockAfterWave; // 解锁波次阈值
    /// <summary>过期波次阈值（已完成波数超过后移出随机池，0 表示永不过期）。</summary>
    public int ExpireAfterWave; // 过期波次阈值
    /// <summary>随机权重（>0 才进入随机池）。</summary>
    public int RandomWeight; // 随机权重
    /// <summary>显示波次编号（用于 UI）。</summary>
    public int DisplayIndex; // 显示波次编号

    int IDataRow.Id => WaveId; // IDataRow 主键映射

    /// <summary>
    /// CSV 解析（顺序需与 WavePlan.csv 表头一致）。
    /// </summary>
    /// <param name="values">字段字符串数组。</param>
    public void ParseRow(string[] values) // 解析行入口
    {
        WaveId = int.Parse(values[0]); // 解析波次 Id
        Name = values[1]; // 解析波次名称
        EndType = int.Parse(values[2]); // 解析结束类型
        EndValue = float.Parse(values[3], System.Globalization.CultureInfo.InvariantCulture); // 解析结束阈值
        EndId = values[4]; // 解析结束标识
        AutoAdvance = int.Parse(values[5]); // 解析自动推进标记
        NextWaveId = int.Parse(values[6]); // 解析下一波 Id
        TrackIds = values.Length > 7 ? values[7] : string.Empty; // 解析轨道 Id 列表
        UnlockAfterWave = values.Length > 8 ? int.Parse(values[8]) : 0; // 解析解锁波次阈值
        ExpireAfterWave = values.Length > 9 ? int.Parse(values[9]) : 0; // 解析过期波次阈值
        RandomWeight = values.Length > 10 ? int.Parse(values[10]) : 0; // 解析随机权重
        DisplayIndex = values.Length > 11 ? int.Parse(values[11]) : 0; // 解析显示波次编号
    }
}
