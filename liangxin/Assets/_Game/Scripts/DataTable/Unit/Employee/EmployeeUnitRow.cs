// 引用 System 命名空间，使用 Array/StringSplitOptions
using System; // System 基础类型引用
// 引用 System.Globalization 命名空间，使用 CultureInfo
using System.Globalization; // CultureInfo 解析格式引用
// 引用 CYFramework 数据表接口
using CYFramework.Core.DataTable; // 数据表接口引用

/// <summary>
/// 员工数据表行（对应 Employee.csv）。
/// </summary>
public sealed class EmployeeUnitRow : IDataRow // 员工数据表行定义
{
    /// <summary>唯一 Id（DataTable 主键）。</summary>
    public int Id; // 员工 Id
    /// <summary>单位编码（如 F01）。</summary>
    public string Code; // 员工编码
    /// <summary>单位名称。</summary>
    public string Name; // 员工名称
    /// <summary>单位阵营。</summary>
    public UnitCamp Camp; // 员工阵营
    /// <summary>单位状态。</summary>
    public UnitLifeState LifeState; // 员工生命状态
    /// <summary>单位等级。</summary>
    public int Level; // 员工等级
    /// <summary>最大生命值。</summary>
    public int MaxHp; // 员工最大生命值
    /// <summary>攻击力。</summary>
    public int Attack; // 员工攻击力
    /// <summary>防御力。</summary>
    public int Defense; // 员工防御力
    /// <summary>固定防御穿透值。</summary>
    public int DefensePenetration; // 员工固定防御穿透值
    /// <summary>百分比防御穿透（0-1）。</summary>
    public float DefensePenetrationRate; // 员工百分比防御穿透
    /// <summary>暴击率（0-1）。</summary>
    public float CritRate; // 员工暴击率
    /// <summary>闪避率（0-1）。</summary>
    public float DodgeRate; // 员工闪避率
    /// <summary>是否远程单位。</summary>
    public bool IsRanged; // 员工远程标记
    /// <summary>移动速度。</summary>
    public float MoveSpeed; // 员工移动速度
    /// <summary>攻击距离。</summary>
    public float AttackRange; // 员工攻击范围
    /// <summary>可视范围。</summary>
    public float SightRange; // 员工可视范围
    /// <summary>攻击间隔（秒）。</summary>
    public float AttackInterval; // 员工攻击间隔
    /// <summary>招聘价格。</summary>
    public int RecruitmentPrice; // 员工招聘价格
    /// <summary>风格 Id 列表（用 | 分隔）。</summary>
    public string StyleIds; // 员工风格列表字符串
    /// <summary>头像图标资源路径（Resources 相对路径，无扩展名）。</summary>
    public string IconPath; // 员工头像路径
    /// <summary>预制体资源路径（Resources 相对路径，无扩展名）。</summary>
    public string PrefabPath; // 员工预制体路径

    /// <summary>缓存后的风格 Id 数组。</summary>
    private int[] _cachedStyleIds; // 风格 Id 数组缓存
    /// <summary>是否已缓存风格 Id 数组。</summary>
    private bool _hasCachedStyleIds; // 风格 Id 缓存标记

    int IDataRow.Id => Id; // 数据表主键映射

    /// <summary>
    /// CSV 解析（顺序需与 Employee.csv 表头一致）。
    /// </summary>
    public void ParseRow(string[] values) // CSV 解析入口
    {
        Id = int.Parse(values[0]); // 解析 Id
        Code = values[1]; // 解析单位编码
        Name = values[2]; // 解析单位名称
        Camp = (UnitCamp)int.Parse(values[3]); // 解析阵营
        LifeState = (UnitLifeState)int.Parse(values[4]); // 解析生命状态
        Level = int.Parse(values[5]); // 解析等级
        MaxHp = int.Parse(values[6]); // 解析最大生命值
        Attack = int.Parse(values[7]); // 解析攻击力
        Defense = int.Parse(values[8]); // 解析防御力
        DefensePenetration = int.Parse(values[9]); // 解析固定防御穿透
        DefensePenetrationRate = float.Parse(values[10], CultureInfo.InvariantCulture); // 解析百分比防御穿透
        CritRate = float.Parse(values[11], CultureInfo.InvariantCulture); // 解析暴击率
        DodgeRate = float.Parse(values[12], CultureInfo.InvariantCulture); // 解析闪避率
        IsRanged = bool.Parse(values[13]); // 解析远程标记
        MoveSpeed = float.Parse(values[14], CultureInfo.InvariantCulture); // 解析移动速度
        AttackRange = float.Parse(values[15], CultureInfo.InvariantCulture); // 解析攻击距离
        SightRange = float.Parse(values[16], CultureInfo.InvariantCulture); // 解析可视范围
        AttackInterval = float.Parse(values[17], CultureInfo.InvariantCulture); // 解析攻击间隔
        RecruitmentPrice = int.Parse(values[18]); // 解析招聘价格
        StyleIds = values[19]; // 解析风格 Id 列表字符串
        IconPath = values.Length > 20 ? values[20] : string.Empty; // 解析头像图标路径（兼容旧表）
        PrefabPath = values.Length > 21 ? values[21] : string.Empty; // 解析预制体路径（兼容旧表）
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
