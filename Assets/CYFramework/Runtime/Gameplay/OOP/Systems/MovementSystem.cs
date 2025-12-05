// ============================================================================
// CYFramework - 移动系统
// 处理所有实体的移动逻辑
// 支持 OOP 和 DOTS 两种玩法世界实现
// ============================================================================

using UnityEngine;
using CYFramework.Runtime.Core;
using CYFramework.Runtime.Gameplay.Abstraction;

namespace CYFramework.Runtime.Gameplay.OOP.Systems
{
    /// <summary>
    /// 移动系统
    /// 负责处理实体的位置更新
    /// </summary>
    public class MovementSystem : IGameSystem
    {
        public int Priority => 100;

        private IGameplayWorld _world;

        public void Initialize(IGameplayWorld world)
        {
            _world = world;
            Log.I("MovementSystem", "初始化完成");
        }

        public void Update(float deltaTime)
        {
            var entities = _world.GetAllEntities();
            
            for (int i = 0; i < entities.Count; i++)
            {
                EntityData entity = entities[i];
                
                // 只处理活跃且正在移动的实体
                if (entity.State != EntityState.Active || !entity.IsMoving)
                    continue;

                // 计算移动
                Vector3 direction = (entity.TargetPosition - entity.Position).normalized;
                float distance = Vector3.Distance(entity.Position, entity.TargetPosition);
                float moveDistance = entity.MoveSpeed * deltaTime;

                if (moveDistance >= distance)
                {
                    // 到达目标
                    entity.Position = entity.TargetPosition;
                    entity.IsMoving = false;
                }
                else
                {
                    // 继续移动
                    entity.Position += direction * moveDistance;
                    
                    // 更新朝向
                    if (direction.sqrMagnitude > 0.001f)
                    {
                        entity.Rotation = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    }
                }

                // 写回数据
                _world.UpdateEntity(i, entity);
            }
        }

        public void Shutdown()
        {
            _world = null;
            Log.I("MovementSystem", "已关闭");
        }

        public void Reset()
        {
            // 移动系统无需特殊重置
        }
    }
}
