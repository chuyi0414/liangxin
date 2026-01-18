// 引用 System.Globalization 命名空间，使用 CultureInfo
using System.Globalization; // CultureInfo 解析格式引用
// 引用 CYFramework 数据表接口
using CYFramework.Core.DataTable; // 数据表接口引用

/// <summary>
/// 招聘配置数据表行（对应 RecruitConfig.csv）。
/// </summary>
public sealed class RecruitConfigRow : IDataRow // 招聘配置数据表行定义
{
    /// <summary>唯一 Id（DataTable 主键）。</summary>
    public int Id; // 配置 Id
    /// <summary>急聘权重。</summary>
    public float UrgentWeight; // 急聘权重
    /// <summary>普通招聘权重。</summary>
    public float NormalWeight; // 普通招聘权重
    /// <summary>临时工权重。</summary>
    public float TempWeight; // 临时工权重
    /// <summary>普通招聘等待波数最小值。</summary>
    public int NormalWaveMin; // 普通等待最小波数
    /// <summary>普通招聘等待波数最大值。</summary>
    public int NormalWaveMax; // 普通等待最大波数
    /// <summary>临时工持续波数最小值。</summary>
    public int TempWaveMin; // 临时工持续最小波数
    /// <summary>临时工持续波数最大值。</summary>
    public int TempWaveMax; // 临时工持续最大波数

    int IDataRow.Id => Id; // 数据表主键映射

    /// <summary>
    /// CSV 解析（顺序需与 RecruitConfig.csv 表头一致）。
    /// </summary>
    public void ParseRow(string[] values) // CSV 解析入口
    {
        Id = int.Parse(values[0]); // 解析 Id
        UrgentWeight = float.Parse(values[1], CultureInfo.InvariantCulture); // 解析急聘权重
        NormalWeight = float.Parse(values[2], CultureInfo.InvariantCulture); // 解析普通权重
        TempWeight = float.Parse(values[3], CultureInfo.InvariantCulture); // 解析临时工权重
        NormalWaveMin = int.Parse(values[4]); // 解析普通最小波数
        NormalWaveMax = int.Parse(values[5]); // 解析普通最大波数
        TempWaveMin = int.Parse(values[6]); // 解析临时工最小波数
        TempWaveMax = int.Parse(values[7]); // 解析临时工最大波数
    }
}
