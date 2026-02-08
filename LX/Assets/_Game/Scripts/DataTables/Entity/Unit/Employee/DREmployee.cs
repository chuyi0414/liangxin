using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DREmployee : DRUnit
{
    /// <summary>
    /// 解析数据表行，按 Tab 分隔字段。
    /// </summary>
    /// <param name="dataRowString">原始行字符串。</param>
    /// <param name="userData">用户自定义数据。</param>
    /// <returns>解析是否成功。</returns>
    public override bool ParseDataRow(string dataRowString, object userData)
    {
        string[] colString = dataRowString.Split('	');
        int index = 1;
        m_Id = int.Parse(colString[index++]);
        Code = colString[index++];
        Name = colString[index++];
        Camp = (CAMP)int.Parse(colString[index++]);
        PrefabPath = colString[index++];
        MoveSeep = float.Parse(colString[index++]);
        AttackRange = float.Parse(colString[index++]);
        VisualScope = float.Parse(colString[index++]);
        AttackType = (ATTACKTYPE)int.Parse(colString[index++]);
        ProjectileId = int.Parse(colString[index++]);
        ProjectileSpeed = float.Parse(colString[index++]);
        HP = float.Parse(colString[index++]);
        Attack = float.Parse(colString[index++]);
        AttackSpeed = float.Parse(colString[index++]);
        return true;
    }

    /// <summary>
    /// 从指定敌人数据行复制所有字段（用于运行时独立数据副本）。
    /// </summary>
    /// <param name="source">源敌人数据行（只读配置）。</param>
    public void CopyFrom(DREmployee source)
    {
        if (source == null)
        {
            return;
        }

        // 逐项复制基础属性，确保运行时副本与配置一致但互不共享。
        m_Id = source.m_Id;
        Code = source.Code;
        Name = source.Name;
        Camp = source.Camp;
        PrefabPath = source.PrefabPath;
        MoveSeep = source.MoveSeep;
        AttackRange = source.AttackRange;
        VisualScope = source.VisualScope;
        AttackType = source.AttackType;
        ProjectileId = source.ProjectileId;
        ProjectileSpeed = source.ProjectileSpeed;
        HP = source.HP;
        Attack = source.Attack;
        AttackSpeed = source.AttackSpeed;
    }
}
