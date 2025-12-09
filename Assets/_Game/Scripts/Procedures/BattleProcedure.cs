// Assets/_Game/Scripts/Procedures/BattleProcedure.cs
// 战斗流程 - 包含准备阶段和战斗阶段
using CYFramework;
using CYFramework.Core.Procedure;
using CYFramework.Infrastructure;
using UnityEngine;

/// <summary>
/// 战斗流程 - 包含准备阶段和战斗阶段
/// </summary>
[AutoRegisterProcedure("Battle", order: 1)]
public class BattleProcedure : ProcedureBase
{
    // 游戏阶段
    private enum Phase { Prepare, Fighting }
    private Phase _currentPhase;
    
    // 准备阶段配置
    private float _prepareTime = 5f;
    private float _elapsed;
    
    // 暂停状态
    private bool _isPaused;
    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        
    }
    protected override void OnUpdate(float deltaTime)
    {
        
    }
    protected override void OnLeave(ProcedureBase nextProcedure)
    {
       
    }
    
}