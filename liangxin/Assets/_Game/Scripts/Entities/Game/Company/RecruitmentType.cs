/// <summary>
/// 招聘类型枚举。
/// </summary>
public enum RecruitType // 招聘类型枚举
{
    /// <summary>急聘。</summary>
    Urgent = 0, // 急聘类型
    /// <summary>普通招聘。</summary>
    Normal = 1, // 普通招聘类型
    /// <summary>临时工。</summary>
    Temp = 2 // 临时工类型
}

/// <summary>
/// 招聘类型工具：提供价格倍率与显示文本。
/// </summary>
public static class RecruitTypeUtility // 招聘类型工具类
{
    /// <summary>急聘价格倍率。</summary>
    public const int UrgentPriceMultiplier = 3; // 急聘倍率常量
    /// <summary>临时工价格倍率。</summary>
    public const int TempPriceMultiplier = 2; // 临时工倍率常量
    /// <summary>普通招聘价格倍率。</summary>
    public const int NormalPriceMultiplier = 1; // 普通招聘倍率常量

    /// <summary>
    /// 获取招聘类型显示文本。
    /// </summary>
    /// <param name="type">招聘类型。</param>
    /// <returns>显示名称。</returns>
    public static string GetDisplayName(RecruitType type) // 招聘类型文本获取入口
    {
        switch (type) // 类型分支
        {
            case RecruitType.Urgent: // 急聘分支
                return "急聘"; // 返回急聘文本
            case RecruitType.Temp: // 临时工分支
                return "临时工"; // 返回临时工文本
            case RecruitType.Normal: // 普通招聘分支
            default: // 兜底分支
                return "普通招聘"; // 返回普通招聘文本
        }
    }

    /// <summary>
    /// 获取招聘类型价格倍率。
    /// </summary>
    /// <param name="type">招聘类型。</param>
    /// <returns>价格倍率。</returns>
    public static int GetPriceMultiplier(RecruitType type) // 价格倍率获取入口
    {
        switch (type) // 类型分支
        {
            case RecruitType.Urgent: // 急聘分支
                return UrgentPriceMultiplier; // 返回急聘倍率
            case RecruitType.Temp: // 临时工分支
                return TempPriceMultiplier; // 返回临时工倍率
            case RecruitType.Normal: // 普通招聘分支
            default: // 兜底分支
                return NormalPriceMultiplier; // 返回普通招聘倍率
        }
    }

    /// <summary>
    /// 计算最终招聘价格（基于基础价格与类型倍率）。
    /// </summary>
    /// <param name="basePrice">基础价格。</param>
    /// <param name="type">招聘类型。</param>
    /// <returns>最终价格。</returns>
    public static int CalculatePrice(int basePrice, RecruitType type) // 价格计算入口
    {
        var multiplier = GetPriceMultiplier(type); // 获取价格倍率
        return basePrice * multiplier; // 计算最终价格
    }
}
