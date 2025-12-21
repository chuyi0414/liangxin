using CYFramework;
using CYFramework.Core.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[UIPrefab("Prefabs/UI/Main/MainUIPanel")]
public class MainUIPanel : UIPanel
{
    /// <summary>
    /// 开始游戏
    /// </summary>
    [SerializeField]
    private Button _btnStartGame;
    protected override void OnBindUI()
    {
        base.OnBindUI();
        _btnStartGame.onClick.AddListener(OnBtnStartGameClick);
    }

    /// <summary>
    /// 开始游戏
    /// </summary>
    private void OnBtnStartGameClick()
    {
        CY.Procedure.ChangeProcedure<GameProcedure>();
    }

    protected override void OnUnbindUI()
    {
        base.OnUnbindUI();

    }
}
