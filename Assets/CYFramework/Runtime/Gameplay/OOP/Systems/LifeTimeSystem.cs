// ============================================================================
// CYFramework - 生命周期系统
// 处理有限生命周期的实体（如投射物、特效等）
// 支持 OOP 和 DOTS 两种玩法世界实现
// ============================================================================

using UnityEngine;
using CYFramework.Runtime.Core;
using CYFramework.Runtime.Gameplay.Abstraction;

namespace CYFramework.Runtime.Gameplay.OOP.Systems
{
    /// <summary>
    /// 生命周期系统
    /// 负责处理实体的存活时间
    /// </summary>
    public class LifeTimeSystem : IGameSystem
    {
        public int Priority => 50;

        private IGameplayWorld _world;

        public void Initialize(IGameplayWorld world)
        {
            _world = world;
            Log.I("LifeTimeSystem", "初始化完成");
        }

        public void Update(float deltaTime)
        {
            var entities = _world.GetAllEntities();
            
            for (int i = 0; i < entities.Count; i++)
            {
                EntityData entity = entities[i];
                
                // 只处理活跃且有生命周期限制的实体
                if (entity.State != EntityState.Active || entity.MaxLifeTime <= 0)
                    continue;

                // 更新存活时间
                entity.LifeTime += deltaTime;

                // 检查是否超时
                if (entity.LifeTime >= entity.MaxLifeTime)
                {
                    entity.State = EntityState.PendingDestroy;
                }

                // 写回数据
                _world.UpdateEntity(i, entity);
            }
        }

        public void Shutdown()
        {
            _world = null;
            Log.I("LifeTimeSystem", "已关闭");
        }

        public void Reset()
        {
            // 无需特殊重置
        }
    }
}
