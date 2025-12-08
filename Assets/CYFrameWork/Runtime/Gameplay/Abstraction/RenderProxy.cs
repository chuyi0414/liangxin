// ============================================================================
// CYFramework 2.2 - RenderProxy 渲染代理
// 文档位置：3.3 数据桥接层 (Data Bridge)
// 功能：View 层消费快照 + 渲染插值
// ============================================================================

using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Gameplay.Abstraction
{
    /// <summary>
    /// 渲染代理
    /// 负责从 GameplayWorld 获取快照并提供插值数据
    /// </summary>
    public class RenderProxy : IUpdateable
    {
        private readonly IGameplayWorld _world;
        
        // 上一次 FixedUpdate 的时间
        private float _lastFixedTime;
        
        // 插值系数 (0~1)
        private float _alpha;
        
        public int UpdateOrder => 0;
        
        /// <summary>
        /// 当前帧快照
        /// </summary>
        public ref readonly RenderSnapshot CurrentSnapshot => ref _world.GetRenderSnapshot();
        
        /// <summary>
        /// 上一帧快照（用于插值）
        /// </summary>
        public ref readonly RenderSnapshot PrevSnapshot => ref _world.GetPrevSnapshot();
        
        /// <summary>
        /// 插值系数
        /// </summary>
        public float Alpha => _alpha;
        
        public RenderProxy(IGameplayWorld world)
        {
            _world = world;
        }
        
        public void OnUpdate(float deltaTime)
        {
            // 计算插值系数
            // 文档位置：3.2.1 Tick 策略：固定逻辑帧 + 渲染插值
            _alpha = (Time.time - _lastFixedTime) / Time.fixedDeltaTime;
            _alpha = Mathf.Clamp01(_alpha);
        }
        
        /// <summary>
        /// 通知 FixedUpdate 发生（由 Bootstrap 调用）
        /// </summary>
        public void OnFixedUpdate()
        {
            _lastFixedTime = Time.time;
        }
        
        /// <summary>
        /// 获取插值后的位置
        /// </summary>
        public Vector3 GetInterpolatedPosition(int index)
        {
            ref readonly var curr = ref CurrentSnapshot;
            ref readonly var prev = ref PrevSnapshot;
            
            if (index < 0 || index >= curr.Count) return Vector3.zero;
            
            // 查找 prev 中对应的索引（通过 ID 匹配）
            int currId = curr.IDs[index];
            int prevIndex = FindIndexById(prev, currId);
            
            if (prevIndex >= 0)
            {
                return Vector3.Lerp(prev.Positions[prevIndex], curr.Positions[index], _alpha);
            }
            
            return curr.Positions[index];
        }
        
        /// <summary>
        /// 获取插值后的旋转
        /// </summary>
        public Quaternion GetInterpolatedRotation(int index)
        {
            ref readonly var curr = ref CurrentSnapshot;
            ref readonly var prev = ref PrevSnapshot;
            
            if (index < 0 || index >= curr.Count) return Quaternion.identity;
            
            int currId = curr.IDs[index];
            int prevIndex = FindIndexById(prev, currId);
            
            if (prevIndex >= 0)
            {
                return Quaternion.Slerp(prev.Rotations[prevIndex], curr.Rotations[index], _alpha);
            }
            
            return curr.Rotations[index];
        }
        
        /// <summary>
        /// 在快照中查找 ID 对应的索引
        /// </summary>
        private int FindIndexById(in RenderSnapshot snapshot, int id)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot.IDs[i] == id)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
