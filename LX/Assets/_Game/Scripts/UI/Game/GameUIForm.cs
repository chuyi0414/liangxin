using GameFramework.Event;
using QFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class GameUIForm : UIFormLogic
{
    /// <summary>
    /// 暂停按钮
    /// </summary>
    [SerializeField]private Button _btnPause;

    /// <summary>资金文本</summary>
    [SerializeField] private TMP_Text _txtMoney;
    /// <summary>良心文本</summary>
    [SerializeField] private TMP_Text _txtConscience;
    /// <summary>黑心文本</summary>
    [SerializeField] private TMP_Text _txtBlackHeart;
    /// <summary>
    /// 公司良心
    /// </summary>
    [SerializeField] private TMP_Text _txtCompanyConscience;
    /// <summary>
    /// 当前公司良心最大值
    /// </summary>
    private float _companyConscience;
    /// <summary>
    /// 公司良心伤害阈值
    /// </summary>
    private float _companyConscienceDamagePerPoint;
    /// <summary>
    /// 当前公司良心伤害
    /// </summary>
    private float _currentCompanyConscienceDamagePerPoint;
    /// <summary>
    /// 公司污染
    /// </summary>
    [SerializeField] private TMP_Text _txtCompanyPollution;
    /// <summary>
    /// 当前公司污染百分比
    /// </summary>
    private float _companyPollutionPercentage;
    /// <summary>
    /// 公司污染最大值
    /// </summary>
    private float _companyPollution;
    /// <summary>
    /// 公司污染伤害阈值
    /// </summary>
    private float _companyPollutionDamagePerPoint;
    /// <summary>
    /// 当前公司污染值
    /// </summary>
    private float _currentCompanyPollutionDamagePerPoint;
    /// <summary>
    /// 公司滑动条
    /// </summary>
    [SerializeField] private Slider _sliderCompanyPollution;
    /// <summary>
    /// 波次倒计时
    /// </summary>
    [SerializeField] private TMP_Text _txtWaveCountdown;
    /// <summary>
    /// 波次阶段
    /// </summary>
    [SerializeField] private TMP_Text _txtStage;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);

        _btnPause.onClick.AddListener(() =>
        {
            GameFramework.Procedure.ProcedureBase currentProcedure = GameEntry.Procedure.CurrentProcedure;
            currentProcedure.ChangeState<MainProcedure>(currentProcedure.procedureOwner);
        });
    }

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        GameEntry.Event.Subscribe(PollutionAttackEventArgs.EventId, OnPollutionAttackEvent);

        DRBattleData dRBattleData = GameEntry.StartGame.DRBattleDatas.GetDataRow(1);
        GameEntry.DataBinding.Set<int>("Money", dRBattleData.Money);
        GameEntry.DataBinding.Set<int>("Conscience", dRBattleData.Conscience);
        GameEntry.DataBinding.Set<int>("CompanyConscience", dRBattleData.CompanyConscience);
        _currentCompanyConscienceDamagePerPoint = 0;
        _companyConscience = dRBattleData.CompanyConscience;
        _companyConscienceDamagePerPoint = dRBattleData.CompanyConscienceDamagePerPoint;
        GameEntry.DataBinding.Set<float>("CompanyPollution", 0);
        _companyPollutionPercentage = 0;
        _companyPollution = dRBattleData.CompanyPollution;
        _companyPollutionDamagePerPoint = dRBattleData.CompanyPollutionDamagePerPoint;
        _currentCompanyPollutionDamagePerPoint = 0;

        GameEntry.DataBinding.Bind<int>(
              "Money",
              0,
              v => _txtMoney.SetText("{0}", v),
              this
          );
        GameEntry.DataBinding.Bind<int>(
              "Conscience",
              0,
              v => _txtConscience.SetText("{0}", v),
              this
          );
        GameEntry.DataBinding.Bind<int>(
              "CompanyConscience",
              0,
              v => _txtCompanyConscience.SetText("{0}", v),
              this
          );
        GameEntry.DataBinding.Bind<float>(
              "CompanyPollution",
              0,
              v => 
              {
                  float vPercentage = v / _companyPollution;
                  _txtCompanyPollution.SetText("{0}%", vPercentage * 100);
                  _sliderCompanyPollution.value = vPercentage;
              },
              this
          );
    }

    private void OnPollutionAttackEvent(object sender, GameEventArgs e)
    {
        PollutionAttackEventArgs ne = e as PollutionAttackEventArgs;
        object[] os = ne.UserData as object[];
        int attack = (int)os[0];

        _currentCompanyPollutionDamagePerPoint += attack;
        if(_currentCompanyPollutionDamagePerPoint >= _companyPollutionDamagePerPoint)
        {
            _currentCompanyPollutionDamagePerPoint -= _companyPollutionDamagePerPoint;
            _companyPollutionPercentage++;
            if(_companyPollutionPercentage >= _companyPollution)
            {
                _companyPollutionPercentage -= _companyPollution;
                //发送污染事件
            }
        }

        BindableProperty<float> bp = GameEntry.DataBinding.Get<float>("CompanyPollution");
        bp.Value = _companyPollutionPercentage;

        _currentCompanyConscienceDamagePerPoint++;
        if(_currentCompanyConscienceDamagePerPoint >= _companyConscienceDamagePerPoint)
        {
            _currentCompanyConscienceDamagePerPoint -= _companyConscienceDamagePerPoint;
            _companyConscience -= 1;
            if(_companyConscience <= 0)
            {
                _companyConscience = 0;
                //发送公司破产事件
            }
        }
        GameEntry.DataBinding.Get<int>("CompanyConscience").Value = (int)_companyConscience;
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        GameEntry.DataBinding.UnbindAll(this);
        GameEntry.Event.Unsubscribe(PollutionAttackEventArgs.EventId, OnPollutionAttackEvent);
    }
}
