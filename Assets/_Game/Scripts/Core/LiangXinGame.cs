using CYFramework;
using CYFramework.Core;
using CYFramework.Infrastructure;

/// <summary>
/// 良心防线 - 游戏入口
/// </summary>
public class LiangXinGame : GameEntryBase
{
    protected override void OnGameInit()
    {
        // 游戏特定的服务注册（如需要）
        
        CY.Log("[LiangXinGame] 游戏初始化完成");
    }
    
    protected override void OnGameStart()
    {
        // 框架自动启动入口流程
    }
}
