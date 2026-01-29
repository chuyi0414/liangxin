using GameFramework.DataTable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityGameFramework.Runtime;

/// <summary>
/// 主角数据表行数据。
/// </summary>
public class DRProtagonist : DataRowBase
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
    /// 主角代码，用于逻辑侧索引或配置引用。
    /// </summary>
    public string Code { get; set; }
    /// <summary>
    /// 主角显示名称，允许包含空格。
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 阵营编号，用于敌我关系或阵营判定。
    /// </summary>
    public CAMP Camp { get; set; }
    /// <summary>
    /// 主角预制体资源路径，用于加载实体。
    /// </summary>
    public string PrefabPath { get; set; }
    /// <summary>
    /// 移动速度（配置字段 MoveSeep）。
    /// </summary>
    public float MoveSeep { get; set; }
    /// <summary>
    /// 子弹数据表 Id，用于关联子弹配置。
    /// </summary>
    public int ProjectileId { get; set; }
    /// <summary>
    /// 子弹飞行速度。
    /// </summary>
    public float ProjectileSpeed { get; set; }


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
        Camp = (CAMP)int.Parse(colString[index++]);
        PrefabPath = colString[index++];
        MoveSeep = float.Parse(colString[index++]);
        ProjectileId = int.Parse(colString[index++]);
        ProjectileSpeed = float.Parse(colString[index++]);
        return true;
    }
}
