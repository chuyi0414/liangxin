using CYFramework;
using CYFramework.Core.Pool;
using UnityEngine;

/// <summary>
/// 远程单位实体
/// 适用于：喷键暴徒、幻饼术士等
/// </summary>
public class RemoteEnemyEntity : EnemyEntity
{
    [Header("Remote Settings")]
    [SerializeField] private Transform _firePoint;     // 发射点 (枪口/嘴巴)

    private GameObjectPool _bulletPool;

    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        
        if (!string.IsNullOrEmpty(Data.ProjectilePath))
        {
            var prefab = CY.Resource.Load<GameObject>(Data.ProjectilePath);
            if (prefab != null)
            {
                // 初始化对象池 (使用 Path 作为 Key，放入 EnemyProjectiles 分组)
                _bulletPool = CY.Pool.GetOrCreatePool(Data.ProjectilePath, prefab, "EnemyProjectiles");
            }
            else
            {
                CY.LogError($"[{Data.Name}] 无法加载子弹预制体: {Data.ProjectilePath}");
            }
        }
    }

    protected override void PerformAttack()
    {
        if (_bulletPool == null)
        {
            CY.LogError($"[{Data.Name}] 缺少 BulletPrefab 或 Pool 初始化失败，无法进行远程攻击！");
            return;
        }

        if (_animator) _animator.SetTrigger("Attack");

        // 计算发射方向
        // 增加一点偏移，防止重叠
        Vector3 spawnPos = _firePoint ? _firePoint.position : transform.position;
        Vector3 direction = (_target.position - transform.position).normalized;

        // 从池中生成子弹
        var bulletGo = _bulletPool.Get(spawnPos, Quaternion.identity);
        
        // 初始化子弹
        var projectile = bulletGo.GetComponent<SimpleProjectile>();
        if (projectile != null)
        {
            projectile.SetPool(_bulletPool);
            // 这里我们用 hardcode 的速度 8f，目标 Tag: Player
            projectile.Init(direction, Data.Attack, 8f, "Player");
        }
        
        CY.Log($"[{Data.Name}] 发射了子弹！");
    }

    // 可选：重写 UpdateMovement 让它在射程边缘就停下，不要贴脸
    // 现在的基类逻辑是：distance > Range 就会走。
    // 如果 Ra
    // nge 是 6.0，它会在 6.0 处停下开火，这符合远程怪逻辑。
    // 不需要改 UpdateMovement。
}
