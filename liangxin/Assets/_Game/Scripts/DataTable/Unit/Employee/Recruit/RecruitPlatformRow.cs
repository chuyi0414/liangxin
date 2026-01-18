// 引用 CYFramework 数据表接口
using CYFramework.Core.DataTable; // 数据表接口引用

/// <summary>
/// 招聘平台数据表行（对应 RecruitPlatform.csv）。
/// </summary>
public sealed class RecruitPlatformRow : IDataRow // 招聘平台数据表行定义
{
    /// <summary>唯一 Id（DataTable 主键）。</summary>
    public int Id; // 平台 Id
    /// <summary>平台名称（谐音）。</summary>
    public string Name; // 平台名称

    int IDataRow.Id => Id; // 数据表主键映射

    /// <summary>
    /// CSV 解析（顺序需与 RecruitPlatform.csv 表头一致）。
    /// </summary>
    public void ParseRow(string[] values) // CSV 解析入口
    {
        Id = int.Parse(values[0]); // 解析 Id
        Name = values[1]; // 解析平台名称
    }
}
