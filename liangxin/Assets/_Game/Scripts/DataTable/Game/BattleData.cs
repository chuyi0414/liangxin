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
    /// <summary>黑心自动转换为良心的时间间隔（秒，<=0 表示禁用自动转换）。</summary>
    public float BlackHeartConvertTime;
    /// <summary>黑心并发吸收槽位数量（<=0 时按 1 处理）。</summary>
    public int BlackHeartAbsorbCount;
    /// <summary>公司良心</summary>
    public int CompanyConscience;
    /// <summary>
    /// 良心损害累计值
    /// </summary>
    public int CompanyConscienceDamagePerPoint;
    /// <summary>公司污染度上限</summary>
    public int CompanyPollution;
    /// <summary>
    /// 污染损害累计值
    /// </summary>
    public int CompanyPollutionDamagePerPoint;
    /// <summary>
    /// 人才库默认显示数量（对应 UI 的 _goTalentPoolContent 子物体数量）。
    /// </summary>
    public int TalentPoolDisplayCount;
    /// <summary>
    /// 人才库刷新价格。
    /// </summary>
    public int TalentPoolRefreshPrice;

    int IDataRow.Id => Id;

    /// <summary>
    /// CSV 解析（若仅使用 JSON，可保持空实现）。
    /// </summary>
    public void ParseRow(string[] values)
    {
        
    }
}
