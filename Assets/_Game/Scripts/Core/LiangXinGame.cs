using CYFramework;
using CYFramework.Core;
using CYFramework.Infrastructure;
using UnityEngine;


/// <summary>
/// 良心防线 - 游戏入口
/// </summary>
public class LiangXinGame : GameEntryBase
{
    protected override void OnGameInit()
    {
        // 游戏特定的服务注册（如需要）
        
        // 注册/创建 CameraManager
        if (!ServiceLocator.IsRegistered<CameraManager>())
        {
            var go = new GameObject("CameraManager");
            go.AddComponent<CameraManager>(); // Awake 逻辑会自动注册
            DontDestroyOnLoad(go); // 保持常驻
        }

        CY.Log("[LiangXinGame] 游戏初始化完成");
    }
    
    protected override void OnGameStart()
    {
        // 框架自动启动入口流程
        
        // 打开血条管理器
        CY.UI.Open<HPBarManager>();
    }
}
