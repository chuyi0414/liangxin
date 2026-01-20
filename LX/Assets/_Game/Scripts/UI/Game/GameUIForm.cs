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
    }

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        GameEntry.DataBinding.Bind<int>(
              "Money",
              0,
              v => _txtMoney.SetText("{0}", v),
              this
          );
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        GameEntry.DataBinding.UnbindAll(this);
    }
}
