using CYFramework.Core.DataTable;

/// <summary>
/// 玩家数据行（用于 CSV 加载）
/// 玩家（良心老板）是玩家直接操控的角色，是整个团队的移动指挥中心
/// </summary>
public class PlayerRow : IDataRow
{
    // ═══════════ 基础信息 ═══════════
    public int Id { get; private set; }
    public string Code { get; private set; }           // Player01, Player02...
    public string Name { get; private set; }           // 玩家名称/皮肤名
    
    // ═══════════ 基础属性 ═══════════
    public float Hp { get; private set; }              // 生命值
    public float MoveSpeed { get; private set; }       // 移动速度
    public float Attack { get; private set; }          // 普攻伤害（较低）
    public float AttackSpeed { get; private set; }     // 攻击速度
    public float Range { get; private set; }           // 攻击范围
    
    // ═══════════ 指挥系统 ═══════════
    public float CommandRange { get; private set; }    // 初始指挥范围（米）
    public float CommandRangeGrowth { get; private set; } // 每级指挥范围成长
    
    // ═══════════ 死亡惩罚 ═══════════
    public float RespawnTime { get; private set; }     // 复活时间（秒）
    public float GoldLossPercent { get; private set; } // 死亡损失资金百分比
    public int ConscienceLoss { get; private set; }    // 死亡损失良心值
    
    // ═══════════ 拾取范围 ═══════════
    public float PickupRange { get; private set; }     // 自动拾取资金的范围
    
    // ═══════════ 资源路径 ═══════════
    public string PrefabPath { get; private set; }
    public string PortraitPath { get; private set; }

    // ═══════════ 辅助属性 ═══════════
    /// <summary>
    /// 获取实体生成的唯一 Key (Player_ + Code)
    /// </summary>
    public string EntityKey => $"Player_{Code}";

    /// <summary>
    /// 解析 CSV 行
    /// </summary>
    public void ParseRow(string[] values)
    {
        int i = 0;
        
        // 基础信息
        Id = int.Parse(values[i++]);
        Code = values[i++];
        Name = values[i++];
        
        // 基础属性
        Hp = float.Parse(values[i++]);
        MoveSpeed = float.Parse(values[i++]);
        Attack = float.Parse(values[i++]);
        AttackSpeed = float.Parse(values[i++]);
        Range = float.Parse(values[i++]);
        
        // 指挥系统
        CommandRange = float.Parse(values[i++]);
        CommandRangeGrowth = float.Parse(values[i++]);
        
        // 死亡惩罚
        RespawnTime = float.Parse(values[i++]);
        GoldLossPercent = float.Parse(values[i++]);
        ConscienceLoss = int.Parse(values[i++]);
        
        // 拾取范围
        PickupRange = float.Parse(values[i++]);
        
        // 资源路径
        PrefabPath = values[i++];
        PortraitPath = values[i++];
    }
}
