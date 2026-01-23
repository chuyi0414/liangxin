using GameFramework.DataTable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityGameFramework.Runtime;

/// <summary>
/// 主角数据表
/// </summary>
public class DRProtagonist : DataRowBase
{
    private int m_Id;

    public override int Id => m_Id;
    /// <summary>
    /// Code
    /// </summary>
    public string Code { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 阵容
    /// </summary>
    public int Camp { get; set; }
    /// <summary>
    /// 预制体路径
    /// </summary>
    public string PrefabPath { get; set; }
    /// <summary>
    /// 移动速度
    /// </summary>
    public float MoveSeep { get; set; }
    /// <summary>
    /// 子弹预制体路径
    /// </summary>
    public int ProjectileId { get; set; }
    /// <summary>
    /// 子弹速度
    /// </summary>
    public float ProjectileSpeed { get; set; }


    public override bool ParseDataRow(string dataRowString, object userData)
    {
        string[] colString = dataRowString.Split(' ');
        int index = 1;
        m_Id = int.Parse(colString[index++]);
        Code = colString[index++];
        Name = colString[index++];
        Camp = int.Parse(colString[index++]);
        PrefabPath = colString[index++];
        MoveSeep = float.Parse(colString[index++]);
        ProjectileId = int.Parse(colString[index++]);
        ProjectileSpeed = float.Parse(colString[index++]);
        return true;
    }
}