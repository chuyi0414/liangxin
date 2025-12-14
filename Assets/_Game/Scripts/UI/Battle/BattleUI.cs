using CYFramework;
using CYFramework.Core.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private float _totalDurationForCurrentPhase = 1f; // 用于计算进度条的 Slider.value

    protected override void OnBindUI()
    {
        base.OnBindUI();
        _BtnExitBattle.onClick.AddListener(OnExitBattleClicked);
        CY.Log($"Stage: {_tmpWaveStage}, Count: {_tmpWaveCount}, Timer: {_tmpRemainingTime}, Slider: {_sliderWaveProgress}, Preview: {_tmpWavePreview}");
        
        // 初始隐藏情报文本
        if (_tmpWavePreview) _tmpWavePreview.gameObject.SetActive(false);
        
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
        base.OnUnbindUI();
        _lastWaveIndex = -1;

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
                        _tmpWaveStage.text = "战斗准备中...";
                    }
                    else
                    {
                        // 之后的波次间隙
                        _tmpWaveStage.text = "敌人正在组织攻势...";
                    }
                }
                else if (CY.Wave.State == WaveManager.WaveState.Fighting)
                {
                    _tmpWaveStage.text = "战斗中";
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
                    _tmpRemainingTime.text = $"{CY.Wave.RemainingTime:F1}s";
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
        //返回菜单流程
        CY.Procedure.ChangeProcedure<MenuProcedure>();
    }
}
