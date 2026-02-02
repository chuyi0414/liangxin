using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 公司实体
/// </summary>
public class CompanyEntity : EntityLogic
{
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        
    }

    protected override void OnShow(object userData)
    {
        base.OnShow(userData);

        object[] os = userData as object[];
        transform.position = ((Transform)os[0]).position;

        GameEntry.GameManager.companyEntity = this;
    }
}
