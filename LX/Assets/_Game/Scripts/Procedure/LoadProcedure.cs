using GameFramework.DataTable;
using GameFramework.Event;
using GameFramework.Fsm;
using GameFramework.Procedure;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;
using UnityGameFramework.Runtime;

/// <summary>
/// 加载流程
/// </summary>
public class LoadProcedure : ProcedureBase
{
    //总加载数量
    private int _loadNumber = 1;
    //已经加载的数量
    private int _accomplishLoadNumber = 0;


    //LoadUIForm表Id
    private int _loadUIFormId;

    //DRBattleData
    private DataTableBase _dRBattleData;

    protected override void OnInit(IFsm<IProcedureManager> procedureOwner)
    {
        base.OnInit(procedureOwner);
        

    }
    protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
    {
        base.OnEnter(procedureOwner);

        GameEntry.Event.Subscribe(LoadDataTableSuccessEventArgs.EventId,OnLoadDataTableSuccess);
        GameEntry.Event.Subscribe(LoadDataTableFailureEventArgs.EventId, OnLoadDataTableFailure);

        if (GameEntry.DataTable.HasDataTable<DRBattleData>())
        {
            _dRBattleData = (DataTableBase)GameEntry.DataTable.GetDataTable<DRBattleData>();
        }
        else
        {
            _dRBattleData = (DataTableBase)GameEntry.DataTable.CreateDataTable<DRBattleData>();
        }

        _dRBattleData.ReadData("DataTables/Game/BattleData"
            ,new object[]
            {
                this,
                "BattleData"
            });
    }

    protected override void OnUpdate(IFsm<IProcedureManager> procedureOwner, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

    }

    protected override void OnLeave(IFsm<IProcedureManager> procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);

        GameEntry.Event.Unsubscribe(LoadDataTableSuccessEventArgs.EventId, OnLoadDataTableSuccess);
        GameEntry.Event.Unsubscribe(LoadDataTableFailureEventArgs.EventId, OnLoadDataTableFailure);

        if(_loadUIFormId != 0)
        GameEntry.UI.CloseUIForm(_loadUIFormId);
    }

    protected override void OnDestroy(IFsm<IProcedureManager> procedureOwner)
    {
        base.OnDestroy(procedureOwner);

    }

    private void OnLoadDataTableFailure(object sender, GameEventArgs e)
    {

    }

    private void OnLoadDataTableSuccess(object sender, GameEventArgs e)
    {
        LoadDataTableSuccessEventArgs ne = e as LoadDataTableSuccessEventArgs;
        
        object[] os = ne.UserData as object[];
        if (os[0] != this)
            return;

        if (os[1].Equals("BattleData"))
        {
            IDataTable<DRBattleData> dRBattleDatas = GameEntry.DataTable.GetDataTable<DRBattleData>();
            DRBattleData battleData = dRBattleDatas.GetDataRow(1);
        }

        _accomplishLoadNumber++;
        if(_accomplishLoadNumber == _loadNumber)
        {
            _loadUIFormId = GameEntry.UI.OpenUIForm("UI/Load/LoadUIForm", "Normal");
        }
    }
}
