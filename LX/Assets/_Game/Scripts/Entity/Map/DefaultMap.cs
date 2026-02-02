using GameFramework.DataTable;
using Pathfinding;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// ��ͼʵ��
/// </summary>
public class DefaultMap : EntityLogic
{
    /// <summary>
    /// ��������λ��
    /// </summary>
    [SerializeField]
    private Transform _protagonistTransform;
    /// <summary>
    /// ��˾����λ��
    /// </summary>
    [SerializeField]
    private Transform _companyTransform;

    /// <summary>
    /// A*扫描协程变量，避免重复启动扫描
    /// </summary>
    private Coroutine _rescanCoroutine;

    /// <summary>
    /// A*网格节点大小（越大越省性能，但寻路精度更低）
    /// </summary>
    [SerializeField]
    private float _gridNodeSize = EnemyAIConfig.GridNodeSize;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        
    }

    protected override void OnShow(object userData)
    {
        base.OnShow(userData);

        IDataTable<DRProtagonist> dRProtagonists = GameEntry.GameManager.DRProtagonists;
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
        // ������˾ʵ��
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

        // 尝试设置网格图节点大小，降低节点数量提升性能
        GridGraph gridGraph = AstarPath.active.data != null ? AstarPath.active.data.gridGraph : null;
        if (gridGraph != null)
        {
            gridGraph.nodeSize = Mathf.Max(0.5f, _gridNodeSize);
        }

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
