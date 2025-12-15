using CYFramework.Core.DataTable;

/// <summary>
/// 波次模板配置表 (数据源: WaveTemplateTable.csv)
/// 用于无尽模式下的动态波次生成。系统会根据当前波次随机选择一个符合条件的模板。
/// </summary>
public class WaveTemplateRow : IDataRow
{
    public int Id { get; private set; }
    
    /// <summary>
    /// 模板名称 (仅备注)。
    /// 如："杂兵潮-单点", "精英突袭-夹击", "BOSS战"。
    /// </summary>
    public string Name { get; private set; }
    
    /// <summary>
    /// 情报文案模板。
    /// 支持占位符 {WaitTime}，运行时替换。
    /// 如："警告：大量敌人在 {Direction} 出现！"
    /// </summary>
    public string PreviewText { get; private set; }

    // ═══════════ 触发条件 (Trigger Conditions) ═══════════
    /// <summary>
    /// 最小波次限制。
    /// [1, 5] = 只在 1-5 波出现 (如新手教学模板)。
    /// </summary>
    public int MinWave { get; private set; }
    
    /// <summary>
    /// 最大波次限制。
    /// 9999 = 无限制。
    /// </summary>
    public int MaxWave { get; private set; }

    /// <summary>
    /// 固定周期。
    /// 0 = 随机出现（遵循 Weight）。
    /// > 0 = 每 N 波强制出现一次（如 10 表示第10, 20, 30波必出）。
    /// </summary>
    public int Period { get; private set; }
    
    /// <summary>
    /// 随机权重。
    /// 当满足 Wave 限制且不是固定周期时，按此权重随机抽取。
    /// </summary>
    public int RandomWeight { get; private set; }

    // ═══════════ 刷怪规则 (Spawn Rules) ═══════════
    /// <summary>
    /// 允许的怪物池类型 (Tag 或 EnemyID)。
    /// 格式："101|102" 或 "Melee|Ranged"。
    /// 空字符串 = 允许所有符合 Wave 限制的怪。
    /// </summary>
    public string EnemyPool { get; private set; }

    /// <summary>
    /// 出生点策略。
    /// "Single" = 随机选 1 个门出。
    /// "All" = 所有门同时出。
    /// "Pincer" = 选 2 个相对的门夹击。
    /// "Random" = 每个怪单独随机（极度分散）。
    /// </summary>
    public string SpawnPointStrategy { get; private set; }

    /// <summary>
    /// 出怪节奏。
    /// "Linear" = 匀速刷出 (如: 总时长内平均分布)。
    /// "Burst" = 开局爆发 (如: 前 20% 时间刷完 80% 怪)。
    /// "Stream" = 持续涓流 (适合超高数量弱怪)。
    /// "Waves" = 分批次 (每隔几秒刷一波)。
    /// </summary>
    public string SpawnRhythm { get; private set; }

    // ═══════════ 强度修正 (Modifiers) ═══════════
    /// <summary>
    /// 预算倍率。
    /// 1.0 = 标准难度。2.0 = 高难波 (预算翻倍，怪更多/更强)。
    /// </summary>
    public float BudgetMultiplier { get; private set; }
    
    /// <summary>
    /// 持续时间倍率。
    /// 基于标准时长 (如 60s) 的乘数。
    /// 0.5 = 短波次快速战斗。
    /// </summary>
    public float DurationMultiplier { get; private set; }
    
    /// <summary>
    /// 准备时间倍率。
    /// 2.0 = 给玩家更多时间准备（常用于 Boss 战前）。
    /// </summary>
    public float PrepareTimeMultiplier { get; private set; }

    public void ParseRow(string[] values)
    {
        int i = 0;
        Id = int.Parse(values[i++]);
        Name = values[i++];
        PreviewText = values[i++];
        
        MinWave = int.Parse(values[i++]);
        MaxWave = int.Parse(values[i++]);
        Period = int.Parse(values[i++]);
        RandomWeight = int.Parse(values[i++]);
        
        EnemyPool = values[i++];
        SpawnPointStrategy = values[i++];
        SpawnRhythm = values[i++];
        
        BudgetMultiplier = float.Parse(values[i++]);
        DurationMultiplier = float.Parse(values[i++]);
        PrepareTimeMultiplier = float.Parse(values[i++]);
    }
}
