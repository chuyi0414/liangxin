using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework
{
    /// <summary>
    /// CY 类扩展 - 良心防线游戏专属功能
    /// </summary>
    public static partial class CY
    {
        /// <summary>玩家（老板）管理器</summary>
        public static PlayerManager Player => ServiceLocator.Get<PlayerManager>();
    }
}
