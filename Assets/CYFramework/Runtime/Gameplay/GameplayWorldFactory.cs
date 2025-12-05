// ============================================================================
// CYFramework - 玩法世界工厂
// 根据平台和配置创建合适的 IGameplayWorld 实现
// ============================================================================

using System;
using UnityEngine;
using CYFramework.Runtime.Core;
using CYFramework.Runtime.Gameplay.Abstraction;
using CYFramework.Runtime.Gameplay.OOP;

namespace CYFramework.Runtime.Gameplay
{
    /// <summary>
    /// 玩法世界工厂
    /// 根据运行平台和配置选择合适的实现
    /// </summary>
    public static class GameplayWorldFactory
    {
        // DOTS 实现类的完整类型名
        private const string DOTS_TYPE_NAME = "CYFramework.Runtime.Gameplay.DOTS.GameplayWorldDots";

        /// <summary>
        /// 创建玩法世界实例
        /// </summary>
        /// <param name="config">玩法配置</param>
        /// <returns>玩法世界实例</returns>
        public static IGameplayWorld Create(GameplayConfig config)
        {
            config ??= new GameplayConfig();
            IGameplayWorld world;

        #if UNITY_WEBGL || WECHAT_MINIGAME
            // 小游戏/浏览器等受限平台，使用 OOP 实现
            world = new GameplayWorldOOP();
            Log.I("GameplayWorldFactory", "平台受限，使用 OOP 实现");
        #else
            // 支持 DOTS 的平台，可以按配置选择 OOP 或 DOTS 实现
            if (config.UseDotsImplementation && IsDotsSupported())
            {
                world = CreateDotsWorld();
                if (world != null)
                {
                    Log.I("GameplayWorldFactory", "使用 DOTS 实现");
                }
                else
                {
                    world = new GameplayWorldOOP();
                    Log.W("GameplayWorldFactory", "DOTS 实现不可用，回退到 OOP 实现");
                }
            }
            else
            {
                world = new GameplayWorldOOP();
                Log.I("GameplayWorldFactory", "使用 OOP 实现");
            }
        #endif

            world.Initialize(config);
            return world;
        }

        /// <summary>
        /// 通过反射创建 DOTS 实现（避免循环引用）
        /// </summary>
        private static IGameplayWorld CreateDotsWorld()
        {
            try
            {
                // 尝试从所有已加载的程序集中查找类型
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type dotsType = assembly.GetType(DOTS_TYPE_NAME);
                    if (dotsType != null)
                    {
                        return Activator.CreateInstance(dotsType) as IGameplayWorld;
                    }
                }
            }
            catch (Exception e)
            {
                Log.W("GameplayWorldFactory", $"创建 DOTS 实现失败: {e.Message}");
            }
            return null;
        }

        /// <summary>
        /// 检查当前平台是否支持 DOTS
        /// </summary>
        private static bool IsDotsSupported()
        {
        #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID
            // 编辑器和原生平台支持 DOTS
            return true;
        #else
            return false;
        #endif
        }

        /// <summary>
        /// 获取当前平台推荐的实现类型
        /// </summary>
        public static string GetRecommendedImplementation()
        {
        #if UNITY_WEBGL || WECHAT_MINIGAME
            return "OOP (平台受限)";
        #else
            return "OOP 或 DOTS (可选)";
        #endif
        }
    }
}
