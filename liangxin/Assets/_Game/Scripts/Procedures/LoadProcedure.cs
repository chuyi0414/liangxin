// Assets/_Game/Scripts/Procedures/LoadProcedure.cs
// 加载流程 - 游戏入口，负责加载配置数据
using CYFramework;
using CYFramework.Core.Procedure;
using UnityEngine;  // TextAsset 需要

/// <summary>
/// 加载流程 - 游戏入口
/// 负责：1. 加载配置数据表  2. 显示加载界面
/// </summary>
[AutoRegisterProcedure("Load", order: 0)]
public class LoadProcedure : ProcedureBase
{
    /// <summary>
    /// 进入加载流程
    /// </summary>
    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        // 1. 加载配置数据表
        LoadDataTables();
        
        // 2. 打开加载界面
        CY.UI.Open<LoadUI>();
    }

    /// <summary>
    /// 加载所有配置数据表
    /// </summary>
    /// </summary>
    private void LoadDataTables()
    {
        // 加载全局配置表
        if (!CY.Data.HasDataTable("GlobalConfig"))
        {
            var globalConfigCsv = CY.Resource.Load<TextAsset>("DataTables/Global/GlobalConfig");
            if (globalConfigCsv != null)
            {
                CY.Data.LoadFromCsv<GlobalConfigRow>(globalConfigCsv.text, "GlobalConfig");
                CY.Log("[LoadProcedure] 全局配置表加载完成，总数: " + CY.Data.GetDataTable<GlobalConfigRow>("GlobalConfig").Count);
            }
            else
            {
                CY.LogWarning("[LoadProcedure] 未找到全局配置表：Resources/DataTables/Global/GlobalConfig.csv");
            }
        }

        // 加载员工表
        if (!CY.Data.HasDataTable("Employees"))
        {
            var employeeCsv = CY.Resource.Load<TextAsset>("DataTables/Units/Employees");
            if (employeeCsv != null)
            {
                CY.Data.LoadFromCsv<EmployeeRow>(employeeCsv.text, "Employees");
                CY.Log("[LoadProcedure] 员工表加载完成，总数: " + CY.Data.GetDataTable<EmployeeRow>("Employees").Count);
            }
            else
            {
                CY.LogWarning("[LoadProcedure] 未找到员工数据表：Resources/DataTables/Units/Employees.csv");
            }
        }
        
        // 加载玩家表
        if (!CY.Data.HasDataTable("Player"))
        {
            var playerCsv = CY.Resource.Load<TextAsset>("DataTables/Units/Player");
            if (playerCsv != null)
            {
                CY.Data.LoadFromCsv<PlayerRow>(playerCsv.text, "Player");
                CY.Log("[LoadProcedure] 玩家表加载完成，总数: " + CY.Data.GetDataTable<PlayerRow>("Player").Count);
            }
            else
            {
                CY.LogWarning("[LoadProcedure] 未找到玩家数据表：Resources/DataTables/Units/Player.csv");
            }
        }
        
        // 加载敌人表
        if (!CY.Data.HasDataTable("Enemy"))
        {
            var enemyCsv = CY.Resource.Load<TextAsset>("DataTables/Units/EnemyTable");
            if (enemyCsv != null)
            {
                CY.Data.LoadFromCsv<EnemyRow>(enemyCsv.text, "Enemy");
                CY.Log("[LoadProcedure] 敌人表加载完成，总数: " + CY.Data.GetDataTable<EnemyRow>("Enemy").Count);
            }
            else
            {
                CY.LogWarning("[LoadProcedure] 未找到敌人数据表：Resources/DataTables/Units/EnemyTable.csv");
            }
        }
        
        // 加载波次模板表
        if (!CY.Data.HasDataTable("WaveTemplate"))
        {
            var waveTemplateCsv = CY.Resource.Load<TextAsset>("DataTables/Battle/WaveTemplateTable");
            if (waveTemplateCsv != null)
            {
                CY.Data.LoadFromCsv<WaveTemplateRow>(waveTemplateCsv.text, "WaveTemplate");
                CY.Log("[LoadProcedure] 波次模板表加载完成，总数: " + CY.Data.GetDataTable<WaveTemplateRow>("WaveTemplate").Count);
            }
            else
            {
                CY.LogWarning("[LoadProcedure] 未找到波次模板表：Resources/DataTables/Battle/WaveTemplateTable.csv");
            }
        }
    }

    /// <summary>
    /// 加载流程当前无需逐帧逻辑，保留扩展点（如加载进度条）
    /// </summary>
    protected override void OnUpdate(float deltaTime)
    {
    }

    /// <summary>
    /// 离开加载流程时关闭加载界面
    /// </summary>
    protected override void OnLeave(ProcedureBase nextProcedure)
    {
        CY.UI.Close<LoadUI>();
    }
}

