/// <summary>
/// 单位生成事件：用于创建血条等跟随 UI。
/// </summary>
public struct UnitSpawnedEvent // 单位生成事件结构体
{
    /// <summary>单位实体引用。</summary>
    public UnitEntity Unit; // 单位实体
    /// <summary>当前生命值。</summary>
    public int CurrentHp; // 当前生命
    /// <summary>最大生命值。</summary>
    public int MaxHp; // 最大生命
}

/// <summary>
/// 单位移除事件：用于回收血条等跟随 UI。
/// </summary>
public struct UnitDespawnedEvent // 单位移除事件结构体
{
    /// <summary>单位实体引用。</summary>
    public UnitEntity Unit; // 单位实体
}

/// <summary>
/// 单位生命变化事件：用于刷新血条数值。
/// </summary>
public struct UnitHpChangedEvent // 单位血量变化事件结构体
{
    /// <summary>单位实体引用。</summary>
    public UnitEntity Unit; // 单位实体
    /// <summary>当前生命值。</summary>
    public int CurrentHp; // 当前生命
    /// <summary>最大生命值。</summary>
    public int MaxHp; // 最大生命
}

/// <summary>
/// 伤害飘字事件：用于播放伤害数字。
/// </summary>
public struct UnitDamagePopupEvent // 伤害飘字事件结构体
{
    /// <summary>单位实体引用。</summary>
    public UnitEntity Unit; // 受击单位
    /// <summary>伤害数值（应为正数）。</summary>
    public int Damage; // 伤害值
    /// <summary>是否暴击。</summary>
    public bool IsCrit; // 暴击标记
}

/// <summary>
/// 单位生命状态变化事件：用于死亡表现等。
/// </summary>
public struct UnitLifeStateChangedEvent // 单位生命状态变化事件结构体
{
    /// <summary>单位实体引用。</summary>
    public UnitEntity Unit; // 单位实体
    /// <summary>旧状态。</summary>
    public UnitLifeState OldState; // 旧状态
    /// <summary>新状态。</summary>
    public UnitLifeState NewState; // 新状态
}

/// <summary>
/// 员工招聘请求事件：UI 点击招聘按钮后派发，由 CompanyEntity 负责在刷新点创建员工实体。
/// </summary>
public struct EmployeeRecruitRequestedEvent // 员工招聘请求事件结构体
{
    /// <summary>员工配置 Id（Employee.csv 的 Id）。</summary>
    public int EmployeeId; // 员工配置 Id
}
