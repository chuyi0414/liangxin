using CYFramework;
using CYFramework.Core.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

[UIPrefab("Prefabs/UI/Menu/Main/MainUI")]
public class MainUI : UIPanel
{
    /// <summary>
    /// 开始游戏
    /// </summary>
    [SerializeField] private Button _btnStartGame;

    protected override void OnBindUI()
    {
        base.OnBindUI();
        _btnStartGame.onClick.AddListener(OnStartGameClicked);
    }

    protected override void OnUnbindUI()
    {
        base.OnUnbindUI();

    }

    /// <summary>
    /// 开始游戏
    /// </summary>
    private void OnStartGameClicked()
    {
        CY.LogInfo("按下了按钮");
        
        CY.Procedure.ChangeProcedure<BattleProcedure>();
    }
}
