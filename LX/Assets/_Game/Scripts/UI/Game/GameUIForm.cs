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
    /// 公司污染
    /// </summary>
    [SerializeField] private TMP_Text _txtCompanyPollution;
    /// <summary>
    /// 当前公司污染百分比
    /// </summary>
    private float _companyPollutionPercentage;
    /// <summary>
    /// 当前公司污染值
    /// </summary>
    private float _currentCompanyPollution;
    /// <summary>
    /// 公司污染最大值
    /// </summary>
    private float _companyPollution;
    /// <summary>
    /// 公司污染伤害值
    /// </summary>
    private float _companyPollutionDamagePerPoint;
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
        GameFramework.DataTable.IDataTable<DRBattleData> dRBattleDatas = GameEntry.DataTable.GetDataTable<DRBattleData>();
        DRBattleData dRBattleData = dRBattleDatas.GetDataRow(1);
        GameEntry.DataBinding.Set<int>("Money", dRBattleData.Money);
        GameEntry.DataBinding.Set<int>("Conscience", dRBattleData.Conscience);
        GameEntry.DataBinding.Set<int>("CompanyConscience", dRBattleData.CompanyConscience);
        GameEntry.DataBinding.Set<int>("CompanyPollution", 0);
        _currentCompanyPollution = 0;
        _companyPollutionPercentage = 0;
        _companyPollution = dRBattleData.CompanyPollution;
        _companyPollutionDamagePerPoint = dRBattleData.CompanyPollutionDamagePerPoint;
    }

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        GameEntry.Event.Subscribe(PollutionAttackEventArgs.EventId, OnPollutionAttackEvent);

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
        GameEntry.DataBinding.Bind<int>(
              "CompanyPollution",
              0,
              v => 
              {
                  
                  _txtCompanyPollution.SetText("{0}%", v);
                  _sliderCompanyPollution.value = v / _companyPollution;
              },
              this
          );
    }

    private void OnPollutionAttackEvent(object sender, GameEventArgs e)
    {
        PollutionAttackEventArgs ne = e as PollutionAttackEventArgs;
        object[] os = ne.UserData as object[];
        int attack = (int)os[0];

        BindableProperty<int> bp = GameEntry.DataBinding.Get<int>("CompanyPollution");
        bp.Value = attack;
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        GameEntry.DataBinding.UnbindAll(this);
        GameEntry.Event.Unsubscribe(PollutionAttackEventArgs.EventId, OnPollutionAttackEvent);
    }
}
