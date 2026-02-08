using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using PrimeTween;
using TMPro;
using GameFramework.ObjectPool;

/// <summary>
/// 招募ui
/// </summary>
public class RecruitUIForm : UIFormLogic
{
    /// <summary>
    /// 招募go
    /// </summary>
    [SerializeField]private GameObject _goRecruitUI;
    /// <summary>招募UI的RectTransform组件缓存</summary>
    private RectTransform _recruitRect;
    /// <summary>
    /// 招募go现在的展开/收起状态
    /// </summary>
    private bool _isOpened = false;
    /// <summary>
    /// 招募go 展开/收起
    /// </summary>
    [SerializeField] private Button _btnSwitch;
    /// <summary>
    /// 人才库刷新数量
    /// </summary>
    [SerializeField]private TextMeshProUGUI _txtTalentPoolDisplayCount;
    /// <summary>
    /// 人才库刷新数量
    /// </summary>
    private int _talentPoolDisplayCount;
    /// <summary>
    /// 人才库刷新价格
    /// </summary>
    [SerializeField] private TextMeshProUGUI _txtTalentPoolRefreshPrice;
    /// <summary>
    /// 人才库刷新价格
    /// </summary>
    private int _talentPoolRefreshPrice;
    /// <summary>
    /// 人才
    /// </summary>
    [SerializeField]private GoTalents _goTalents;
    //当前显示人才卡片
    List<GoTalents> _activeTalents = new List<GoTalents>();
    /// <summary>
    /// 人才数据
    /// </summary>
    private DREmployee[] dREmployees;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        
        _recruitRect = _goRecruitUI.GetComponent<RectTransform>();
        _btnSwitch.onClick.AddListener(OnBtnSwitchClick);
        _isOpened = false;
        dREmployees = GameEntry.GameManager.DREmployees.GetAllDataRows();
        _activeTalents.Add(_goTalents);
        _goTalents.gameObject.SetActive(false);
        _goTalents.InitNameBinding(1);
        for (int i = 0; i < 14; i++)
        {
            GoTalents goTalents = Instantiate(_goTalents, _goTalents.transform.parent,false);
            goTalents.InitNameBinding(i + 2);
            _activeTalents.Add(goTalents);
        }
    }

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        DRBattleData dRBattleData = GameEntry.GameManager.DRBattleDatas.GetDataRow(1);
        _talentPoolDisplayCount = dRBattleData.TalentPoolDisplayCount;
        _talentPoolRefreshPrice = dRBattleData.TalentPoolRefreshPrice;
        TalentsRefresh();

        GameEntry.DataBinding.Bind<int>(
            "TalentPoolDisplayCount",
            dRBattleData.TalentPoolDisplayCount,
            v => _txtTalentPoolDisplayCount.SetText("{0}", v),
            this
            );
        GameEntry.DataBinding.Bind<int>(
            "TalentPoolRefreshPrice",
            dRBattleData.TalentPoolRefreshPrice,
            v => _txtTalentPoolRefreshPrice.SetText("{0}", v),
            this
            );
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);

        if (_isOpened)
        {
            SwitchTween(200f);
        }
        GameEntry.DataBinding.UnbindAll(this);
    }

    protected override void OnRecycle()
    {
        base.OnRecycle();
        
    }

    private void TalentsRefresh()
    {
        for(int i = 0;i< _activeTalents.Count; i++ )
        {
            
            if (i< _talentPoolDisplayCount)
            {
                DREmployee dREmployee = dREmployees[UnityEngine.Random.Range(0, dREmployees.Length)];
                _activeTalents[i].gameObject.SetActive(true);
                _activeTalents[i].Id = dREmployee.Id;

                _activeTalents[i].SetName(dREmployee.Name);
                continue;
            }
            _activeTalents[i].gameObject.SetActive(false);
        }
    }

    private void SwitchTween(float value)
    {
        Tween.UIAnchoredPositionX(
        _recruitRect,
        endValue: value,
        duration: 0.3f,
        ease: Ease.OutCubic
        );
    }

    private void OnBtnSwitchClick()
    {
        if(_isOpened)
        {
            _isOpened = false;
            SwitchTween(200);
        }
        else
        {
            _isOpened = true;
            SwitchTween(-250);
        }
    }
}
