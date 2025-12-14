using CYFramework.Core.DataTable;

/// <summary>
/// 员工数据行（用于 CSV 加载）
/// </summary>
public class EmployeeRow : IDataRow
{
    // ═══════════ 基础信息 ═══════════
    public int Id { get; private set; }
    public string Code { get; private set; }           // F01, F02...
    public string JobTitle { get; private set; }       // 职业名称
    public int Department { get; private set; }        // 部门类型 (1-5)
    
    // ═══════════ 等级描述 ═══════════
    public string Lv1Desc { get; private set; }        // Lv1描述
    public string Lv2Desc { get; private set; }        // Lv2描述
    public string Lv5Task { get; private set; }        // Lv5任务目标
    public string Lv5Ultimate { get; private set; }    // Lv5大招描述
    
    // ═══════════ 战斗属性 ═══════════
    public float Hp { get; private set; }              // 生命值
    public float Attack { get; private set; }          // 攻击力
    public float AttackSpeed { get; private set; }     // 攻击速度（每秒攻击次数）
    public float MoveSpeed { get; private set; }       // 移动速度
    public float Range { get; private set; }           // 攻击射程
    
    // ═══════════ 招募升级费用 ═══════════
    public int RecruitCost { get; private set; }       // 招募费用（资金）
    public int UpgradeCostLv2 { get; private set; }    // 升级到Lv2费用
    public int UpgradeCostLv3 { get; private set; }    // 升级到Lv3费用
    public int UpgradeCostLv4 { get; private set; }    // 升级到Lv4费用
    
    // ═══════════ 技能ID ═══════════
    public string Lv1SkillId { get; private set; }     // Lv1普攻技能ID
    public string Lv2SkillId { get; private set; }     // Lv2自动技能ID
    public string Lv5SkillId { get; private set; }     // Lv5传奇大招ID
    
    // ═══════════ 资源路径 ═══════════
    public string PrefabPath { get; private set; }     // 员工预制体路径
    public string PortraitPath { get; private set; }   // 员工头像路径

    // ═══════════ 辅助属性 ═══════════
    /// <summary>
    /// 获取实体生成的唯一 Key (Employee_ + Code)
    /// </summary>
    public string EntityKey => $"Employee_{Code}";

    /// <summary>
    /// 解析 CSV 行
    /// </summary>
    public void ParseRow(string[] values)
    {
        int i = 0;
        
        // 基础信息
        Id = int.Parse(values[i++]);
        Code = values[i++];
        JobTitle = values[i++];
        Department = int.Parse(values[i++]);
        
        // 等级描述
        Lv1Desc = values[i++];
        Lv2Desc = values[i++];
        Lv5Task = values[i++];
        Lv5Ultimate = values[i++];
        
        // 战斗属性
        Hp = float.Parse(values[i++]);
        Attack = float.Parse(values[i++]);
        AttackSpeed = float.Parse(values[i++]);
        MoveSpeed = float.Parse(values[i++]);
        Range = float.Parse(values[i++]);
        
        // 招募升级
        RecruitCost = int.Parse(values[i++]);
        UpgradeCostLv2 = int.Parse(values[i++]);
        UpgradeCostLv3 = int.Parse(values[i++]);
        UpgradeCostLv4 = int.Parse(values[i++]);
        
        // 技能ID
        Lv1SkillId = values[i++];
        Lv2SkillId = values[i++];
        Lv5SkillId = values[i++];
        
        // 资源路径
        PrefabPath = values[i++];
        PortraitPath = values[i++];
    }
    
    /// <summary>
    /// 获取部门类型枚举
    /// </summary>
    public DepartmentType GetDepartment() => (DepartmentType)Department;
}
