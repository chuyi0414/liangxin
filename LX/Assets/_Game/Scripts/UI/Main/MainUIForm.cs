using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class MainUIForm : UIFormLogic
{
    /// <summary>
    /// Ω¯»Î”Œœ∑
    /// </summary>
    [SerializeField]
    private Button _btnStartGame;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnStartGame.onClick.AddListener(OnBtnStartGameClick);
    }

    private void OnBtnStartGameClick()
    {
        GameFramework.Procedure.ProcedureBase currentProcedure = GameEntry.Procedure.CurrentProcedure;
        currentProcedure.ChangeState<GameProcedure>(currentProcedure.procedureOwner);
    }
}
