// 引用 System 命名空间，使用 Array/StringSplitOptions
using System; // System 基础类型引用
// 引用 CYFramework 数据表接口
using CYFramework.Core.DataTable; // 数据表接口引用

/// <summary>
/// 子弹选择规则枚举（用于数组选择方式）。
/// </summary>
public enum BulletSelectRule // 子弹选择规则枚举
{
    /// <summary>随机选择。</summary>
    Random = 0, // 随机选择
    /// <summary>顺序轮播。</summary>
    Sequential = 1 // 顺序轮播
}

/// <summary>
/// 子弹数组数据表行（对应 BulletArray.csv）。
/// </summary>
public sealed class BulletArrayRow : IDataRow // 子弹数组数据表行定义
{
    /// <summary>唯一 Id（DataTable 主键）。</summary>
    public int Id; // 子弹数组 Id
    /// <summary>子弹选择规则。</summary>
    public BulletSelectRule SelectRule; // 选择规则
    /// <summary>子弹预制体路径集合（用 | 分隔，Resources 相对路径）。</summary>
    public string PrefabPaths; // 预制体路径字符串

    /// <summary>缓存后的子弹预制体路径数组。</summary>
    private string[] _cachedPrefabPaths; // 路径数组缓存
    /// <summary>是否已缓存路径数组。</summary>
    private bool _hasCachedPrefabPaths; // 路径缓存标记

    int IDataRow.Id => Id; // 数据表主键映射

    /// <summary>
    /// CSV 解析（顺序需与 BulletArray.csv 表头一致）。
    /// </summary>
    public void ParseRow(string[] values) // CSV 解析入口
    {
        Id = int.Parse(values[0]); // 解析 Id
        SelectRule = (BulletSelectRule)int.Parse(values[1]); // 解析选择规则
        PrefabPaths = values[2]; // 解析路径字符串
    }

    /// <summary>
    /// 获取缓存后的预制体路径数组（按需拆分并过滤空项）。
    /// </summary>
    /// <param name="prefabPaths">输出路径数组。</param>
    /// <returns>是否获得有效路径数组。</returns>
    public bool TryGetPrefabPaths(out string[] prefabPaths) // 路径数组获取入口
    {
        if (_hasCachedPrefabPaths)
        {
            prefabPaths = _cachedPrefabPaths; // 直接返回缓存数组
            return prefabPaths != null && prefabPaths.Length > 0; // 返回缓存是否有效
        }

        if (string.IsNullOrEmpty(PrefabPaths))
        {
            _cachedPrefabPaths = Array.Empty<string>(); // 空路径时缓存空数组
            _hasCachedPrefabPaths = true; // 标记已缓存
            prefabPaths = _cachedPrefabPaths; // 输出空数组
            return false; // 返回无有效路径
        }

        var rawItems = PrefabPaths.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries); // 按 | 拆分路径
        var validCount = 0; // 记录有效路径数量
        for (int i = 0; i < rawItems.Length; i++)
        {
            var item = rawItems[i]; // 获取当前路径
            if (string.IsNullOrEmpty(item))
            {
                continue; // 空字符串直接跳过
            }

            rawItems[i] = item.Trim(); // 去除路径首尾空格
            if (string.IsNullOrEmpty(rawItems[i]))
            {
                continue; // 去空格后仍为空时跳过
            }

            validCount++; // 记录有效路径
        }

        if (validCount <= 0)
        {
            _cachedPrefabPaths = Array.Empty<string>(); // 无有效路径时缓存空数组
            _hasCachedPrefabPaths = true; // 标记已缓存
            prefabPaths = _cachedPrefabPaths; // 输出空数组
            return false; // 返回无有效路径
        }

        var result = new string[validCount]; // 分配结果数组
        var index = 0; // 结果数组写入索引
        for (int i = 0; i < rawItems.Length; i++)
        {
            var item = rawItems[i]; // 获取当前路径
            if (string.IsNullOrEmpty(item))
            {
                continue; // 空路径跳过
            }

            if (index >= result.Length)
            {
                break; // 写入达到上限时退出
            }

            result[index] = item; // 写入有效路径
            index++; // 递增写入索引
        }

        _cachedPrefabPaths = result; // 缓存结果数组
        _hasCachedPrefabPaths = true; // 标记已缓存
        prefabPaths = _cachedPrefabPaths; // 输出结果数组
        return prefabPaths.Length > 0; // 返回是否存在有效路径
    }
}
