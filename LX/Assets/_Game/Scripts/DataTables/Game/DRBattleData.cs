using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using UnityGameFramework.Runtime;

public class DRBattleData : DataRowBase
{
    private int m_Id;

    public override int Id => m_Id;

    public int Money { get; private set; }

    public int Conscience { get; private set; }

    public int BlackHeart { get; private set; }
    
    public float BlackHeartConvertTime { get; private set; }

    public int BlackHeartAbsorbCount { get; private set; }

    public int CompanyConscience { get; private set; }

    public int CompanyConscienceDamagePerPoint { get; private set; }

    public int CompanyPollution { get; private set; }

    public int CompanyPollutionDamagePerPoint { get; private set; }

    public int TalentPoolDisplayCount { get; private set; }

    public int TalentPoolRefreshPrice { get; private set; }

    public override bool ParseDataRow(string dataRowString, object userData)
    {
        string[] colString = dataRowString.Split('\t');

        int index = 1;

        m_Id = int.Parse(colString[index++]);
        Money = int.Parse(colString[index++]);
        Conscience = int.Parse(colString[index++]);
        BlackHeart = int.Parse(colString[index++]);
        BlackHeartConvertTime = float.Parse(colString[index++]);
        BlackHeartAbsorbCount = int.Parse(colString[index++]);
        CompanyConscience = int.Parse(colString[index++]);
        CompanyConscienceDamagePerPoint = int.Parse(colString[index++]);
        CompanyPollution = int.Parse(colString[index++]);
        CompanyPollutionDamagePerPoint = int.Parse(colString[index++]);
        TalentPoolDisplayCount = int.Parse(colString[index++]);
        TalentPoolRefreshPrice = int.Parse(colString[index++]);

        return true;
    }


}
