using CYFramework;
using CYFramework.Core.Procedure;
using CYFramework.Infrastructure;
using UnityEngine;

[AutoRegisterProcedure(name: "Game", order: 20)]
public class GameProcedure : ProcedureBase
{
    private LevelEntity _levelEntity;
    private const string EmployeeDataTableName = "Employee"; // 员工数据表名常量（与 LoadProcedure 保持一致）
    private const string EmployeeDataCsvPath = "DataTable/Unit/Employee/Employee"; // 员工数据表 Resources 路径常量（与 LoadProcedure 保持一致）

    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        base.OnEnter(previousProcedure);
        var battleDataManager = CY.BattleDataManager; // 获取战斗数据管理器
        if (battleDataManager != null)
        {
            var resetCompleted = battleDataManager.ResetRuntimeForNewGame(false); // 进入游戏流程时先重置运行时数据，确保 UI 打开时读取到初始值
            var panel = CY.UI.Open<GameUIPanel>(); // 打开游戏 UI 面板
            if (panel != null) // 面板存在判定
            {
                ReloadEmployeeDataTableIfPossible(); // 进入游戏流程时重载员工数据表（避免只切到 GameProcedure 时仍使用旧表）
                panel.RefreshTalentPoolContent(); // 进入游戏流程时刷新人才库 Content（Employee.csv 抽取）
            }
            if (!resetCompleted)
            {
                battleDataManager.ResetRuntimeForNewGame(true); // 数据尚未加载时改为“加载完成后派发事件”，避免 UI 长期显示为 --
            }

            SpawnDefaultLevel();
            CY.Timer.NextFrame(ResumeWaveSystem); // 下一帧再启动波次系统，确保 UI 初始化完成
            return; // 已处理打开 UI 与初始化逻辑后直接返回
        }
        else
        {
            CY.LogWarning("[GameProcedure] BattleDataManager 未就绪，无法在进入游戏流程时重置 UI 数据。"); // 输出警告
        }

        var fallbackPanel = CY.UI.Open<GameUIPanel>(); // BattleDataManager 未就绪时仍然打开 UI（会显示 --）
        if (fallbackPanel != null) // 面板存在判定
        {
            ReloadEmployeeDataTableIfPossible(); // 进入游戏流程时重载员工数据表（避免只切到 GameProcedure 时仍使用旧表）
            fallbackPanel.RefreshTalentPoolContent(); // 进入游戏流程时刷新人才库 Content（Employee.csv 抽取）
        }
        SpawnDefaultLevel();
        CY.Timer.NextFrame(ResumeWaveSystem); // 下一帧再启动波次系统，确保 UI 初始化完成
    }

    /// <summary>
    /// 尝试重新加载员工数据表：用于“直接从 MainProcedure 切到 GameProcedure”时，确保人才库读取到最新 Employee.csv。
    /// </summary>
    private void ReloadEmployeeDataTableIfPossible() // 员工数据表重载入口
    {
        var dataManager = CY.Data; // 获取数据表管理器
        if (dataManager == null) // 管理器为空判定
        {
            CY.LogWarning("[GameProcedure] DataTableManager 未就绪，无法重载员工数据表。"); // 输出警告日志
            return; // 管理器为空时直接退出
        }

        if (dataManager.HasDataTable(EmployeeDataTableName)) // 数据表已加载判定
        {
            dataManager.UnloadDataTable(EmployeeDataTableName); // 卸载旧表（确保读取到最新内容）
        }

        var csvAsset = CY.Resource.Load<TextAsset>(EmployeeDataCsvPath); // 从 Resources 加载员工 CSV 文本资源
        if (csvAsset == null) // 资源为空判定
        {
            CY.LogError($"[GameProcedure] 加载员工数据失败：{EmployeeDataCsvPath}"); // 输出错误日志
            return; // 资源为空时退出
        }

        dataManager.LoadFromCsv<EmployeeUnitRow>(csvAsset.text, EmployeeDataTableName); // 解析并加载员工数据表
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
        ResetWaveRuntime(); // 退出流程时重置波次运行时状态

        if (ServiceLocator.TryGet<CameraManager>(out var cameraManager))
        {
            cameraManager.ClearFollowTarget(); // 清理相机跟随目标
            cameraManager.ResetWorldCameraPosition(Vector2.zero); // 重置相机到原点（保持 Z 不变）
        }

        var unitManager = CY.Unit; // 获取单位管理器
        var entityManager = CY.Entity; // 获取实体管理器
        if (entityManager != null)
        {
            entityManager.RecycleAllEntities("Level"); // 回收关卡实体
            entityManager.RecycleAllEntities("CompanyEntity"); // 回收公司实体
            entityManager.RecycleAllEntities("EnemyEntity"); // 回收敌人实体
            entityManager.RecycleAllEntities("MoneyEntity"); // 回收金币实体
            entityManager.RecycleAllEntities("BlackHeartEntity"); // 回收黑心实体
            entityManager.RecycleAllEntities("Players"); // 回收玩家实体
            if (unitManager != null && unitManager.TryGetDefaultPlayerRow(out var playerRow))
            {
                var bulletArrayId = playerRow.BulletArrayId; // 读取玩家子弹数组 Id
                if (bulletArrayId > 0 && unitManager.TryGetBulletArrayRow(bulletArrayId, out var bulletArrayRow))
                {
                    if (bulletArrayRow.TryGetPrefabPaths(out var prefabPaths))
                    {
                        for (int i = 0; i < prefabPaths.Length; i++)
                        {
                            var prefabPath = prefabPaths[i]; // 获取当前子弹路径
                            if (string.IsNullOrEmpty(prefabPath))
                            {
                                continue; // 空路径时跳过
                            }

                            entityManager.RecycleAllEntities(prefabPath); // 回收玩家子弹实体
                        }
                    }
                }
            }
        }

        if (unitManager != null)
        {
            unitManager.ClearAll(); // 清空单位缓存引用
        }

        _levelEntity = null; // 清空关卡实体引用
    }

    /// <summary>
    /// 重置波次运行时数据（避免流程切换残留）。
    /// </summary>
    private void ResetWaveRuntime() // 波次运行时重置入口
    {
        var waveManager = CY.Wave; // 获取波次管理器
        if (waveManager != null)
        {
            waveManager.ResetRuntime(); // 重置波次运行时
        }

        if (ServiceLocator.TryGet<WaveAutoAdvanceManager>(out var autoAdvanceManager))
        {
            autoAdvanceManager.ResetRuntime(); // 重置自动推进运行时
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
