using CYFramework;
using CYFramework.Core.Entity;
using UnityEngine;

/// <summary>
/// 玩家（老板）实体
/// </summary>
public class PlayerEntity : EntityBase
{
    public PlayerRow Data { get; private set; }
    public Collider2D Collider { get; private set; }

    [Header("Movement")]
    private Rigidbody2D _rb;
    private Vector2 _inputDir;

    [Header("Combat")]
    private CYFramework.Core.Pool.GameObjectPool _projectilePool;
    private float _attackTimer;

    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        // 初始化组件引用等
        _rb = GetComponent<Rigidbody2D>();
        Collider = GetComponent<Collider2D>();
        
        if (_rb == null)
        {
            CY.LogError("[PlayerEntity] 缺少 Rigidbody2D 组件，无法移动！");
        }
    }

    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);
        
        if (userData is PlayerRow data)
        {
            Data = data;
            
            // 准备投射物池 (如果是远程 AttackType == 1)
            if (Data.AttackType == 1 && !string.IsNullOrEmpty(Data.ProjectilePath))
            {
                // 1. 加载 Prefab
                var prefab = CY.Resource.Load<GameObject>(Data.ProjectilePath);
                if (prefab != null)
                {
                    // 2. 获取/创建池 (使用 Path 作为 Key，分组 "Projectiles")
                    _projectilePool = CY.Pool.GetOrCreatePool(Data.ProjectilePath, prefab, "Projectiles");
                }
                else
                {
                    CY.LogError($"[PlayerEntity] 无法加载投射物: {Data.ProjectilePath}");
                }
            }
        }
        
        // 重置状态
        _inputDir = Vector2.zero;
        if (_rb != null) _rb.velocity = Vector2.zero;
        
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

    protected override void OnEntityUpdate(float deltaTime)
    {
        base.OnEntityUpdate(deltaTime);
        
        // 1. 处理输入 (Input.GetAxisRaw 获取 -1, 0, 1)
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        
        _inputDir = new Vector2(x, y).normalized;
        
        // 2. 面朝向处理
        if (x != 0)
        {
            // 保持编辑器里设置的 Y/Z 缩放，只改变 X 的正负
            var scale = transform.localScale;
            scale.x = x > 0 ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        // 3. 自动攻击逻辑
        if (Data != null)
        {
            _attackTimer -= deltaTime;
            if (_attackTimer <= 0)
            {
                var target = FindNearestEnemy();
                if (target != null)
                {
                    Attack(target);
                    _attackTimer = 1f / Data.AttackSpeed; // 重置冷却
                }
            }
        }
    }

    private EnemyEntity FindNearestEnemy()
    {
        if (CY.Unit == null) return null;
        
        EnemyEntity nearest = null;
        float minDistSq = Data.Range * Data.Range;
        
        // 遍历所有活跃敌人
        foreach (var entity in CY.Unit.ActiveEnemies)
        {
            // 简单的距离判定 (注意：EnemyEntity 需要公开 IsDead 属性，或者判断 HP)
            // 这里假设 TakeDamage 内部会处理死亡，外部只要它还在列表里且活着就行
            if (entity is EnemyEntity enemy)
            {
                 // 稍微扩大一点判定，或者严格按照 Range
                 float distSq = (entity.transform.position - transform.position).sqrMagnitude;
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
        // 播放动画 (如果有)
        // var anim = GetComponentInChildren<Animator>();
        // if (anim) anim.SetTrigger("Attack"); 
        
        // 远程攻击 (Type 1)
        if (Data.AttackType == 1 && _projectilePool != null)
        {
             // 从池中获取
             var go = _projectilePool.Get(transform.position, Quaternion.identity);
             var projectile = go.GetComponent<SimpleProjectile>();
             if (projectile != null)
             {
                 // 注入池引用，方便回收
                 projectile.SetPool(_projectilePool);
                 
                 // 初始化
                 Vector3 direction = (target.transform.position - transform.position).normalized;
                 projectile.Init(direction, Data.Attack, 10f); 
             }
             return;
        }

        // 近战攻击 (Type 0) 或 远程资源缺失
        // 播放攻击动作...
        target.TakeDamage(Data.Attack);
    }

    protected override void OnEntityFixedUpdate(float deltaTime)
    {
        base.OnEntityFixedUpdate(deltaTime);

        // 3. 物理移动
        if (_rb != null && Data != null)
        {
            _rb.velocity = _inputDir * Data.MoveSpeed;
        }
    }
}
