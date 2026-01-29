using GameFramework.DataTable;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 地图实体
/// </summary>
public class DefaultMap : EntityLogic
{
    /// <summary>
    /// 主角生成位置
    /// </summary>
    [SerializeField]
    private Transform _protagonistTransform;
    /// <summary>
    /// 公司生成位置
    /// </summary>
    [SerializeField]
    private Transform _companyTransform;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        IDataTable<DRProtagonist> dRProtagonists = GameEntry.StartGame.DRProtagonists;
        if (dRProtagonists != null)
        {
            DRProtagonist dRProtagonist = dRProtagonists.GetDataRow(1);
            if (dRProtagonist != null)
            {
                int entityId = GameEntry.EntityIdPool.Acquire();
                GameEntry.Entity.ShowEntity<ProtagonistEntity>(
                    entityId
                    , dRProtagonist.PrefabPath
                    , "Character"
                    , new object[]
                    {
                        dRProtagonist,
                        _protagonistTransform
                    }
                );
            }
        }
        // 创建公司实体
        GameEntry.Entity.ShowEntity<CompanyEntity>(
            GameEntry.EntityIdPool.Acquire()
            , "Entity/Company/CompanyEntity"
            , "Environment"
            , new object[]
            {
                _companyTransform
            }
        );

    }

    protected override void OnRecycle()
    {
        base.OnRecycle();

    }
}
