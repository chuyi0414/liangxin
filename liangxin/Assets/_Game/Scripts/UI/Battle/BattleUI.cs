using CYFramework;
using CYFramework.Core.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI;
using TMPro;
using CYFramework.Infrastructure;

[UIPrefab("Prefabs/UI/Battle/BattleUI")]
public class BattleUI : UIPanel
{
    /// <summary>
    /// 退出游戏按钮
    /// </summary>
    [SerializeField]
    private Button _BtnExitBattle;

    [Header("Info")]
    /// <summary>
    /// 当前波次/阶段文本 (状态描述，如 "准备中")
    /// </summary>
    [SerializeField] private TextMeshProUGUI _tmpWaveStage;

    /// <summary>
    /// 当前波数 (纯数字，如 "1")
    /// </summary>
    [SerializeField] private TextMeshProUGUI _tmpWaveCount;

    /// <summary>
    /// 剩余时间文本
    /// </summary>
    [SerializeField] private TextMeshProUGUI _tmpRemainingTime;

    /// <summary>
    /// 阶段倒计时进度条
    /// </summary>
    [SerializeField] private Slider _sliderWaveProgress;

    /// <summary>
    /// 波次预告情报文本
    /// </summary>
    [SerializeField] private TextMeshProUGUI _tmpWavePreview;

    [Header("Resource Info")]
    [SerializeField] private TextMeshProUGUI _tmpGold;//玩家资金
    [SerializeField] private TextMeshProUGUI _tmpConscienceResource; // 玩家持有良心
    [SerializeField] private TextMeshProUGUI _tmpDarkHeart;           // 玩家持有黑心

    private float _totalDurationForCurrentPhase = 1f; // 用于计算进度条的 Slider.value

    // 资源显示缓存：事件可能在短时间内多次触发，缓存可避免重复刷新导致的额外 UI 开销。
    // 说明：这里用 int.MinValue 作为“未初始化”的哨兵值，确保首次刷新一定生效。
    private int _lastGold = int.MinValue;
    private int _lastConscienceResource = int.MinValue;
    private int _lastDarkHeart = int.MinValue;

    protected override void OnBindUI()
    {
        base.OnBindUI();
        if (_BtnExitBattle) _BtnExitBattle.onClick.AddListener(OnExitBattleClicked);
        CY.Log($"Stage: {_tmpWaveStage}, Count: {_tmpWaveCount}, Timer: {_tmpRemainingTime}, Slider: {_sliderWaveProgress}, Preview: {_tmpWavePreview}");
        
        // 初始隐藏情报文本
        if (_tmpWavePreview) _tmpWavePreview.gameObject.SetActive(false);

        // 资源 UI 刷新策略（重要性能点）：
        // - 旧实现：OnUpdate 每帧轮询并用字符串插值写 TMP.text，容易产生 GC 与不必要的 UI 重建。
        // - 新实现：DepartmentManager 在资源变更时派发 DepartmentResourceChangedEvent，BattleUI 订阅后事件驱动刷新（零 GC）。
        CY.Event.Subscribe<DepartmentResourceChangedEvent>(OnDepartmentResourceChanged, this);
        RefreshResourceFromDepartment();
        
        // 监听波次开始事件 (需要 WaveManager 通知)
        // 由于 WaveManager 目前没发事件，我们在 Update 里轮询检查 State 变化也是一种简易做法，
        // 或者直接让 WaveManager 提供 event。
        // 鉴于 OnUpdate 已经在跑了，且为了不动 WaveManager 架构，我们用简单的状态变动检测。
    }
    
    // 简单的状态追踪，用于触发一次性 UI 逻辑 (如显示 Preview)
    private WaveManager.WaveState _lastState = WaveManager.WaveState.None;
    private int _lastWaveIndex = -1;

    protected override void OnUnbindUI()
    {
        // 解绑按钮与事件订阅，避免面板反复打开时重复绑定/泄漏。
        if (_BtnExitBattle) _BtnExitBattle.onClick.RemoveListener(OnExitBattleClicked);
        CY.Event.UnsubscribeAll(this);

        base.OnUnbindUI();
        _lastWaveIndex = -1;

        // 重置缓存，确保面板复用/再次打开时能立刻刷新一次。
        _lastGold = int.MinValue;
        _lastConscienceResource = int.MinValue;
        _lastDarkHeart = int.MinValue;
    }

    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        // 刷新 UI 显示
        if (CY.Wave != null)
        {
            // 刷新阶段状态
            if (_tmpWaveStage)
            {
                if (CY.Wave.State == WaveManager.WaveState.Preparing)
                {
                    // 如果是第0波（还没开始第1波），显示“准备中”
                    if (CY.Wave.CurrentWaveIndex == 0)
                    {
                        _tmpWaveStage.text = "准备中";
                    }
                    else
                    {
                        // 之后的波次间隙
                        _tmpWaveStage.text = "敌人正在计划重组";
                    }
                }
                else if (CY.Wave.State == WaveManager.WaveState.Fighting)
                {
                    _tmpWaveStage.text = "";
                }
                else
                {
                    _tmpWaveStage.text = "等待开始";
                }
            }
            
            // 刷新波数
            if (_tmpWaveCount)
            {
                // 如果在准备中，且 CurrentWaveIndex 是 0 (刚进游戏)，可能显示为 0 或 1？
                // 根据 StartNextWave 逻辑，Preparing 时 CurrentWaveIndex 还是上一波的。
                // 如果是 Preparing 为了 Warning 下一波，显示 Next Wave 比较好？
                
                // 但用户要的是 "当前波数"。通常 Preparing 时 WaveIndex 还没加。
                // 比如从 Wave 0 结束 -> Preparing (Index=0) -> StartNextWave (Index=1) -> Fighting
                
                // 需求：首波准备时显示0，准备结束进入战斗时+1变成1
                // 后续准备时显示Current (例如1)，准备结束进入下一次战斗时+1变成2
                // 这正好就是 CY.Wave.CurrentWaveIndex 的自然逻辑：
                // StartNextWave() 时 Index++ 并进入 Fighting。
                
                int displayWave = CY.Wave.CurrentWaveIndex;
                _tmpWaveCount.text = $"{displayWave}";
            }

            // 显示倒计时 (剩余时间)
            if (_tmpRemainingTime)
            {
                if (CY.Wave.State != WaveManager.WaveState.None)
                {
                    int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, CY.Wave.RemainingTime));
                    int minutes = totalSeconds / 60;
                    int seconds = totalSeconds % 60;
                    _tmpRemainingTime.text = $"{minutes:00}:{seconds:00}";
                }
                else
                {
                    _tmpRemainingTime.text = "";
                }
            }
            
            UpdateSliderLogic();
            CheckStateChangeForPreview();
        }
    }

    /// <summary>
    /// 部门资源变化事件回调：只在数值变化时刷新文本，避免重复刷新造成的额外 UI 开销。
    /// </summary>
    private void OnDepartmentResourceChanged(ref DepartmentResourceChangedEvent evt)
    {
        ApplyResourceText(evt.Gold, evt.ConscienceResource, evt.DarkHeart);
    }

    /// <summary>
    /// 主动刷新一次资源显示（用于面板首次打开时初始化 UI）。
    /// 注意：这里使用 ServiceLocator.TryGet 防御性获取，避免服务未注册时抛异常导致 UI 打不开。
    /// </summary>
    private void RefreshResourceFromDepartment()
    {
        if (!ServiceLocator.TryGet<DepartmentManager>(out var dept) || dept == null)
        {
            return;
        }

        var data = dept.Data;
        ApplyResourceText(data.Gold, data.ConscienceResource, data.DarkHeart);
    }

    /// <summary>
    /// 将资源数值写入到 TMP 文本。
    /// 说明：使用 TMP.SetText("{0}", value) 可避免 value.ToString() 产生临时字符串（零 GC）。
    /// </summary>
    private void ApplyResourceText(int gold, int conscienceResource, int darkHeart)
    {
        if (_tmpGold && gold != _lastGold)
        {
            _lastGold = gold;
            _tmpGold.SetText("{0}", gold);
        }

        if (_tmpConscienceResource && conscienceResource != _lastConscienceResource)
        {
            _lastConscienceResource = conscienceResource;
            _tmpConscienceResource.SetText("{0}", conscienceResource);
        }

        if (_tmpDarkHeart && darkHeart != _lastDarkHeart)
        {
            _lastDarkHeart = darkHeart;
            _tmpDarkHeart.SetText("{0}", darkHeart);
        }
    }
    
    // ============================================ UI Logic Helpers ============================================

    private void UpdateSliderLogic()
    {
        if (!_sliderWaveProgress) return;

        // 状态变化时捕获 TotalDuration
        if (CY.Wave.State != _lastState)
        {
             // 刚切状态时，RemainingTime 是满的，所以捕获它作为分母
             if (CY.Wave.RemainingTime > _totalDurationForCurrentPhase)
                _totalDurationForCurrentPhase = CY.Wave.RemainingTime;
        }
        
        // 容错：如果 RemainingTime 突然变大了（比如 Reset 了），也更新
        if (CY.Wave.RemainingTime > _totalDurationForCurrentPhase)
        {
            _totalDurationForCurrentPhase = CY.Wave.RemainingTime;
        }

        if (_totalDurationForCurrentPhase > 0.01f)
        {
            // 需求：进度条 0 -> 1 (随着时间流逝增加)
            // RemainingTime 是倒计时 (Total -> 0)
            // 所以 1 - (Remaining / Total) = (0 -> 1)
            _sliderWaveProgress.value = 1.0f - (CY.Wave.RemainingTime / _totalDurationForCurrentPhase);
        }
        else
        {
            _sliderWaveProgress.value = 1;
        }
    }

    private void CheckStateChangeForPreview()
    {
        bool isNewWave = CY.Wave.CurrentWaveIndex != _lastWaveIndex;
        bool isFighting = CY.Wave.State == WaveManager.WaveState.Fighting;
        
        if (isNewWave && isFighting)
        {
            _lastWaveIndex = CY.Wave.CurrentWaveIndex;
            ShowWavePreview();
        }
        
        if (CY.Wave.State != _lastState)
        {
            _lastState = CY.Wave.State;
            // State Changed
            _totalDurationForCurrentPhase = CY.Wave.RemainingTime;
        }
    }

    private void ShowWavePreview()
    {
        var template = CY.Wave.CurrentTemplate;
        if (template != null && !string.IsNullOrEmpty(template.PreviewText) && _tmpWavePreview)
        {
            _tmpWavePreview.text = template.PreviewText;
            _tmpWavePreview.gameObject.SetActive(true);
            
            // 3秒后自动隐藏
            CY.Timer.Delay(3.0f, () => 
            {
                // 回调里再次检查是否为空，防止 UI 已销毁
                if (_tmpWavePreview) _tmpWavePreview.gameObject.SetActive(false);
            });
        }
    }

    /// <summary>
    /// 退出战斗
    /// </summary>
    private void OnExitBattleClicked()
    {
        OverGameEvent evt = new OverGameEvent { };
        CY.Event.Post<OverGameEvent>(ref evt);


        //返回菜单流程
        CY.Procedure.ChangeProcedure<MenuProcedure>();
    }
}
