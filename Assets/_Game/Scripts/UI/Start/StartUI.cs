using CYFramework;
using CYFramework.Core.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartUI : UIPanel
{
    [SerializeField] private Button _btnStartGame;  // Inspector 拖拽

    protected override void OnBindUI()
    {
        base.OnBindUI();
        _btnStartGame.onClick.AddListener(OnStartGameClicked);
    }

    protected override void OnUnbindUI()
    {
        base.OnUnbindUI();
        _btnStartGame.onClick.RemoveListener(OnStartGameClicked);
    }

    private void OnStartGameClicked()
    {
        CY.Procedure.Change("Battle");
    }
}