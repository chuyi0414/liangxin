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
    /// 波次倒计时
    /// </summary>
    [SerializeField] private TMP_Text _txtWaveCountdown;
    /// <summary>
    /// 波次阶段
    /// </summary>
    [SerializeField] private TMP_Text _txtStage;
    /// <summary>波次 UI 刷新计时器。</summary>
    private Timer _waveUiTimer;
    /// <summary>是否已订阅战斗数据事件。</summary>
    private bool _battleDataEventsSubscribed; // 战斗数据事件订阅标记

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
        EnsureBattleDataSubscribed(); // 确保订阅战斗数据事件
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

        UnsubscribeBattleDataEvents(); // 取消战斗数据事件订阅
        StopWaveUiTimer();
    }

    /// <summary>
    /// 确保订阅战斗数据事件。
    /// </summary>
    private void EnsureBattleDataSubscribed() // 战斗数据事件订阅入口
    {
        if (_battleDataEventsSubscribed)
        {
            return; // 已订阅时直接返回
        }

        CY.Event.Subscribe<CompanyConscienceChangedEvent>(OnCompanyConscienceChanged, this); // 订阅公司良心变化事件
        CY.Event.Subscribe<CompanyPollutionChangedEvent>(OnCompanyPollutionChanged, this); // 订阅公司污染变化事件
        CY.Event.Subscribe<MoneyChangedEvent>(OnMoneyChanged, this); // 订阅资金变化事件
        CY.Event.Subscribe<BlackHeartChangedEvent>(OnBlackHeartChanged, this); // 订阅黑心变化事件
        _battleDataEventsSubscribed = true; // 标记已订阅
    }

    /// <summary>
    /// 取消订阅战斗数据事件。
    /// </summary>
    private void UnsubscribeBattleDataEvents() // 战斗数据事件取消订阅入口
    {
        if (!_battleDataEventsSubscribed)
        {
            return; // 未订阅时直接返回
        }

        CY.Event.UnsubscribeAll(this); // 取消当前面板的事件订阅
        _battleDataEventsSubscribed = false; // 标记已取消订阅
    }

    /// <summary>
    /// 公司良心变化事件回调。
    /// </summary>
    /// <param name="evt">良心变化事件。</param>
    private void OnCompanyConscienceChanged(ref CompanyConscienceChangedEvent evt) // 良心事件回调入口
    {
        SetValueText(_txtCompanyConscience, evt.CurrentValue); // 刷新公司良心显示
    }

    /// <summary>
    /// 公司污染变化事件回调。
    /// </summary>
    /// <param name="evt">污染变化事件。</param>
    private void OnCompanyPollutionChanged(ref CompanyPollutionChangedEvent evt) // 污染事件回调入口
    {
        var percent = ToPercent(evt.CurrentValue, evt.ThresholdValue); // 计算污染百分比
        SetValueText(_txtCompanyPollution, percent, true); // 刷新公司污染显示
        SetCompanyPollutionScrollbar(percent); // 刷新污染滑动条
    }

    /// <summary>
    /// 资金变化事件回调。
    /// </summary>
    /// <param name="evt">资金变化事件。</param>
    private void OnMoneyChanged(ref MoneyChangedEvent evt) // 资金事件回调入口
    {
        SetValueText(_txtMoney, evt.CurrentValue); // 刷新资金显示
    }

    /// <summary>
    /// 黑心变化事件回调。
    /// </summary>
    /// <param name="evt">黑心变化事件。</param>
    private void OnBlackHeartChanged(ref BlackHeartChangedEvent evt) // 黑心事件回调入口
    {
        SetValueText(_txtBlackHeart, evt.CurrentValue); // 刷新黑心显示
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

        SetValueText(_txtMoney, manager.MoneyCurrent); // 刷新资金显示
        SetValueText(_txtConscience, data.Conscience);
        SetValueText(_txtBlackHeart, manager.BlackHeartCurrent); // 刷新黑心显示
        var companyConscience = manager.CompanyConscienceCurrent; // 读取公司良心当前值
        var companyPollution = manager.CompanyPollutionCurrent; // 读取公司污染当前值
        var companyPollutionMax = data.CompanyPollution; // 读取公司污染阈值

        SetValueText(_txtCompanyConscience, companyConscience); // 刷新公司良心显示
        var pollutionPercent = ToPercent(companyPollution, companyPollutionMax); // 计算污染百分比
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
