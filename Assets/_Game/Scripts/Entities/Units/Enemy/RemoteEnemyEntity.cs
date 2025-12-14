using CYFramework;
using UnityEngine;

/// <summary>
/// 远程单位实体
/// 适用于：喷键暴徒、幻饼术士等
/// </summary>
public class RemoteEnemyEntity : EnemyEntity
{
    [Header("Remote Settings")]
    [SerializeField] private GameObject _bulletPrefab; // 子弹预制体 (暂时直接拖拽引用)
    [SerializeField] private Transform _firePoint;     // 发射点 (枪口/嘴巴)

    protected override void PerformAttack()
    {
        if (_bulletPrefab == null)
        {
            CY.LogError($"[{Data.Name}] 缺少 BulletPrefab，无法进行远程攻击！");
            return;
        }

        if (_animator) _animator.SetTrigger("Attack");

        // 计算发射方向
        Vector3 direction = (_target.position - transform.position).normalized;
        Vector3 spawnPos = _firePoint ? _firePoint.position : transform.position;

        // 生成子弹
        var bulletGo = Instantiate(_bulletPrefab, spawnPos, Quaternion.identity);
        
        // 初始化子弹
        var projectile = bulletGo.GetComponent<ProjectileBase>();
        if (projectile != null)
        {
            // 假设远程怪子弹速度 8，目标是 Player
            projectile.Init(direction, 8f, Data.Attack, "Player");
        }
        
        CY.Log($"[{Data.Name}] 发射了子弹！");
    }

    // 可选：重写 UpdateMovement 让它在射程边缘就停下，不要贴脸
    // 现在的基类逻辑是：distance > Range 就会走。
    // 如果 Range 是 6.0，它会在 6.0 处停下开火，这符合远程怪逻辑。
    // 不需要改 UpdateMovement。
}
