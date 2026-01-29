using System.Collections;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 子弹数据表行数据。
/// </summary>
public class DRProjectile : DataRowBase
{
    /// <summary>
    /// 主键 Id 的内部存储，对应数据表的 Id 列。
    /// </summary>
    private int m_Id;

    /// <summary>
    /// 数据行唯一 Id。
    /// </summary>
    public override int Id => m_Id;
    /// <summary>
    /// 子弹代码，用于逻辑侧索引或配置引用。
    /// </summary>
    public string Code { get; private set; }
    /// <summary>
    /// 子弹显示名称，允许包含空格。
    /// </summary>
    public string Name { get; private set; }
    /// <summary>
    /// 子弹预制体资源路径，用于加载实体。
    /// </summary>
    public string PrefabPath { get; private set; }

    /// <summary>
    /// 解析数据表行，按 Tab 分隔字段。
    /// </summary>
    /// <param name="dataRowString">原始行字符串。</param>
    /// <param name="userData">用户自定义数据。</param>
    /// <returns>解析是否成功。</returns>
    public override bool ParseDataRow(string dataRowString, object userData)
    {
        string[] colString = dataRowString.Split('\t');
        int index = 1;
        m_Id = int.Parse(colString[index++]);
        Code = colString[index++];
        Name = colString[index++];
        PrefabPath = colString[index++];
        return true;
    }
}
