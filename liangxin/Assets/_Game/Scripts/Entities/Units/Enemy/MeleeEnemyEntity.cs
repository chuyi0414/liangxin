using CYFramework;
using UnityEngine;

/// <summary>
/// 通用近战单位
/// </summary>
public class MeleeEnemyEntity : EnemyEntity
{
    protected override void PerformAttack()
    {
        if (_animator) _animator.SetTrigger("Attack");

        // 近战判定：检测前方扇形或圆形区域内的目标
        // 这里简化为：直接判断目标是否还在攻击距离内
        if (_target != null)
        {
            float dist = Vector3.Distance(transform.position, _target.position);
            // 允许一点容错 (Range + 0.5)
            if (dist <= Data.Range + 0.5f)
            {
                // 造成伤害
                // var damageable = _target.GetComponent<IDamageable>();
                // damageable?.TakeDamage(Data.Attack);
                
                CY.Log($"[Melee] {Data.Name} 砍了 {_target.name} 一刀! 伤害: {Data.Attack}");
            }
        }
    }
}
