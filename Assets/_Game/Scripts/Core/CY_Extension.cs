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
        
        /// <summary>波数管理器</summary>
        public static WaveManager Wave => ServiceLocator.Get<WaveManager>();
        
        /// <summary>部门（经济）管理器</summary>
        public static DepartmentManager Department => ServiceLocator.Get<DepartmentManager>();
        
        /// <summary>相机管理器</summary>
        public static CameraManager CameraManager => ServiceLocator.Get<CameraManager>();
    }
}
