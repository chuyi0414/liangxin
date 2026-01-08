/// <summary>
/// 公司良心变化事件：用于刷新良心显示。
/// </summary>
public struct CompanyConscienceChangedEvent // 公司良心变化事件结构体
{
    /// <summary>当前良心值。</summary>
    public int CurrentValue; // 当前良心
    /// <summary>良心最大值。</summary>
    public int MaxValue; // 良心上限
    /// <summary>本次变化量。</summary>
    public int Delta; // 变化值
}

/// <summary>
/// 公司污染变化事件：用于刷新污染显示。
/// </summary>
public struct CompanyPollutionChangedEvent // 公司污染变化事件结构体
{
    /// <summary>当前污染进度。</summary>
    public int CurrentValue; // 当前污染
    /// <summary>污染触发阈值。</summary>
    public int ThresholdValue; // 污染阈值
    /// <summary>本次变化量。</summary>
    public int Delta; // 变化值
}

/// <summary>
/// 公司污染触发事件：污染达到阈值时派发。
/// </summary>
public struct CompanyPollutionReachedEvent // 公司污染触发事件结构体
{
    /// <summary>触发次数。</summary>
    public int TriggerCount; // 触发次数
    /// <summary>污染触发阈值。</summary>
    public int ThresholdValue; // 污染阈值
}

/// <summary>
/// 资金变化事件：用于刷新资金显示。
/// </summary>
public struct MoneyChangedEvent // 资金变化事件结构体
{
    /// <summary>当前资金。</summary>
    public int CurrentValue; // 当前资金
    /// <summary>本次变化量。</summary>
    public int Delta; // 变化量
}

/// <summary>
/// 黑心变化事件：用于刷新黑心显示。
/// </summary>
public struct BlackHeartChangedEvent // 黑心变化事件结构体
{
    /// <summary>当前黑心。</summary>
    public int CurrentValue; // 当前黑心
    /// <summary>本次变化量。</summary>
    public int Delta; // 变化量
}
