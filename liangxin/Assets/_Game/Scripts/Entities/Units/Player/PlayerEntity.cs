using CYFramework;
using CYFramework.Core.Entity;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 玩家（老板）实体
/// </summary>
public class PlayerEntity : EntityBase
{
    public PlayerRow Data { get; private set; }
    public Collider2D Collider { get; private set; }
    public float CurrentHp { get; private set; }

    [Header("Movement")]
    private Rigidbody2D _rb;
    private Vector2 _inputDir;

    [Header("Combat")]
    private CYFramework.Core.Pool.GameObjectPool _projectilePool;
    private float _attackTimer;

    [Header("Physics")]
    [SerializeField] private PhysicsMaterial2D _frictionlessMaterial; // 可在 Inspector 绑定零摩擦材质

    //初始位置
    private Vector3 _initialPosition = new Vector3(3,0,0);

    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        // 初始化组件引用等
        _rb = GetComponent<Rigidbody2D>();
        Collider = GetComponent<Collider2D>();

        // 消除物理摩擦，防止蹭墙减速
        EnsureFrictionlessMaterial(ref _frictionlessMaterial, "PlayerFrictionless");
        if (Collider != null && _frictionlessMaterial != null)
        {
            Collider.sharedMaterial = _frictionlessMaterial;
        }
        if (_rb != null)
        {
            _rb.drag = 0f;
        }

        if (_rb == null)
        {
            CY.LogError("[PlayerEntity] 缺少 Rigidbody2D 组件，无法移动！");
        }

        transform.position = _initialPosition;
    }

    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);
        
        if (userData is PlayerRow data)
        {
            Data = data;
            CurrentHp = Data.Hp;
            
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
    
    public void TakeDamage(float damage)
    {
        if (CurrentHp <= 0) return;
        
        CurrentHp -= damage;
        
        // 扣除良心值/Update UI...
        // 可以在这里广播事件，或者直接通知 UI
        
        if (CurrentHp <= 0)
        {
            CurrentHp = 0;
            Die();
        }
    }

    private void Die()
    {
        CY.Log("老板倒下了！良心破产！");
        // CY.Game.GameOver(); // 假设有
        // 暂时只是回收或隐藏
        // CY.Entity.RecycleEntity(this); // 如果是 SpawnEntity 出来的
        // 但 Player 可能是场景常驻，或者需要特殊处理
    }

    private EnemyEntity FindNearestEnemy()
    {
        if (CY.Unit == null) return null;
        
        EnemyEntity nearest = null;
        float minDistSq = Data.Range * Data.Range;
        Vector3 myPos = transform.position;
        
        // 遍历所有活跃敌人
        foreach (var entity in CY.Unit.ActiveEnemies)
        {
            if (entity is EnemyEntity enemy)
            {
                 if (enemy == null || enemy.IsDead) continue;

                 // 核心优化：不再计算中心点距离，而是计算"我到敌人碰撞体表面最近点"的距离
                 // 这样大体型的怪物只要有一部分身体进入射程，就会被判定为"在射程内"
                 float distSq;
                 var enemyCollider = enemy.Collider; 
                 
                 if (enemyCollider != null)
                 {
                     Vector3 closestPoint = enemyCollider.ClosestPoint(myPos);
                     distSq = (closestPoint - myPos).sqrMagnitude;
                     
                     // 视线阻挡检测 (Raycast)
                     // 如果距离合适，再额外发射一条射线，看看中间有没有类似 Wall / BaseCamp 的障碍物
                     if (distSq <= minDistSq)
                     {
                         Vector3 direction = (closestPoint - myPos).normalized;
                         float distance = Mathf.Sqrt(distSq);
                         // LayerMask 需要根据项目设置调整，这里假设 Default 层包含墙壁和大本营
                         // 必须这就是所谓的 "RaycastHit2D hit = Physics2D.Raycast(..., distance, layerMask)"
                         // 注意：要小心不要射到自己或者敌人自己，所以要适当调整起始点或过滤
                         
                         // 简单起见，检测 Default 层 (通常这个层放墙壁、建筑) 和 BaseCamp 层
                         int layerMask = LayerMask.GetMask("Default", "Obstacle", "BaseCamp"); 
                         RaycastHit2D hit = Physics2D.Raycast(myPos, direction, distance, layerMask);
                         
                         // 如果射到了东西，且这个东西不是敌人本身(理论上Default层不含敌人)，则说明被挡住了
                         if (hit.collider != null && hit.collider.gameObject != enemy.gameObject)
                         {
                             continue; // 被挡住了，跳过
                         }
                     }
                 }
                 else
                 {
                     // 兜底：没有碰撞体就算中心点
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
                 
                 // 初始化 (目标 Tag: Enemy)
                 Vector3 direction = (target.transform.position - transform.position).normalized;
                 projectile.Init(direction, Data.Attack, 10f, "Enemy"); 
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
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Data != null)
        {
            // 绿色线框表示攻击范围
            Gizmos.color = new Color(0, 1, 0, 0.4f);
            Gizmos.DrawWireSphere(transform.position, Data.Range);
        }
    }
#endif

    /// <summary>
    /// 确保存在零摩擦材质：优先使用 Inspector 绑定，缺失时运行时创建一份
    /// </summary>
    private void EnsureFrictionlessMaterial(ref PhysicsMaterial2D materialField, string runtimeName)
    {
        if (materialField != null) return;
        materialField = new PhysicsMaterial2D(runtimeName)
        {
            friction = 0f,
            bounciness = 0f
        };
    }
}
