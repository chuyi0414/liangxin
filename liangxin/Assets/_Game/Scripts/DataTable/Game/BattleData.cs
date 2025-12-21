using CYFramework.Core.DataTable;

/// <summary>
/// 战斗初始配置（数据表行）。
/// 用于 JSON 单对象加载，Id 可由加载器自动补为 1。
/// </summary>
public sealed class BattleData : IDataRow
{
    /// <summary>唯一 Id（DataTable 主键）</summary>
    public int Id;
    /// <summary>资金</summary>
    public int Money;
    /// <summary>良心</summary>
    public int Conscience;
    /// <summary>黑心</summary>
    public int BlackHeart;
    /// <summary>公司良心</summary>
    public int CompanyConscience;
    /// <summary>公司污染度</summary>
    public int CompanyPollution;

    int IDataRow.Id => Id;

    /// <summary>
    /// CSV 解析（若仅使用 JSON，可保持空实现）。
    /// </summary>
    public void ParseRow(string[] values)
    {
        
    }
}
