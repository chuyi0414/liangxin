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
    private void LoadDataTables()
    {
        // 加载员工表
        var employeeCsv = CY.Resource.Load<TextAsset>("DataTables/Employees");
        if (employeeCsv != null)
        {
            CY.Data.LoadFromCsv<EmployeeRow>(employeeCsv.text, "Employees");
            CY.Log("[LoadProcedure] 员工表加载完成，总数: " + CY.Data.GetDataTable<EmployeeRow>("Employees").Count);
        }
        else
        {
            CY.LogWarning("[LoadProcedure] 未找到员工数据表：Resources/DataTables/Employees.csv");
        }
        
        // 加载玩家表
        var playerCsv = CY.Resource.Load<TextAsset>("DataTables/Player");
        if (playerCsv != null)
        {
            CY.Data.LoadFromCsv<PlayerRow>(playerCsv.text, "Player");
            CY.Log("[LoadProcedure] 玩家表加载完成，总数: " + CY.Data.GetDataTable<PlayerRow>("Player").Count);
        }
        else
        {
            CY.LogWarning("[LoadProcedure] 未找到玩家数据表：Resources/DataTables/Player.csv");
        }
        
        // TODO: 加载其他数据表（敌人表、波次表、神器表等）
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

