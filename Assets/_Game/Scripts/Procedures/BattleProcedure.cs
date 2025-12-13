// Assets/_Game/Scripts/Procedures/BattleProcedure.cs
// 战斗流程 - 包含准备阶段和战斗阶段
using CYFramework;
using CYFramework.Core.Procedure;

/// <summary>
/// 战斗流程 - 包含准备阶段和战斗阶段
/// </summary>
[AutoRegisterProcedure("Battle", order: 100)]
public class BattleProcedure : ProcedureBase
{
    /// <summary>
    /// 进入战斗流程：创建玩家并打开战斗 HUD
    /// </summary>
    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        // 1. 创建玩家（老板）
        CY.Player.Spawn();
        
        // 2. 打开战斗UI
        CY.UI.Open<BattleUI>();
        
        CY.Log("[BattleProcedure] 战斗开始！");
    }

    /// <summary>
    /// 战斗流程每帧驱动
    /// </summary>
    protected override void OnUpdate(float deltaTime)
    {
        // TODO: 波次逻辑、战斗状态更新等
    }

    /// <summary>
    /// 离开战斗流程：销毁玩家，关闭 HUD
    /// </summary>
    protected override void OnLeave(ProcedureBase nextProcedure)
    {
        // 销毁玩家
        CY.Player.Despawn();
        
        CY.UI.Close<BattleUI>();
        
        CY.Log("[BattleProcedure] 战斗结束！");
    }
}

