/// <summary>
/// 波次阶段类型。
/// </summary>
public enum WaveStage // 波次阶段枚举
{
    /// <summary>无波次。</summary>
    None = 0, // 无波次
    /// <summary>准备阶段。</summary>
    Prepare = 1, // 准备阶段
    /// <summary>刷怪阶段。</summary>
    Spawn = 2 // 刷怪阶段
}

/// <summary>
/// 波次触发类型（开始/结束条件）。
/// </summary>
public enum WaveTriggerType // 波次触发类型枚举
{
    /// <summary>时间触发（秒）。</summary>
    Time = 0, // 时间触发
    /// <summary>击杀数触发。</summary>
    KillCount = 1, // 击杀触发
    /// <summary>存活数触发。</summary>
    AliveCount = 2, // 存活触发
    /// <summary>区域触发。</summary>
    Area = 3, // 区域触发
    /// <summary>事件触发。</summary>
    Event = 4, // 事件触发
    /// <summary>刷新数量触发。</summary>
    SpawnedCount = 5, // 刷新数量触发
    /// <summary>全部轨道完成。</summary>
    AllTracksDone = 6 // 全轨道完成触发
}

/// <summary>
/// 刷新点模式。
/// </summary>
public enum WavePointMode // 刷新点模式枚举
{
    /// <summary>命名点。</summary>
    PointId = 0, // 命名点模式
    /// <summary>区域采样。</summary>
    AreaId = 1 // 区域模式
}

/// <summary>
/// 刷怪阵型类型。
/// </summary>
public enum WaveFormation // 刷怪阵型枚举
{
    /// <summary>单点。</summary>
    Point = 0, // 单点
    /// <summary>圆形。</summary>
    Circle = 1, // 圆形
    /// <summary>直线。</summary>
    Line = 2, // 直线
    /// <summary>扇形。</summary>
    Fan = 3, // 扇形
    /// <summary>矩形。</summary>
    Rect = 4 // 矩形
}

/// <summary>
/// 阵型分布方式。
/// </summary>
public enum WaveDistribution // 阵型分布枚举
{
    /// <summary>随机分布。</summary>
    Random = 0, // 随机分布
    /// <summary>均匀分布。</summary>
    Uniform = 1 // 均匀分布
}
