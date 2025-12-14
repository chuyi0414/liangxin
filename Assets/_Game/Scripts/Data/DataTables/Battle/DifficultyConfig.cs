/// <summary>
/// 全局难度配置 (单例配置类)
/// 硬编码或从 GlobalConfig.csv 读取
/// </summary>
public static class DifficultyConfig
{
    // ═══════════ 基础节奏 ═══════════
    public const float BaseDuration = 60f;           // 基础波次时长 (秒)
    
    /// <summary>
    /// 首波准备时间 (秒)。
    /// -1 = 无限等待 (需要玩家手动点击"开始战斗")。
    /// >0 = 倒计时自动开始。
    /// </summary>
    public const float FirstWavePrepareTime = 5f;   
    
    /// <summary>
    /// 常规波间准备时间 (秒)。
    /// </summary>
    public const float NormalPrepareTime = 10f;
    
    // ═══════════ 预算公式 ═══════════
    // Budget = BaseBudget * (WaveGrowth ^ (CurrentWave - 1))
    
    public const float BaseBudget = 100f;       // 第1波的基础预算
    public const float WaveGrowth = 1.15f;      // 每波预算增长 15%
    
    // ═══════════ 限制 ═══════════
    public const int MaxEnemyCount = 200;       // 同屏最大怪物数 (超过暂停刷怪)
}
