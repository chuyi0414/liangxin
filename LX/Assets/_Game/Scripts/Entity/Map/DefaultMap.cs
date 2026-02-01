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

    /// <summary>
    /// A*重新扫描协程引用，避免重复并发扫描
    /// </summary>
    private Coroutine _rescanCoroutine;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        
    }

    protected override void OnShow(object userData)
    {
        base.OnShow(userData);

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


        if (_rescanCoroutine != null)
        {
            StopCoroutine(_rescanCoroutine);
            _rescanCoroutine = null;
        }

        _rescanCoroutine = StartCoroutine(RescanGraph());
    }

    private IEnumerator RescanGraph()
    {
        yield return null;

        if (AstarPath.active == null)
            yield break;

        foreach (var _ in AstarPath.active.ScanAsync())
            yield return null;
    }


    protected override void OnRecycle()
    {
        base.OnRecycle();
        if (_rescanCoroutine != null)
        {
            StopCoroutine(_rescanCoroutine);
            _rescanCoroutine = null;
        }
    }
}
