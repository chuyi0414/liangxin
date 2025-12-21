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
            return;
        }

        SetValueText(_txtMoney, data.Money);
        SetValueText(_txtConscience, data.Conscience);
        SetValueText(_txtBlackHeart, data.BlackHeart);
    }

    private static void SetValueText(TMP_Text target, int value)
    {
        if (target == null) return;
        target.SetText("{0}", value);
    }

    private static void SetValueText(TMP_Text target, string value)
    {
        if (target == null) return;
        target.SetText(value);
    }
}
