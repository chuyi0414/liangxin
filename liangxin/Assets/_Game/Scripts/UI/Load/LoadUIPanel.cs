using CYFramework;
using CYFramework.Core.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[UIPrefab("Prefabs/UI/Load/LoadUIPanel")]
public class LoadUIPanel : UIPanel
{
    /// <summary>
    /// 进入游戏
    /// </summary>
    [SerializeField]
    private Button _btnLoad;

    protected override void OnBindUI()
    {
        base.OnBindUI();
        _btnLoad.onClick.AddListener(OnBtnLoadClick);
    }

    protected override void OnUnbindUI()
    {
        base.OnUnbindUI();

    }

    /// <summary>
    /// 点击进入游戏按钮
    /// </summary>
    private void OnBtnLoadClick()
    {
        CY.Procedure.ChangeProcedure<MainProcedure>();
    }
}
