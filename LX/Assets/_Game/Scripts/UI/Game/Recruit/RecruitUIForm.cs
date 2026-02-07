using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using PrimeTween;

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

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        
        _recruitRect = _goRecruitUI.GetComponent<RectTransform>();
        _btnSwitch.onClick.AddListener(OnBtnSwitchClick);
        _isOpened = false;
    }

    protected override void OnRecycle()
    {
        base.OnRecycle();

        if(_isOpened)
        {
            SwitchTween(200f);
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
