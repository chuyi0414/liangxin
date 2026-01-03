using CYFramework;
using CYFramework.Core.Timer;
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
    [SerializeField] private Slider _sliderCompanyPollution;
    /// <summary>
    /// 当前污染度
    /// </summary>
    [SerializeField] private float _floatCompanyPollution;
    /// <summary>
    /// 波次倒计时
    /// </summary>
    [SerializeField] private TMP_Text _txtWaveCountdown;
    /// <summary>
    /// 波次阶段
    /// </summary>
    [SerializeField] private TMP_Text _txtStage;
    /// <summary>波次 UI 刷新计时器。</summary>
    private Timer _waveUiTimer;

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
        StartWaveUiTimer();
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

        StopWaveUiTimer();
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
        if (_sliderCompanyPollution == null) return;
        if (percent <= 0)
        {
            _sliderCompanyPollution.value = 0f;
            return;
        }

        _sliderCompanyPollution.value = percent >= 100 ? 1f : percent / 100f;
    }

    /// <summary>
    /// 启动波次 UI 刷新计时器。
    /// </summary>
    private void StartWaveUiTimer()
    {
        StopWaveUiTimer();
        _waveUiTimer = CY.Timer.Loop(0.2f, UpdateWaveUi);
        UpdateWaveUi();
    }

    /// <summary>
    /// 停止波次 UI 刷新计时器。
    /// </summary>
    private void StopWaveUiTimer()
    {
        if (_waveUiTimer == null)
        {
            return;
        }

        _waveUiTimer.Stop();
        _waveUiTimer = null;
    }

    /// <summary>
    /// 刷新波次倒计时与阶段显示。
    /// </summary>
    private void UpdateWaveUi()
    {
        var waveManager = CY.Wave;
        if (waveManager == null)
        {
            SetWaveStageText("--");
            SetWaveCountdownText("--:--");
            return;
        }

        if (!waveManager.TryGetMainWaveStatus(out var waveId, out var stage, out var remaining))
        {
            SetWaveStageText("--");
            SetWaveCountdownText("--:--");
            return;
        }

        SetWaveStageText(waveId, stage);
        var seconds = Mathf.CeilToInt(remaining);
        SetWaveCountdownText(seconds);
    }

    /// <summary>
    /// 设置波次阶段文本。
    /// </summary>
    private void SetWaveStageText(int waveId, WaveStage stage)
    {
        if (_txtStage == null)
        {
            return;
        }

        if (stage == WaveStage.Prepare)
        {
            _txtStage.SetText("第{0}波 准备中", waveId);
            return;
        }

        if (stage == WaveStage.Spawn)
        {
            _txtStage.SetText("第{0}波 刷怪中", waveId);
            return;
        }

        _txtStage.SetText("--");
    }

    /// <summary>
    /// 设置波次阶段文本（无数据）。
    /// </summary>
    private void SetWaveStageText(string text)
    {
        if (_txtStage == null)
        {
            return;
        }

        _txtStage.SetText(text);
    }

    /// <summary>
    /// 设置波次倒计时文本。
    /// </summary>
    private void SetWaveCountdownText(int seconds)
    {
        if (_txtWaveCountdown == null)
        {
            return;
        }

        if (seconds < 0)
        {
            seconds = 0; // 负数保护
        }

        var minutes = seconds / 60; // 计算分钟
        var remainSeconds = seconds - minutes * 60; // 计算剩余秒
        _txtWaveCountdown.SetText("{0:00}:{1:00}", minutes, remainSeconds); // 按 mm:ss 输出
    }

    /// <summary>
    /// 设置波次倒计时文本（无数据）。
    /// </summary>
    private void SetWaveCountdownText(string text)
    {
        if (_txtWaveCountdown == null)
        {
            return;
        }

        _txtWaveCountdown.SetText(text);
    }
}
