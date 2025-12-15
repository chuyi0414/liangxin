// Assets/_Game/Scripts/Procedures/MenuProcedure.cs
// 菜单流程 - 游戏入口
using CYFramework;
using CYFramework.Core.Procedure;
using CYFramework.Infrastructure;
using UnityEngine;

/// <summary>
/// 菜单流程 - 游戏入口
/// </summary>
[AutoRegisterProcedure("Menu", order: 50)]
public class MenuProcedure : ProcedureBase
{
    /// <summary>
    /// 进入菜单流程时打开主界面
    /// </summary>
    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        CY.Log("[MenuProcedure] 进入主菜单");
        CY.UI.Open<MainUI>();
    }

    /// <summary>
    /// 菜单流程当前无需逐帧逻辑，保留扩展点（如轮播/动效）
    /// </summary>
    protected override void OnUpdate(float deltaTime)
    {
    }

    /// <summary>
    /// 离开菜单流程时关闭主界面，避免残留在 UI 栈
    /// </summary>
    protected override void OnLeave(ProcedureBase nextProcedure)
    {
        CY.UI.Close<MainUI>();
    }
}
