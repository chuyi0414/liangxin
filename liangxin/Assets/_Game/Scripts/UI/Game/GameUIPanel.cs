using CYFramework;
using CYFramework.Core.Timer;
using CYFramework.Core.UI;
using PrimeTween; // PrimeTween 动画系统引用
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

    [Header("人才库")]
    /// <summary>
    /// 人才库物体
    /// </summary>
    [SerializeField] private GameObject _goTalentPool;
    /// <summary>
    /// 人才库按钮（显示/隐藏）
    /// </summary>
    [SerializeField] private Button _btnShowHide;
    /// <summary>人才库 RectTransform 缓存。</summary>
    private RectTransform _talentPoolRectTransform; // 人才库 RectTransform 缓存
    /// <summary>人才库是否展开。</summary>
    private bool _isTalentPoolExpanded; // 人才库展开状态标记
    /// <summary>人才库展开目标本地坐标。</summary>
    private static readonly Vector3 TalentPoolExpandedLocalPosition = new Vector3(0f, 0f, 0f); // 人才库展开位置
    /// <summary>人才库收起目标本地坐标。</summary>
    private static readonly Vector3 TalentPoolCollapsedLocalPosition = new Vector3(500f, 0f, 0f); // 人才库收起位置
    /// <summary>人才库移动动画时长（秒）。</summary>
    [SerializeField] private float _talentPoolMoveDuration = 0.3f; // 人才库移动时长
    /// <summary>人才库移动 Tween 句柄。</summary>
    private Tween _talentPoolTween; // 人才库移动 Tween 句柄
    /// <summary>人才库 Tween 起点缓存。</summary>
    private Vector3 _talentPoolTweenFrom; // 人才库 Tween 起点缓存
    /// <summary>人才库 Tween 终点缓存。</summary>
    private Vector3 _talentPoolTweenTo; // 人才库 Tween 终点缓存

    protected override void OnBindUI()
    {
        base.OnBindUI();
        if (_btnPause != null)
        {
            _btnPause.onClick.AddListener(OnBtnPauseClick);
        }
        if (_btnShowHide != null)
        {
            _btnShowHide.onClick.AddListener(OnBtnShowHideClick); // 绑定人才库显示/隐藏按钮事件
        }
        if (_goTalentPool != null)
        {
            _talentPoolRectTransform = _goTalentPool.GetComponent<RectTransform>(); // 缓存人才库 RectTransform
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
    /// 人才库显示/隐藏按钮点击回调。
    /// </summary>
    private void OnBtnShowHideClick() // 人才库按钮点击入口
    {
        if (!TryGetTalentPoolRectTransform(out var rectTransform))
        {
            return; // 未获取到 RectTransform 时直接退出
        }

        _isTalentPoolExpanded = !_isTalentPoolExpanded; // 切换人才库展开状态
        var targetPosition = _isTalentPoolExpanded ? TalentPoolExpandedLocalPosition : TalentPoolCollapsedLocalPosition; // 计算目标位置
        PlayTalentPoolMoveTween(rectTransform, targetPosition); // 播放人才库移动动画
    }

    /// <summary>
    /// 获取并缓存人才库 RectTransform。
    /// </summary>
    /// <param name="rectTransform">输出 RectTransform。</param>
    /// <returns>是否获取成功。</returns>
    private bool TryGetTalentPoolRectTransform(out RectTransform rectTransform) // 人才库 RectTransform 获取入口
    {
        rectTransform = _talentPoolRectTransform; // 优先使用缓存引用
        if (rectTransform != null)
        {
            return true; // 缓存可用时直接返回成功
        }

        if (_goTalentPool == null)
        {
            return false; // 物体为空时返回失败
        }

        rectTransform = _goTalentPool.GetComponent<RectTransform>(); // 获取人才库 RectTransform
        _talentPoolRectTransform = rectTransform; // 缓存 RectTransform 引用
        return rectTransform != null; // 返回是否获取成功
    }

    /// <summary>
    /// 播放人才库移动动画。
    /// </summary>
    /// <param name="rectTransform">人才库 RectTransform。</param>
    /// <param name="targetLocalPosition">目标本地坐标。</param>
    private void PlayTalentPoolMoveTween(RectTransform rectTransform, Vector3 targetLocalPosition) // 人才库移动动画入口
    {
        if (rectTransform == null)
        {
            return; // RectTransform 为空时直接退出
        }

        _talentPoolRectTransform = rectTransform; // 缓存 RectTransform 引用
        StopTalentPoolTween(); // 停止旧的移动动画
        _talentPoolTweenFrom = rectTransform.anchoredPosition3D; // 记录动画起点位置
        _talentPoolTweenTo = targetLocalPosition; // 记录动画终点位置
        var duration = _talentPoolMoveDuration; // 读取动画时长
        if (duration <= 0f)
        {
            rectTransform.anchoredPosition3D = targetLocalPosition; // 时长无效时直接设置位置
            return; // 直接结束
        }

        _talentPoolTween = Tween.Custom<GameUIPanel>(this, 0f, 1f, duration, (self, t) => // 使用 PrimeTween 播放自定义位移动画
        {
            var targetRect = self._talentPoolRectTransform; // 获取当前缓存 RectTransform
            if (targetRect == null)
            {
                return; // RectTransform 为空时直接退出
            }

            var clamped = Mathf.Clamp01(t); // 限制进度范围
            var eased = 1f - Mathf.Pow(1f - clamped, 3f); // 计算缓出曲线进度
            var nextPosition = Vector3.Lerp(self._talentPoolTweenFrom, self._talentPoolTweenTo, eased); // 计算插值位置
            targetRect.anchoredPosition3D = nextPosition; // 写入人才库位置
        });
    }

    /// <summary>
    /// 停止人才库移动动画。
    /// </summary>
    private void StopTalentPoolTween() // 人才库移动动画停止入口
    {
        if (_talentPoolTween.isAlive)
        {
            _talentPoolTween.Stop(); // 停止正在播放的动画
        }
    }

    /// <summary>
    /// 重置人才库为收起状态。
    /// </summary>
    private void ResetTalentPoolToHidden() // 人才库重置入口
    {
        if (!TryGetTalentPoolRectTransform(out var rectTransform))
        {
            return; // 无法获取 RectTransform 时直接退出
        }

        StopTalentPoolTween(); // 停止移动动画
        rectTransform.anchoredPosition3D = TalentPoolCollapsedLocalPosition; // 重置到收起位置
        _isTalentPoolExpanded = false; // 重置展开状态
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

    /// <summary>
    /// 面板隐藏时重置人才库位置。
    /// </summary>
    protected override void OnHide() // 面板隐藏回调入口
    {
        base.OnHide(); // 调用父类隐藏回调
        ResetTalentPoolToHidden(); // 隐藏时重置人才库位置
    }

    protected override void OnUnbindUI()
    {
        base.OnUnbindUI();
        if (_btnPause != null)
        {
            _btnPause.onClick.RemoveListener(OnBtnPauseClick);
        }
        if (_btnShowHide != null)
        {
            _btnShowHide.onClick.RemoveListener(OnBtnShowHideClick); // 解绑人才库显示/隐藏按钮事件
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
        CY.Event.Subscribe<ConscienceChangedEvent>(OnConscienceChanged, this); // 订阅良心变化事件
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
    /// 良心变化事件回调。
    /// </summary>
    /// <param name="evt">良心变化事件。</param>
    private void OnConscienceChanged(ref ConscienceChangedEvent evt) // 良心事件回调入口
    {
        SetValueText(_txtConscience, evt.CurrentValue); // 刷新良心显示
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
        SetValueText(_txtConscience, manager.ConscienceCurrent); // 刷新良心显示
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
