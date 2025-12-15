using CYFramework;
using CYFramework.Core.Entity;
using UnityEngine;

/// <summary>
/// 员工实体
/// </summary>
public class EmployeeEntity : EntityBase
{
    public EmployeeRow Data { get; private set; }
    public Collider2D Collider { get; private set; }

    [Header("Runtime")]
    [SerializeField] private float _currentHp;

    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        // 初始化组件引用，如 Animator, Rigidbody2D, NavMeshAgent 等
        Collider = GetComponent<Collider2D>();
    }

    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);
        
        if (userData is EmployeeRow data)
        {
            Data = data;
            // 初始化属性
            _currentHp = Data.Hp;
            
            // 根据 Data.JobTitle, Data.Attack 等设置表现或逻辑
            CY.Log($"[EmployeeEntity] 员工 {Data.Code} ({Data.JobTitle}) 上班了！");
        }
        else
        {
            CY.LogError("[EmployeeEntity] 缺少 EmployeeRow 数据！");
        }
        
        // 注册到 UnitManager
        if (CY.Unit != null)
        {
            CY.Unit.RegisterUnit(this);
        }
    }
    
    protected override void OnEntityHide()
    {
        base.OnEntityHide();
        
        // 从 UnitManager 注销
        if (CY.Unit != null)
        {
            CY.Unit.UnregisterUnit(this);
        }
    }

    private float _attackTimer;
    // 投射物池 (如果是远程)
    private CYFramework.Core.Pool.GameObjectPool _projectilePool;

    protected override void OnEntityUpdate(float deltaTime)
    {
        base.OnEntityUpdate(deltaTime);
        
        // 自动攻击逻辑
        if (Data != null)
        {
            _attackTimer -= deltaTime;
            if (_attackTimer <= 0)
            {
                var target = FindNearestEnemy();
                if (target != null)
                {
                    Attack(target);
                    // 攻速转冷却时间
                    _attackTimer = Data.AttackSpeed > 0 ? 1f / Data.AttackSpeed : 1f; 
                }
            }
        }
    }

    private EnemyEntity FindNearestEnemy()
    {
        if (CY.Unit == null) return null;
        
        EnemyEntity nearest = null;
        float minDistSq = Data.Range * Data.Range;
        Vector3 myPos = transform.position;
        
        foreach (var entity in CY.Unit.ActiveEnemies)
        {
            if (entity is EnemyEntity enemy)
            {
                 if (enemy == null || enemy.IsDead) continue;

                 // 核心优化：使用 ClosestPoint 计算距离
                 float distSq;
                 var enemyCollider = enemy.Collider; 
                 
                 if (enemyCollider != null)
                 {
                     Vector3 closestPoint = enemyCollider.ClosestPoint(myPos);
                     distSq = (closestPoint - myPos).sqrMagnitude;

                     // 阻挡检测
                     if (distSq <= minDistSq)
                     {
                         Vector3 direction = (closestPoint - myPos).normalized;
                         float distance = Mathf.Sqrt(distSq);
                         // 加上 BaseCamp 层
                         int layerMask = LayerMask.GetMask("Default", "Obstacle", "BaseCamp"); 
                         RaycastHit2D hit = Physics2D.Raycast(myPos, direction, distance, layerMask);
                         
                         if (hit.collider != null && hit.collider.gameObject != enemy.gameObject)
                         {
                             continue; 
                         }
                     }
                 }
                 else
                 {
                     distSq = (entity.transform.position - myPos).sqrMagnitude;
                 }

                 if (distSq <= minDistSq)
                 {
                     minDistSq = distSq;
                     nearest = enemy;
                 }
            }
        }
        return nearest;
    }

    private void Attack(EnemyEntity target)
    {
        if (target == null || target.IsDead) return;

        // 这里仅实现简单伤害，若有投射物逻辑需参考 PlayerEntity
        // 为了演示完整性，直接造成伤害
        target.TakeDamage(Data.Attack);
    }

    /// <summary>
    /// 受伤逻辑
    /// </summary>
    public void TakeDamage(float damage)
    {
        _currentHp -= damage;
        if (_currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        CY.Log($"[EmployeeEntity] 员工 {Data?.Code} 阵亡！");
        CY.Entity.RecycleEntity(this);
    }
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 员工的攻击范围可视化
        if (Data != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.4f);  // 统一用绿色半透明
            Gizmos.DrawWireSphere(transform.position, Data.Range);
        }
    }
#endif
}
