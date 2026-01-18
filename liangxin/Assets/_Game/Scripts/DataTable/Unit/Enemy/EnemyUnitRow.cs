using System; // System 基础类型引用
using System.Globalization; // CultureInfo 解析格式引用
using CYFramework.Core.DataTable; // 数据表接口引用

/// <summary>
/// 敌人数据表行（对应 Enemy.csv）。
/// </summary>
public sealed class EnemyUnitRow : IDataRow
{
    /// <summary>唯一 Id（DataTable 主键）。</summary>
    public int Id;
    /// <summary>单位编码。</summary>
    public string Code;
    /// <summary>单位名称。</summary>
    public string Name;
    /// <summary>单位阵营。</summary>
    public UnitCamp Camp;
    /// <summary>单位状态。</summary>
    public UnitLifeState LifeState;
    /// <summary>单位等级。</summary>
    public int Level;
    /// <summary>最大生命值。</summary>
    public int MaxHp;
    /// <summary>攻击力。</summary>
    public int Attack;
    /// <summary>污染伤害最小值。</summary>
    public float PollutionDamageMin;
    /// <summary>污染伤害最大值。</summary>
    public float PollutionDamageMax;
    /// <summary>防御力。</summary>
    public int Defense;
    /// <summary>固定防御穿透值。</summary>
    public int DefensePenetration;
    /// <summary>百分比防御穿透（0-1）。</summary>
    public float DefensePenetrationRate;
    /// <summary>暴击率（0-1）。</summary>
    public float CritRate;
    /// <summary>暴击倍率（>=1）。</summary>
    public float CritMultiplier; // 敌人暴击倍率
    /// <summary>闪避率（0-1）。</summary>
    public float DodgeRate;
    /// <summary>是否远程单位。</summary>
    public bool IsRanged;
    /// <summary>移动速度。</summary>
    public float MoveSpeed;
    /// <summary>攻击距离。</summary>
    public float AttackRange;
    /// <summary>可视范围。</summary>
    public float SightRange;
    /// <summary>攻击间隔（秒）。</summary>
    public float AttackInterval;
    /// <summary>攻击停顿时长（秒）。</summary>
    public float AttackStopDuration;
    /// <summary>掉落金钱概率（0-1）。</summary>
    public float MoneyDropProb;
    /// <summary>掉落金钱最小数量。</summary>
    public int MoneyDropMin;
    /// <summary>掉落金钱最大数量。</summary>
    public int MoneyDropMax;
    /// <summary>掉落黑心概率（0-1）。</summary>
    public float BlackHeartDropProb;
    /// <summary>掉落黑心最小数量。</summary>
    public int BlackHeartDropMin;
    /// <summary>掉落黑心最大数量。</summary>
    public int BlackHeartDropMax;
    /// <summary>风格 Id 列表（用 | 分隔）。</summary>
    public string StyleIds; // 风格列表字符串
    /// <summary>预制体资源路径（Resources 相对路径，无扩展名）。</summary>
    public string PrefabPath; // 敌人预制体路径

    /// <summary>缓存后的风格 Id 数组。</summary>
    private int[] _cachedStyleIds; // 风格 Id 数组缓存
    /// <summary>是否已缓存风格 Id 数组。</summary>
    private bool _hasCachedStyleIds; // 风格 Id 缓存标记

    int IDataRow.Id => Id;

    /// <summary>
    /// CSV 解析（顺序需与 Enemy.csv 表头一致）。
    /// </summary>
    public void ParseRow(string[] values)
    {
        Id = int.Parse(values[0]); // 解析 Id
        Code = values[1]; // 解析单位编码
        Name = values[2]; // 解析单位名称
        Camp = (UnitCamp)int.Parse(values[3]); // 解析阵营
        LifeState = (UnitLifeState)int.Parse(values[4]); // 解析生命状态
        Level = int.Parse(values[5]); // 解析等级
        MaxHp = int.Parse(values[6]); // 解析最大生命值
        Attack = int.Parse(values[7]); // 解析攻击力
        PollutionDamageMin = float.Parse(values[8], CultureInfo.InvariantCulture); // 解析污染伤害最小值
        PollutionDamageMax = float.Parse(values[9], CultureInfo.InvariantCulture); // 解析污染伤害最大值
        Defense = int.Parse(values[10]); // 解析防御力
        DefensePenetration = int.Parse(values[11]); // 解析固定防御穿透
        DefensePenetrationRate = float.Parse(values[12], CultureInfo.InvariantCulture); // 解析百分比防御穿透
        CritRate = float.Parse(values[13], CultureInfo.InvariantCulture); // 解析暴击率
        DodgeRate = float.Parse(values[14], CultureInfo.InvariantCulture); // 解析闪避率
        IsRanged = bool.Parse(values[15]); // 解析远程标记
        MoveSpeed = float.Parse(values[16], CultureInfo.InvariantCulture); // 解析移动速度
        AttackRange = float.Parse(values[17], CultureInfo.InvariantCulture); // 解析攻击范围
        SightRange = float.Parse(values[18], CultureInfo.InvariantCulture); // 解析可视范围
        AttackInterval = float.Parse(values[19], CultureInfo.InvariantCulture); // 解析攻击间隔
        AttackStopDuration = float.Parse(values[20], CultureInfo.InvariantCulture); // 解析攻击停顿时长
        MoneyDropProb = float.Parse(values[21], CultureInfo.InvariantCulture); // 解析掉落概率
        MoneyDropMin = int.Parse(values[22]); // 解析掉落最小数量
        MoneyDropMax = int.Parse(values[23]); // 解析掉落最大数量
        BlackHeartDropProb = float.Parse(values[24], CultureInfo.InvariantCulture); // 解析黑心掉落概率
        BlackHeartDropMin = int.Parse(values[25]); // 解析黑心掉落最小数量
        BlackHeartDropMax = int.Parse(values[26]); // 解析黑心掉落最大数量
        StyleIds = values[27]; // 解析风格 Id 列表字符串
        PrefabPath = values.Length > 28 ? values[28] : string.Empty; // 解析预制体路径（兼容旧表）
        CritMultiplier = values.Length > 29 ? float.Parse(values[29], CultureInfo.InvariantCulture) : 2f; // 解析暴击倍率（兼容旧表）
    }

    /// <summary>
    /// 获取缓存后的风格 Id 数组（按需拆分并过滤空项）。
    /// </summary>
    /// <param name="styleIds">输出风格 Id 数组。</param>
    /// <returns>是否获得有效风格 Id 数组。</returns>
    public bool TryGetStyleIds(out int[] styleIds) // 风格 Id 数组获取入口
    {
        if (_hasCachedStyleIds)
        {
            styleIds = _cachedStyleIds; // 直接返回缓存数组
            return styleIds != null && styleIds.Length > 0; // 返回缓存是否有效
        }

        if (string.IsNullOrEmpty(StyleIds))
        {
            _cachedStyleIds = Array.Empty<int>(); // 空字符串时缓存空数组
            _hasCachedStyleIds = true; // 标记已缓存
            styleIds = _cachedStyleIds; // 输出空数组
            return false; // 返回无有效风格
        }

        var rawItems = StyleIds.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries); // 按 | 拆分字符串
        if (rawItems == null || rawItems.Length == 0)
        {
            _cachedStyleIds = Array.Empty<int>(); // 无有效项时缓存空数组
            _hasCachedStyleIds = true; // 标记已缓存
            styleIds = _cachedStyleIds; // 输出空数组
            return false; // 返回无有效风格
        }

        var temp = new int[rawItems.Length]; // 创建临时数组
        var validCount = 0; // 记录有效数量
        for (int i = 0; i < rawItems.Length; i++)
        {
            var item = rawItems[i]; // 获取当前项
            if (string.IsNullOrEmpty(item))
            {
                continue; // 空字符串直接跳过
            }

            item = item.Trim(); // 去除首尾空格
            if (string.IsNullOrEmpty(item))
            {
                continue; // 去空格后仍为空时跳过
            }

            if (!int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                continue; // 无法解析时跳过
            }

            temp[validCount] = value; // 写入解析后的风格 Id
            validCount++; // 累计有效数量
        }

        if (validCount <= 0)
        {
            _cachedStyleIds = Array.Empty<int>(); // 无有效 Id 时缓存空数组
            _hasCachedStyleIds = true; // 标记已缓存
            styleIds = _cachedStyleIds; // 输出空数组
            return false; // 返回无有效风格
        }

        if (validCount == temp.Length)
        {
            _cachedStyleIds = temp; // 全部有效时直接使用临时数组
        }
        else
        {
            var result = new int[validCount]; // 创建精确长度数组
            Array.Copy(temp, result, validCount); // 拷贝有效 Id
            _cachedStyleIds = result; // 缓存结果数组
        }

        _hasCachedStyleIds = true; // 标记已缓存
        styleIds = _cachedStyleIds; // 输出结果数组
        return styleIds.Length > 0; // 返回是否存在有效风格
    }
}
