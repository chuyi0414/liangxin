using CYFramework;
using CYFramework.Core.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadUI : UIPanel
{
    [SerializeField] private Button _btnStartGame;

    private void Start()
    {
        _btnStartGame.onClick.AddListener(OnStartGameClicked);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _btnStartGame.onClick.RemoveAllListeners();
    }

    private void OnStartGameClicked()
    {
        //CY.Procedure.Change("Battle");
        CY.UI.Open<MainUI>();
    }
}