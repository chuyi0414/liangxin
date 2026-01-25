using GameFramework.DataTable;
using GameFramework.Fsm;
using GameFramework.Procedure;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// GameProcedure流程
/// </summary>
public class GameProcedure: ProcedureBase
{
    //MainUIFormId
    private int _gameUIFormId;

    protected override void OnInit(IFsm<IProcedureManager> procedureOwner)
    {
        base.OnInit(procedureOwner);

    }
    protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
    {
        base.OnEnter(procedureOwner);
        GameEntry.Entity.ShowEntity<DefaultMap>(GameEntry.EntityIdPool.Acquire(), "Entity/Map/Map1", "Environment");
        _gameUIFormId = GameEntry.UI.OpenUIForm("UI/Game/GameUIForm", "Normal");
    }

    protected override void OnUpdate(IFsm<IProcedureManager> procedureOwner, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

    }

    protected override void OnLeave(IFsm<IProcedureManager> procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
        GameEntry.UI.CloseUIForm(_gameUIFormId);
    }

    protected override void OnDestroy(IFsm<IProcedureManager> procedureOwner)
    {
        base.OnDestroy(procedureOwner);

    }
}
