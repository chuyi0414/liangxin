using CYFramework.Infrastructure;
using CYFramework.Core.Pool;
using UnityEngine;

namespace CYFramework
{
    /// <summary>
    /// CY 类扩展 - 良心防线游戏专属功能
    /// </summary>
    public static partial class CY
    {
        /// <summary>单位管理器（原 Recruitment/Player）</summary>
        public static UnitManager Unit => ServiceLocator.Get<UnitManager>();

        /// <summary>战斗数据管理器</summary>
        public static BattleDataManager BattleDataManager => ServiceLocator.Get<BattleDataManager>();

        /// <summary>战斗反馈管理器（血条/飘字）。</summary>
        public static BattleFeedbackManager BattleFeedback => ServiceLocator.Get<BattleFeedbackManager>();

        /// <summary>测试管理器（示例脚本，用于功能验证）。</summary>
        public static TestManager TestManager => ServiceLocator.Get<TestManager>();

        /// <summary>波次管理器（负责波次/刷怪流程）。</summary>
        public static WaveManager Wave => ServiceLocator.Get<WaveManager>();
    }
}
