using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class LoadUIForm : UIFormLogic
{
    /// <summary>
    /// 开始游戏按钮
    /// </summary>
    [SerializeField]
    private Button _btnPlay;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnPlay.onClick.AddListener(OnBtnPlayClick);
    }

    private void OnBtnPlayClick()
    {
        GameFramework.Procedure.ProcedureBase currentProcedure = GameEntry.Procedure.CurrentProcedure;
        currentProcedure.ChangeState<MainProcedure>(currentProcedure.procedureOwner);
    }
}
