using CYFramework;
using CYFramework.Core.Procedure;
using UnityEngine;

[AutoRegisterProcedure(name: "Game", order: 20)]
public class GameProcedure : ProcedureBase
{
    private LevelEntity _levelEntity;

    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        base.OnEnter(previousProcedure);
        CY.UI.Open<GameUIPanel>();
        SpawnDefaultLevel();
        CY.Timer.NextFrame(ResumeWaveSystem); // 下一帧再启动波次系统，确保 UI 初始化完成
    }

    protected override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

    }

    protected override void OnLeave(ProcedureBase nextProcedure)
    {
        base.OnLeave(nextProcedure);
        CY.UI.Close<GameUIPanel>();
        PauseWaveSystem(); // 退出游戏流程时暂停波次系统

        if (_levelEntity != null)
        {
            CY.Entity.RecycleEntity(_levelEntity);
            _levelEntity = null;
        }
    }

    /// <summary>
    /// 创建 DefaultLevel 预制体，由预制体内部脚本负责生成其它场景实体。
    /// </summary>
    private void SpawnDefaultLevel()
    {
        _levelEntity = CY.Entity.SpawnEntity<LevelEntity>();
        if (_levelEntity == null)
        {
            CY.LogError("[GameProcedure] 创建 DefaultLevelEntity 失败。");
        }
    }

    /// <summary>
    /// 启动波次系统（确保 GameUIPanel 已打开）。
    /// </summary>
    private void ResumeWaveSystem() // 波次系统启动入口
    {
        var waveManager = CY.Wave; // 获取波次管理器
        if (waveManager == null)
        {
            CY.LogWarning("[GameProcedure] WaveManager 未就绪，无法启动波次系统。"); // 输出警告
            return; // 管理器为空时退出
        }

        waveManager.SetPaused(false); // 解除暂停
    }

    /// <summary>
    /// 暂停波次系统（离开游戏流程时执行）。
    /// </summary>
    private void PauseWaveSystem() // 波次系统暂停入口
    {
        var waveManager = CY.Wave; // 获取波次管理器
        if (waveManager == null)
        {
            return; // 管理器为空时退出
        }

        waveManager.SetPaused(true); // 设置暂停
    }
}
