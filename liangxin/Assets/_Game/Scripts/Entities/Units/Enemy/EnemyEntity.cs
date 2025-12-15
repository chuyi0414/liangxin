using CYFramework.Core.Entity;
using UnityEngine;
using CYFramework;


/// <summary>
/// 敌方单位实体基类
/// 支持直接作为普通怪物使用，也可被继承实现特殊逻辑 (如远程怪、自爆怪)
/// </summary>
public class EnemyEntity : EntityBase
{
    protected virtual string DefaultEntityType => "Enemy";

    [Header("Base Components")]
    [SerializeField] protected SpriteRenderer _renderer;
    [SerializeField] protected Animator _animator;
    [SerializeField] protected Rigidbody2D _rb;

    // 运行时属性
    public EnemyRow Data { get; private set; }
    public Collider2D Collider { get; private set; }
    protected float _currentHp;
    protected Transform _target; // 当前攻击目标 (通常是玩家或核心)
    protected Collider2D _targetCollider; // 目标碰撞体缓存
    protected float _attackTimer;

    // 状态
    protected bool _isDead;
    protected bool _isMoving;

    public bool IsDead => _isDead;

    [Header("Physics")]
    [SerializeField] private PhysicsMaterial2D _frictionlessMaterial; // 可在 Inspector 绑定，未绑定则运行时创建

    /// <summary>
    /// 初始化 (当实体从池中取出或创建时调用)
    /// </summary>
    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        // 缓存碰撞体
        Collider = GetComponent<Collider2D>();

        // 消除物理摩擦，避免靠墙/基地时被摩擦力拖慢
        EnsureFrictionlessMaterial(ref _frictionlessMaterial, "EnemyFrictionless");
        if (Collider != null && _frictionlessMaterial != null)
        {
            Collider.sharedMaterial = _frictionlessMaterial;
        }
        if (_rb != null)
        {
            _rb.drag = 0f;
        }

        // 兜底：非 EntityManager 创建时补齐 EntityType
        if (string.IsNullOrEmpty(EntityType))
        {
            EntityType = DefaultEntityType;
        }
    }

    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);

        if (CY.Unit != null)
        {
            CY.Unit.RegisterEnemy(this);
        }

        if (userData is EnemyRow data)
        {
            Data = data;
            _currentHp = Data.Hp;
            
            // 初始化状态
            _isDead = false;
            _isMoving = false;
            _attackTimer = 0;
            if (_renderer) _renderer.color = Color.white;
            if (_rb) 
            {
                _rb.simulated = true;
                _rb.velocity = Vector2.zero;
            }
            
            FindTarget();
            OnBorn(); // 子类钩子

            // 发送初始 HP 事件，让血条Manager能立即生成血条
            UnitHPChangedEvent evt = new UnitHPChangedEvent {
                UnitID = Id,
                CurrentHP = _currentHp,
                MaxHP = Data.Hp,
                Damage = 0,
                WorldPosition = transform.position,
                IsDead = false
            };
            CY.Event.Post(ref evt);
        }
        else
        {
            CY.LogError($"[{GetType().Name}] userData 必须是 EnemyRow 类型");
        }
    }

    protected override void OnEntityRecycle()
    {
        base.OnEntityRecycle();
        
        if (CY.Unit != null)
        {
            CY.Unit.UnregisterEnemy(this);
        }
        
        // 清理引用，防止内存泄漏
        _target = null;
        _targetCollider = null;
    }

    /// <summary>
    /// 寻找目标 (默认找 大本营)
    /// </summary>
    protected virtual void FindTarget()
    {
        // 默认目标：大本营 (优先从 UnitManager 获取)
        if (CY.Unit != null && CY.Unit.BaseCampPoint != null)
        {
            _target = CY.Unit.BaseCampPoint;
            _targetCollider = null; // 重置缓存
        }
        else
        {
            // 兜底
            var baseObj = GameObject.Find("BaseCamp");
            if (baseObj) 
            {
                _target = baseObj.transform;
                _targetCollider = null; // 重置缓存
            }
        }
    }
    
    // 索敌检测计时
    private float _searchTimer = 0;
    private const float SEARCH_INTERVAL = 0.5f;
    private const float ALERT_RANGE = 5.0f; // 警戒半径 (多少米内有员工就转火)

    protected override void OnEntityUpdate(float deltaTime)
    {
        if (_isDead) return;
        
        // 1. 周期性索敌 (检测周围是否有友军)
        _searchTimer += deltaTime;
        if (_searchTimer >= SEARCH_INTERVAL)
        {
            _searchTimer = 0;
            CheckForAggro();
        }

        // 2. 目标有效性检查
        // 如果当前目标是 Employee 但已死亡或消失，重置回大本营
        if (_target == null || !_target.gameObject.activeInHierarchy) 
        {
            FindTarget(); // 回归初心：打大本营
            if (_target == null) return; // 真的找不到目标了 (比如大本营都炸了)
        }
    
        // 3. 执行行为
        CustomUpdate(deltaTime); 

        // 优化距离判定：对所有目标（大本营、员工、老板）都计算“边缘距离”
        // 这样可以避免怪物和目标完全重叠，防止遮挡视觉
        float distance;

        // 尝试获取目标的碰撞体 (带缓存)
        if (_targetCollider == null && _target != null)
        {
            // 优化：优先尝试从已知组件获取缓存的 Collider，避免 GetComponent
            // 这里假设 EmployeeEntity 和 PlayerEntity 都公开了 Collider 属性
            var player = _target.GetComponent<PlayerEntity>();
            if (player != null)
            {
                _targetCollider = player.Collider;
            }
            else
            {
                var employee = _target.GetComponent<EmployeeEntity>();
                if (employee != null)
                {
                    _targetCollider = employee.Collider;
                }
                else
                {
                    // 优化：如果是大本营，直接从 UnitManager 获取缓存
                    if (CY.Unit != null && _target == CY.Unit.BaseCampPoint)
                    {
                        _targetCollider = CY.Unit.BaseCampCollider;
                    }
                    else
                    {
                        // 真的没办法了才用 GetComponent (例如攻击可破坏的障碍物)
                        _targetCollider = _target.GetComponent<Collider2D>();
                    }
                }
            }
        }

        if (_targetCollider != null)
        {
            // 计算从怪物位置到目标碰撞体表面的最近点
            Vector3 closestPoint = _targetCollider.ClosestPoint(transform.position);
            distance = Vector3.Distance(transform.position, closestPoint);
        }
        else
        {
            // 兜底：如果没有碰撞体，回退到中心距离
            distance = Vector3.Distance(transform.position, _target.position);
        }

        if (distance <= Data.Range)
        {
            // 攻击范围内 -> 攻击
            UpdateAttack(deltaTime);
        }
        else
        {
            // 攻击范围外 -> 追击
            UpdateMovement(deltaTime);
        }
    }
    
    /// <summary>
    /// 检测仇恨 (Aggro Check)
    /// </summary>
    protected virtual void CheckForAggro()
    {
        // 0. 绝对优先级：大本营保护区 (Red Zone)
        // 无论当前在打谁，只要走进了大本营的攻击范围（+缓冲），强制转火大本营
        if (CY.Unit != null && CY.Unit.BaseCampPoint != null)
        {
            float distToBase;
            var baseCollider = CY.Unit.BaseCampCollider;
            
            if (baseCollider != null)
            {
                Vector3 closest = baseCollider.ClosestPoint(transform.position);
                distToBase = Vector3.Distance(transform.position, closest);
            }
            else
            {
                distToBase = Vector3.Distance(transform.position, CY.Unit.BaseCampPoint.position);
            }
            
            // 判定：如果在大本营核心区域内
            // 判定：使用滞后比较 (Hysteresis) 防止反复横跳
            // 1. 进入阈值 (Enter Threshold): 只要靠近了大本营一定距离，就强制吸引
            float enterThreshold = Data.Range + 2.0f; 
            // 2. 退出阈值 (Exit Threshold): 一旦锁定了大本营，除非被拉得特别远，否则绝不转火
            float exitThreshold = Data.Range + 5.0f;

            if (_target == CY.Unit.BaseCampPoint)
            {
                // 情况A: 当前已经是大本营 -> 保持粘性（宽容度极高）
                if (distToBase <= exitThreshold)
                {
                    return; // 保持锁定，不检测其他单位
                }
            }
            else
            {
                // 情况B: 当前不是大本营 -> 检查是否在大本营引力范围内
                if (distToBase <= enterThreshold)
                {
                    _target = CY.Unit.BaseCampPoint;
                    _targetCollider = baseCollider;
                    return; // 切换并锁定
                }
            }
        }

        // 优化实现：遍历 ActiveFriendlyUnits 列表，不再用 Physics SphereCast
        if (CY.Unit == null || CY.Unit.ActiveFriendlyUnits.Count == 0) return;

        EntityBase bestUnit = null;
        float minDist = float.MaxValue;
        float sqrAlertRange = ALERT_RANGE * ALERT_RANGE; // 用平方距离避免开方
        Vector3 myPos = transform.position;

        foreach (var unit in CY.Unit.ActiveFriendlyUnits)
        {
            if (unit == null || unit.transform == null) continue;
            if (!unit.gameObject.activeInHierarchy) continue; // 排除已隐藏/失效的

            // 排除自己 (虽然 Friendly 列表里一般不会有 Enemy，但以防万一)
            if (unit == this) continue;

            // 优化：使用 ClosestPoint 计算距离，确保大体型单位边缘进入警戒范围也能被发现
            float sqrDist;
            var unitCollider = unit.GetComponent<Collider2D>(); // 建议缓存，这里为了演示逻辑严谨性直接获取
            
            if (unitCollider != null)
            {
                Vector3 closest = unitCollider.ClosestPoint(myPos);
                sqrDist = (closest - myPos).sqrMagnitude;
            }
            else
            {
                sqrDist = (unit.transform.position - myPos).sqrMagnitude;
            }

            if (sqrDist <= sqrAlertRange)
            {
                if (sqrDist < minDist)
                {
                    minDist = sqrDist;
                    bestUnit = unit;
                }
            }
        }
        
        // 如果找到了更近的 -> 切换
        if (bestUnit != null)
        {
            _target = bestUnit.transform;
            _targetCollider = null; // 切换目标时清空缓存
        }
        else
        {
            // 如果周围没人了，且当前目标不是大本营
            bool isTargetingBase = (_target == CY.Unit.BaseCampPoint);
            
            if (!isTargetingBase)
            {
                // 如果当前追的人跑远了 (1.5倍)
                float sqrDistToCurrent = (_target.position - myPos).sqrMagnitude;
                if (sqrDistToCurrent > (ALERT_RANGE * 1.5f) * (ALERT_RANGE * 1.5f))
                {
                    FindTarget(); // 回去打大本营
                }
            }
        }
    }

    // 子类可重写此方法添加额外 Update 逻辑
    protected virtual void CustomUpdate(float deltaTime) { }
    // 子类可重写此方法添加出生逻辑
    protected virtual void OnBorn() { }

    protected virtual void UpdateMovement(float dt)
    {
        if (_rb == null) return;
        
        _isMoving = true;
        
        // 计算移动目标点：如果是带碰撞体的目标，移动向“最近表面点”而不是“中心点”
        Vector3 myPos = transform.position;
        Vector3 targetPos = _target.position;

        // 尝试使用缓存的碰撞体
        if (_targetCollider == null && _target != null)
        {
             // 再次尝试获取（以防万一 UpdateMovement 在 OnUpdate 之前执行了某些路径）
             // 复用之前的获取逻辑，或者直接依赖 OnUpdate 的结果。
             // 为安全起见，这里做一个轻量级再次检查
            var player = _target.GetComponent<PlayerEntity>();
            if (player != null) _targetCollider = player.Collider;
            else {
                var emp = _target.GetComponent<EmployeeEntity>();
                if (emp != null) _targetCollider = emp.Collider;
                else _targetCollider = _target.GetComponent<Collider2D>();
            }
        }

        if (_targetCollider != null)
        {
            targetPos = _targetCollider.ClosestPoint(myPos);
        }

        float distToTarget = Vector3.Distance(myPos, targetPos);
        
        // 防挤压缓冲区：如果已经非常接近目标点（边缘），直接停止
        if (distToTarget < 0.1f) 
        {
            _rb.velocity = Vector2.zero;
            return; 
        }

        Vector3 direction = (targetPos - myPos).normalized;
        Vector2 velocity = direction * Data.MoveSpeed;
        

        _rb.velocity = velocity;
        
        // 朝向翻转逻辑
        HandleFlip(direction.x);

        if (_animator) _animator.SetBool("IsMoving", true);
    }
    
    protected virtual void HandleFlip(float xDir)
    {
        if (xDir != 0 && _renderer != null)
        {
             // 默认假设素材朝右。若朝左走(x<0)则翻转
             bool flip = xDir < 0;
             Vector3 scale = transform.localScale;
             scale.x = flip ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
             transform.localScale = scale;
        }
    }

    protected virtual void StopMove()
    {
        _isMoving = false;
        if (_rb) _rb.velocity = Vector2.zero;
        if (_animator) _animator.SetBool("IsMoving", false);
    }

    protected virtual void UpdateAttack(float dt)
    {
        StopMove();

        if (_attackTimer <= 0)
        {
            PerformAttack();
            _attackTimer = Data.AttackInterval; // 重置冷却
        }
        else
        {
            _attackTimer -= dt;
        }
    }

    /// <summary>
    /// 执行攻击 (子类核心重写点)
    /// </summary>
    protected virtual void PerformAttack()
    {
        if (_animator) _animator.SetTrigger("Attack");
        
        // 默认实现：简单的近战伤害判定 (或者仅播放动画，由动画事件触发伤害)
        // 这里为了简单直接扣血
        // var targetHealth = _target.GetComponent<IDamageable>();
        // targetHealth?.TakeDamage(Data.Attack);
        
        CY.Log($"[{Data.Name}] 攻击了 {_target.name}");
    }

    /// <summary>
    /// 受伤入口
    /// </summary>
    public virtual void TakeDamage(float damage)
    {
        if (_isDead) return;

        _currentHp -= damage;
        OnTakeDamage(damage); // 子类钩子

        // 发送血量变化事件 (UI 用)
        UnitHPChangedEvent evt = new UnitHPChangedEvent {
            UnitID = Id,
            CurrentHP = _currentHp,
            MaxHP = Data.Hp,
            Damage = (int)damage,
            WorldPosition = transform.position,
            IsDead = _currentHp <= 0
        };
        CY.Event.Post(ref evt);

        // 受击反馈 (通用变红)
        if (_renderer)
        {
            _renderer.color = Color.red;
            CY.Timer.Delay(0.1f, () => { 
                if(_renderer && !_isDead) _renderer.color = Color.white; 
            });
        }

        if (_currentHp <= 0)
        {
            Die();
        }
    }
    
    protected virtual void OnTakeDamage(float damage) { }

    protected virtual void Die()
    {
        _isDead = true;
        // 发送死亡事件 (UI 回收)
        UnitDeadEvent evt = new UnitDeadEvent { UnitID = Id };
        CY.Event.Post(ref evt);

        StopMove();
        if (_rb) _rb.simulated = false; // 禁用物理以免挡路
        
        if (_animator) _animator.SetTrigger("Die");

        OnDead(); // 子类钩子 (自爆、分裂等)

        // 掉落逻辑
        SpawnLoot();

        // 延迟回收 (等待动画播完)
        CY.Timer.Delay(1.0f, () => 
        {
            CY.Entity.RecycleEntity(this);
        });
    }

    protected virtual void OnDead() { }

    protected virtual void SpawnLoot()
    {
        CY.Log($"击杀 {Data.Name}, 掉落金币 {Data.DropGold}");
        // TODO: 生成实际的掉落物实体
    }


#if UNITY_EDITOR
    protected virtual void OnDrawGizmos()
    {
        if (Data != null)
        {
            // 怪物用红色警示
            Gizmos.color = new Color(1, 0, 0, 0.4f);
            Gizmos.DrawWireSphere(transform.position, Data.Range);

            // 警戒范围 (Alert Range) 也可以顺手画一下，虚线或者黄色
            Gizmos.color = new Color(1, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(transform.position, 5.0f); // ALERT_RANGE 常量值
        }
    }
#endif

    /// <summary>
    /// 确保存在零摩擦材质：优先 Inspector 绑定，缺失时运行时创建
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
