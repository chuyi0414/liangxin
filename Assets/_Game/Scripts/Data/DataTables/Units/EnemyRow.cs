using CYFramework.Core.DataTable;

/// <summary>
/// 敌方单位配置表 (数据源: EnemyTable.csv)
/// 包含基础属性和动态生成的定价参数
/// </summary>
public class EnemyRow : IDataRow
{
    // ═══════════ 基础信息 ═══════════
    public int Id { get; private set; }
    public string Name { get; private set; }           // 怪物名称 (如: "实习生僵尸", "摸鱼怪", "甲方恶魔")
    public string Description { get; private set; }    // 描述
    
    // ═══════════ 战斗属性 ═══════════
    public float Hp { get; private set; }              // 生命值
    public float Attack { get; private set; }          // 攻击力
    public float MoveSpeed { get; private set; }       // 移动速度
    public float Range { get; private set; }           // 攻击范围
    public string ProjectilePath { get; private set; } // 投射物路径
    public float AttackInterval { get; private set; }  // 攻击间隔
    public int[] SkillIds { get; private set; }        // 技能ID列表 (如 "101|102")
    
    // ═══════════ 资源收益 ═══════════
    public int DropGold { get; private set; }          // 掉落金币
    public int DropExp { get; private set; }           // 掉落经验(可选)

    // ═══════════ 动态生成参数 (Dynamic Budget) ═══════════
    /// <summary>
    /// 怪物造价 (Cost)。
    /// 决定了它消耗多少“波次预算”。
    /// 例如: 预算500，Cost=10的怪能刷50个，Cost=100的怪只能刷5个。
    /// </summary>
    public int Cost { get; private set; }

    /// <summary>
    /// 最小波次限制。
    /// 0 = 随时可出。
    /// 10 = 第 10 波以后才允许随机到这个怪。
    /// </summary>
    public int MinWave { get; private set; }

    /// <summary>
    /// 权重 (Weight)。
    /// 当预算允许购买多种怪物时，权重高的更容易被选中。
    /// </summary>
    public int Weight { get; private set; }

    /// <summary>
    /// 是否允许作为精英怪。
    /// 如果是，动态生成时可能会给它套个“精英模板” (属性x2, Costx2)。
    /// </summary>
    public bool AllowElite { get; private set; }

    // ═══════════ 资源路径 ═══════════
    public string PrefabPath { get; private set; }

    public void ParseRow(string[] values)
    {
        int i = 0;
        // 基础信息
        Id = int.Parse(values[i++]);
        Name = values[i++];
        Description = values[i++];
        
        // 战斗属性
        Hp = float.Parse(values[i++]);
        Attack = float.Parse(values[i++]);
        MoveSpeed = float.Parse(values[i++]);
        Range = float.Parse(values[i++]);
        ProjectilePath = values[i++];
        AttackInterval = float.Parse(values[i++]);
        
        string skillStr = values[i++];
        if (string.IsNullOrEmpty(skillStr) || skillStr == "0")
        {
            SkillIds = new int[0];
        }
        else
        {
            SkillIds = System.Array.ConvertAll(skillStr.Split('|'), int.Parse);
        }
        
        // 资源收益
        DropGold = int.Parse(values[i++]);
        DropExp = int.Parse(values[i++]);
        
        // 动态生成参数
        Cost = int.Parse(values[i++]);
        MinWave = int.Parse(values[i++]);
        Weight = int.Parse(values[i++]);
        AllowElite = bool.Parse(values[i++]);
        
        // 资源路径
        PrefabPath = values[i++];
    }
}
