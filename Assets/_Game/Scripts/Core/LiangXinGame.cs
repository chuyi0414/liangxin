using CYFramework;
using CYFramework.Core;
using CYFramework.Core.Event;
using CYFramework.Infrastructure;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiangXinGame : GameEntryBase
    {
        // 开启自动注册流程（扫描 [AutoRegisterProcedure] 特性）
        protected override bool AutoRegisterProcedures => true;
        
        // 子系统
        public GameResourceManager Resources { get; private set; }
        
        // 便捷访问
        public static LiangXinGame Game => Get<LiangXinGame>();
        public static int Gold => Game?.Resources?.Gold ?? 0;
        public static int CurrentWave => Game?.Resources?.CurrentWave ?? 0;
        
        protected override void OnGameInit()
        {
            Resources = new GameResourceManager();
        }
        
        protected override void OnGameStart()
        {
            // 按名称启动流程（不用写泛型）
            CY.Procedure.Start("Menu");
        }
    }