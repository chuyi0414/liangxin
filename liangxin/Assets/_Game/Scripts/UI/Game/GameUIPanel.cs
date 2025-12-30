using CYFramework;
using CYFramework.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[UIPrefab("Prefabs/UI/Game/GameUIPanel")]
public class GameUIPanel : UIPanel
{
    /// <summary>
    /// 暂停按钮
    /// </summary>
    [SerializeField] private Button _btnPause;

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
    [SerializeField] private Scrollbar _scrollbarCompanyPollution;
    /// <summary>
    /// 当前污染度
    /// </summary>
    [SerializeField] private float _floatCompanyPollution;

    protected override void OnBindUI()
    {
        base.OnBindUI();
        if (_btnPause != null)
        {
            _btnPause.onClick.AddListener(OnBtnPauseClick);
        }
    }

    /// <summary>
    /// 暂停按钮
    /// </summary>
    private void OnBtnPauseClick()
    {
        CY.Procedure.ChangeProcedure<MainProcedure>();
    }

    /// <summary>
    /// 面板打开时刷新显示
    /// </summary>
    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        _floatCompanyPollution = 0;
        RefreshBattleData();
    }

    /// <summary>
    /// 面板刷新时同步显示
    /// </summary>
    protected override void OnRefresh(object userData)
    {
        base.OnRefresh(userData);
        RefreshBattleData();
    }

    protected override void OnUnbindUI()
    {
        base.OnUnbindUI();
        if (_btnPause != null)
        {
            _btnPause.onClick.RemoveListener(OnBtnPauseClick);
        }
    }

    /// <summary>
    /// 读取 BattleDataManager 中的缓存数据并刷新文本
    /// </summary>
    private void RefreshBattleData()
    {
        var manager = CY.BattleDataManager;
        var data = manager != null ? manager.BattleData : null;

        if (data == null)
        {
            SetValueText(_txtMoney, "--");
            SetValueText(_txtConscience, "--");
            SetValueText(_txtBlackHeart, "--");
            SetValueText(_txtCompanyConscience, "--");
            SetValueText(_txtCompanyPollution, "--");
            SetCompanyPollutionScrollbar(0);
            return;
        }

        SetValueText(_txtMoney, data.Money);
        SetValueText(_txtConscience, data.Conscience);
        SetValueText(_txtBlackHeart, data.BlackHeart);
        SetValueText(_txtCompanyConscience, data.CompanyConscience);
        var pollutionPercent = ToPercent(Mathf.RoundToInt(_floatCompanyPollution), data.CompanyPollution);
        SetValueText(_txtCompanyPollution, pollutionPercent, true);
        SetCompanyPollutionScrollbar(pollutionPercent);
        
    }

    private static void SetValueText(TMP_Text target, int value)
    {
        if (target == null) return;
        target.SetText("{0}", value);
    }

    private static void SetValueText(TMP_Text target, int value, bool suffixPercent)
    {
        if (target == null) return;
        if (suffixPercent)
        {
            target.SetText("{0}%", value);
            return;
        }

        target.SetText("{0}", value);
    }

    private static void SetValueText(TMP_Text target, string value)
    {
        if (target == null) return;
        target.SetText(value);
    }

    /// <summary>
    /// 将数值转换为 0-100 的百分比并做上下限保护。
    /// </summary>
    private static int ToPercent(int value, int max)
    {
        if (max <= 0) return 0;
        if (value <= 0) return 0;
        if (value >= max) return 100;
        return value * 100 / max;
    }

    /// <summary>
    /// 同步污染百分比到滚动条（0-1）。
    /// </summary>
    private void SetCompanyPollutionScrollbar(int percent)
    {
        if (_scrollbarCompanyPollution == null) return;
        if (percent <= 0)
        {
            _scrollbarCompanyPollution.size = 0f;
            return;
        }

        _scrollbarCompanyPollution.size = percent >= 100 ? 1f : percent / 100f;
    }
}
