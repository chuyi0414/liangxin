// 引用 CYFramework 数据表接口
using CYFramework.Core.DataTable; // 数据表接口引用

/// <summary>
/// 单位风格数据表行（对应 UnitStyle.csv）。
/// </summary>
public sealed class UnitStyleRow : IDataRow // 单位风格数据表行定义
{
    /// <summary>唯一 Id（DataTable 主键）。</summary>
    public int Id; // 风格 Id
    /// <summary>风格名称。</summary>
    public string Name; // 风格名称

    int IDataRow.Id => Id; // 数据表主键映射

    /// <summary>
    /// CSV 解析（顺序需与 UnitStyle.csv 表头一致）。
    /// </summary>
    public void ParseRow(string[] values) // CSV 解析入口
    {
        Id = int.Parse(values[0]); // 解析 Id
        Name = values[1]; // 解析风格名称
    }
}