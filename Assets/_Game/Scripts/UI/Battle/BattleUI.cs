using CYFramework;
using CYFramework.Core.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[UIPrefab("Prefabs/UI/Battle/BattleUI")]
public class BattleUI : UIPanel
{
    /// <summary>
    /// 退出游戏按钮
    /// </summary>
    [SerializeField]
    private Button _BtnExitBattle;

    protected override void OnBindUI()
    {
        base.OnBindUI();
        _BtnExitBattle.onClick.AddListener(OnExitBattleClicked);
    }

    protected override void OnUnbindUI()
    {
        base.OnUnbindUI();

    }

    /// <summary>
    /// 退出战斗
    /// </summary>
    private void OnExitBattleClicked()
    {
        //返回菜单流程
        CY.Procedure.ChangeProcedure<MenuProcedure>();
    }
}
