using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

public class DREnemy : DataRowBase
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
    /// Code
    /// </summary>
    public string Code { get; private set; }
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; private set; }
    /// <summary>
    /// 阵营
    /// </summary>
    public CAMP Camp {  get; private set; }
    /// <summary>
    /// 预制体路径
    /// </summary>
    public string PrefabPath { get; private set; }
    /// <summary>
    /// 怪物速度
    /// </summary>
    public float MoveSeep { get; private set; }

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
        return true;
    }
}
