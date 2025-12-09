// Assets/_Game/Scripts/Procedures/GameOverProcedure.cs
// 结算流程 - 统一处理胜利/失败
using CYFramework;
using CYFramework.Core.Procedure;
using CYFramework.Infrastructure;
using UnityEngine;

/// <summary>
/// 游戏结束参数
/// </summary>
public class GameOverParams
{
    public bool IsVictory;
}
/// <summary>
/// 结算流程 - 统一处理胜利/失败
/// 继承泛型基类以接收参数
/// </summary>
[AutoRegisterProcedure("GameOver", order: 2)]
public class GameOverProcedure : ProcedureBase<GameOverParams>
{
    private bool _isVictory;
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