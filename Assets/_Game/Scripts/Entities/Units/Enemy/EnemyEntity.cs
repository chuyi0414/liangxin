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
    protected float _currentHp;
    protected Transform _target; // 当前攻击目标 (通常是玩家或核心)
    protected float _attackTimer;

    // 状态
    protected bool _isDead;
    protected bool _isMoving;

    /// <summary>
    /// 初始化 (当实体从池中取出或创建时调用)
    /// </summary>
    protected override void OnEntityInit(object userData)
    {
        // 兜底：非 EntityManager 创建时补齐 EntityType
        if (string.IsNullOrEmpty(EntityType))
        {
            EntityType = DefaultEntityType;
        }
    }

    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);

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
        }
        else
        {
            CY.LogError($"[{GetType().Name}] userData 必须是 EnemyRow 类型");
        }
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
        }
        else
        {
            // 兜底
            var baseObj = GameObject.Find("BaseCamp");
            if (baseObj) _target = baseObj.transform;
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

        float distance = Vector3.Distance(transform.position, _target.position);

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
        // 0. 优先级判断：如果当前正在攻击大本营，则无视一切干扰
        // 大本营优先级最高，一旦粘上就不放手
        if (_target != null && CY.Unit != null && _target == CY.Unit.BaseCampPoint)
        {
            float distToBase = Vector3.Distance(transform.position, _target.position);
            // 稍微放宽一点判断，或者是 <= Data.Range
            if (distToBase <= Data.Range)
            {
                return; 
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

            float sqrDist = (unit.transform.position - myPos).sqrMagnitude;
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
        Vector3 direction = (_target.position - transform.position).normalized;
        Vector2 velocity = direction * Data.MoveSpeed;
        
        // Debug Log: 频率较高，确认问题后请删除
        if (Time.frameCount % 60 == 0) // 每约1秒打印一次，防止刷屏
        {
            CY.Log($"[EnemyDebug] {_target.name} | Dir:{direction} | Speed:{Data.MoveSpeed} | Vel:{velocity}");
        }

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
        StopMove();
        if (_rb) _rb.simulated = false; // 禁用物理以免挡路
        
        if (_animator) _animator.SetTrigger("Die");

        OnDead(); // 子类钩子 (自爆、分裂等)

        // 掉落逻辑
        SpawnLoot();

        // 延迟回收 (等待动画播完)
        CY.Timer.Delay(1.0f, () => 
        {
            CY.Entity.HideEntity(this);
        });
    }

    protected virtual void OnDead() { }

    protected virtual void SpawnLoot()
    {
        CY.Log($"击杀 {Data.Name}, 掉落金币 {Data.DropGold}");
        // TODO: 生成实际的掉落物实体
    }

    protected override void OnEntityHide()
    {
        // 清理引用，防止内存泄漏
        _target = null;
        StopMove();
    }
}
