// ============================================================================
// CYFramework - 清理系统
// 负责清理待销毁的实体
// 支持 OOP 和 DOTS 两种玩法世界实现
// ============================================================================

using UnityEngine;
using CYFramework.Runtime.Core;
using CYFramework.Runtime.Gameplay.Abstraction;

namespace CYFramework.Runtime.Gameplay.OOP.Systems
{
    /// <summary>
    /// 清理系统
    /// 负责在每帧末尾清理待销毁的实体
    /// </summary>
    public class CleanupSystem : IGameSystem
    {
        public int Priority => 9999; // 最后执行

        private IGameplayWorld _world;

        public void Initialize(IGameplayWorld world)
        {
            _world = world;
            Log.I("CleanupSystem", "初始化完成");
        }

        public void Update(float deltaTime)
        {
            // 清理待销毁的实体
            _world.CleanupPendingDestroyEntities();
        }

        public void Shutdown()
        {
            _world = null;
            Log.I("CleanupSystem", "已关闭");
        }

        public void Reset()
        {
            // 无需特殊重置
        }
    }
}
