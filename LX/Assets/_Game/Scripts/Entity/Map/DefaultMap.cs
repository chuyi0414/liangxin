using GameFramework.DataTable;
using Pathfinding;
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
    /// 主角出生位置
    /// </summary>
    [SerializeField]
    public Transform _protagonistTransform;
    /// <summary>
    /// 公司出生位置
    /// </summary>
    [SerializeField]
    public Transform _companyTransform;
    /// <summary>
    /// 员工生成点
    /// </summary>
    [SerializeField]
    public Transform _employeeGenericPoint;

    /// <summary>
    /// A*扫描协程变量，避免重复启动扫描
    /// </summary>
    private Coroutine _rescanCoroutine;

    /// <summary>
    /// A*网格节点大小（越大越省性能，但寻路精度更低）
    /// </summary>
    private float _gridNodeSize = EnemyAIConfig.GridNodeSize;

    /// <summary>
    /// 实体初始化（当前仅走基类流程）
    /// </summary>
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        
    }

    /// <summary>
    /// 实体显示时创建主角与公司，并启动 A* 扫描
    /// </summary>
    protected override void OnShow(object userData)
    {
        base.OnShow(userData);

        GameEntry.GameManager._defaultMap = this;
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
        // 创建公司实体
        GameEntry.Entity.ShowEntity<CompanyEntity>(
            GameEntry.EntityIdPool.Acquire()
            , "Prefabs/Entity/Company/CompanyEntity"
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

    /// <summary>
    /// 重新扫描 A* 网格（延后一帧以确保场景就绪）
    /// </summary>
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


    /// <summary>
    /// 实体回收时停止扫描协程
    /// </summary>
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
