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
    /// <summary>是否闪避。</summary>
    public bool IsDodge; // 闪避标记
}

/// <summary>
/// 单位受伤事件：只在扣血时派发，用于显示血条等反馈。
/// </summary>
public struct UnitDamagedEvent // 单位受伤事件结构体
{
    /// <summary>单位实体引用。</summary>
    public UnitEntity Unit; // 受伤单位
    /// <summary>本次伤害数值（>0）。</summary>
    public int Damage; // 伤害值
    /// <summary>当前生命值。</summary>
    public int CurrentHp; // 当前生命
    /// <summary>最大生命值。</summary>
    public int MaxHp; // 最大生命
}

/// <summary>
/// 员工选中状态变化事件：用于刷新选中员工信息显示。
/// </summary>
public struct EmployeeSelectedEvent // 员工选中事件结构体
{
    /// <summary>选中的员工单位（取消选中时为 null）。</summary>
    public UnitEntity Employee; // 选中员工单位引用
    /// <summary>是否选中。</summary>
    public bool IsSelected; // 选中状态标记
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
    /// <summary>招聘类型（急聘/普通/临时工）。</summary>
    public RecruitType RecruitType; // 招聘类型
    /// <summary>招聘平台名称（谐音显示）。</summary>
    public string PlatformName; // 招聘平台名称
    /// <summary>最终招聘价格（已包含倍率）。</summary>
    public int RecruitmentPrice; // 招聘最终价格
    /// <summary>招聘波数（普通：等待波数；临时工：持续波数；急聘可为 0）。</summary>
    public int RecruitWaveCount; // 招聘波数
}
