using System.Collections;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 子弹数据表
/// </summary>
public class DRProjectile : DataRowBase
{
    private int m_Id;

    public override int Id => m_Id;
    //Code
    public string Code { get; private set; }
    //名称
    public string Name { get; private set; }
    //预制体路径
    public string PrefabPath { get; private set; }

    public override bool ParseDataRow(string dataRowString, object userData)
    {
        string[] colString = dataRowString.Split(',');
        int index = 1;
        m_Id = int.Parse(colString[index++]);
        Code = colString[index++];
        Name = colString[index++];
        PrefabPath = colString[index++];
        return true;
    }
}