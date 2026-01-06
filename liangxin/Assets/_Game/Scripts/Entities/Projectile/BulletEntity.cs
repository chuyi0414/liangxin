// 引用 System 命名空间，使用 Serializable 特性
using System; // System 基础类型引用
// 引用 CYFramework 命名空间，使用框架入口
using CYFramework; // CYFramework 入口引用
// 引用实体系统命名空间，使用 EntityBase 等类型
using CYFramework.Core.Entity; // 实体系统类型引用
// 引用对象池命名空间，复用生成数据
using CYFramework.Core.Pool; // 对象池类型引用
// 引用 UnityEngine 命名空间，使用 Rigidbody2D/Collider2D 等类型
using UnityEngine; // Unity 引擎基础类型引用

/// <summary>
/// 子弹生成用户数据（类 + 对象池，避免频繁分配）。
/// </summary>
[Serializable] // 序列化支持
public sealed class BulletSpawnUserData // 子弹生成用户数据类
{
    /// <summary>子弹出生位置（XY 平面）。</summary>
    public Vector2 Position; // 出生位置
    /// <summary>子弹方向（会在实体内归一化）。</summary>
    public Vector2 Direction; // 方向向量
    /// <summary>子弹速度（<=0 使用默认速度）。</summary>
    public float Speed; // 速度值
    /// <summary>存活时间（秒，<=0 使用默认存活时间）。</summary>
    public float Lifetime; // 存活时长
    /// <summary>子弹伤害（<=0 使用默认伤害）。</summary>
    public int Damage; // 伤害值
    /// <summary>子弹阵营（用于敌我识别）。</summary>
    public UnitCamp Camp; // 阵营标识
    /// <summary>子弹拥有者（用于忽略自身）。</summary>
    public UnitEntity Owner; // 拥有者引用
    /// <summary>是否暴击。</summary>
    public bool IsCrit; // 暴击标记

    /// <summary>
    /// 重置数据内容（用于对象池复用）。
    /// </summary>
    public void Reset() // 数据重置入口
    {
        Position = Vector2.zero; // 清理位置
        Direction = Vector2.zero; // 清理方向
        Speed = 0f; // 清理速度
        Lifetime = 0f; // 清理寿命
        Damage = 0; // 清理伤害
        Camp = UnitCamp.Neutral; // 清理阵营
        Owner = null; // 清理拥有者
        IsCrit = false; // 清理暴击
    }
}

/// <summary>
/// 子弹基础实体：负责 2D 运动、命中判定与伤害派发。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))] // 约束必须挂载刚体组件
[RequireComponent(typeof(Collider2D))] // 约束必须挂载碰撞体组件
[EntityPrefab("Prefabs/Entities/Projectiles/BulletBase", "BulletBase", "Projectiles")] // 绑定子弹预制体路径
public class BulletEntity : EntityBase // 子弹实体定义
{
    [Header("基础参数")] // Inspector 分组：基础参数
    /// <summary>默认移动速度。</summary>
    [SerializeField] private float _defaultSpeed = 10f; // 默认速度
    /// <summary>默认存活时间（秒）。</summary>
    [SerializeField] private float _defaultLifetime = 2f; // 默认存活时间
    /// <summary>默认伤害值。</summary>
    [SerializeField] private int _defaultDamage = 1; // 默认伤害
    /// <summary>是否使用刚体速度驱动移动。</summary>
    [SerializeField] private bool _useRigidbodyVelocity = true; // 使用速度驱动
    /// <summary>是否对齐朝向到飞行方向。</summary>
    [SerializeField] private bool _alignToDirection = true; // 朝向对齐开关
    /// <summary>是否允许同阵营伤害（已由阵营规则替代，保留仅用于兼容旧资源）。</summary>
    [SerializeField] private bool _allowFriendlyFire = false; // 友伤开关（兼容字段）
    /// <summary>命中后是否自动回收。</summary>
    [SerializeField] private bool _recycleOnHit = true; // 命中回收开关
    /// <summary>是否只命中一次。</summary>
    [SerializeField] private bool _singleHit = true; // 单次命中开关
    /// <summary>命中层遮罩（0 表示不筛选）。</summary>
    [SerializeField] private LayerMask _hitMask; // 命中层遮罩

    /// <summary>Transform 缓存。</summary>
    private Transform _cachedTransform; // Transform 缓存
    /// <summary>Rigidbody2D 缓存。</summary>
    private Rigidbody2D _rigidbody2D; // 刚体缓存
    /// <summary>Collider2D 缓存。</summary>
    private Collider2D _collider2D; // 碰撞体缓存
    /// <summary>子弹生成数据对象池（全局复用）。</summary>
    private static ObjectPool<BulletSpawnUserData> _spawnUserDataPool; // 生成数据对象池
    /// <summary>当前实体持有的生成数据（用于回收）。</summary>
    private BulletSpawnUserData _spawnUserData; // 生成数据引用
    /// <summary>默认方向（由初始朝向决定）。</summary>
    private Vector2 _defaultDirection; // 默认方向

    /// <summary>当前移动方向。</summary>
    private Vector2 _direction; // 运行时方向
    /// <summary>当前速度。</summary>
    private float _speed; // 运行时速度
    /// <summary>当前存活时间。</summary>
    private float _lifeTime; // 运行时存活时间
    /// <summary>当前剩余存活时间。</summary>
    private float _lifeTimer; // 运行时剩余时间
    /// <summary>当前伤害值。</summary>
    private int _damage; // 运行时伤害
    /// <summary>当前阵营。</summary>
    private UnitCamp _camp; // 运行时阵营
    /// <summary>当前拥有者。</summary>
    private UnitEntity _owner; // 运行时拥有者
    /// <summary>是否允许命中。</summary>
    private bool _canHit; // 命中开关
    /// <summary>是否处于激活状态。</summary>
    private bool _isActive; // 激活标记
    /// <summary>是否已经命中过。</summary>
    private bool _hasHit; // 命中标记
    /// <summary>是否暴击。</summary>
    private bool _isCrit; // 暴击标记
    /// <summary>暂停前速度缓存。</summary>
    private Vector2 _cachedVelocityBeforePause; // 暂停速度缓存
    /// <summary>是否有暂停速度缓存。</summary>
    private bool _hasPauseVelocity; // 暂停缓存标记

    /// <summary>当前方向（只读）。</summary>
    public Vector2 Direction => _direction; // 方向只读访问
    /// <summary>当前速度（只读）。</summary>
    public float Speed => _speed; // 速度只读访问
    /// <summary>当前伤害（只读）。</summary>
    public int Damage => _damage; // 伤害只读访问
    /// <summary>当前阵营（只读）。</summary>
    public UnitCamp Camp => _camp; // 阵营只读访问
    /// <summary>当前拥有者（只读）。</summary>
    public UnitEntity Owner => _owner; // 拥有者只读访问
    /// <summary>是否激活（只读）。</summary>
    public bool IsActive => _isActive; // 激活状态只读访问
    /// <summary>是否暴击（只读）。</summary>
    public bool IsCrit => _isCrit; // 暴击只读访问

    /// <summary>
    /// 获取生成数据对象池（延迟创建）。
    /// </summary>
    private static ObjectPool<BulletSpawnUserData> GetSpawnUserDataPool() // 对象池获取入口
    {
        if (_spawnUserDataPool == null)
        {
            _spawnUserDataPool = new ObjectPool<BulletSpawnUserData>(() => new BulletSpawnUserData()); // 创建对象池
        }

        return _spawnUserDataPool; // 返回对象池
    }

    /// <summary>
    /// 申请一个生成数据对象（供外部填充）。
    /// </summary>
    public static BulletSpawnUserData RentSpawnUserData() // 生成数据申请入口
    {
        var data = GetSpawnUserDataPool().Get(); // 从池中获取
        data.Reset(); // 清理旧数据
        return data; // 返回生成数据
    }

    /// <summary>
    /// 归还生成数据对象（供外部回收）。
    /// </summary>
    public static void ReturnSpawnUserData(BulletSpawnUserData data) // 生成数据归还入口
    {
        if (data == null)
        {
            return; // 空引用直接返回
        }

        data.Reset(); // 清理数据
        GetSpawnUserDataPool().Return(data); // 放回对象池
    }

    /// <summary>
    /// 实体初始化：缓存组件引用并记录默认朝向。
    /// </summary>
    /// <param name="userData">初始化传入的数据。</param>
    protected override void OnEntityInit(object userData) // 实体初始化入口
    {
        base.OnEntityInit(userData); // 调用父类初始化
        _cachedTransform = transform; // 缓存 Transform
        _rigidbody2D = GetComponent<Rigidbody2D>(); // 缓存刚体组件
        _collider2D = GetComponent<Collider2D>(); // 缓存碰撞体组件
        _defaultDirection = _cachedTransform != null ? (Vector2)_cachedTransform.right : Vector2.right; // 记录默认方向
        var spriteRenderer = GetComponent<SpriteRenderer>(); // 获取精灵渲染器（用于可视化检查）

        if (_rigidbody2D == null)
        {
            CY.LogError("[BulletEntity] 缺少 Rigidbody2D 组件。"); // 输出刚体缺失错误
        }

        if (_collider2D == null)
        {
            CY.LogError("[BulletEntity] 缺少 Collider2D 组件。"); // 输出碰撞体缺失错误
        }

        if (spriteRenderer == null)
        {
            CY.LogWarning("[BulletEntity] 缺少 SpriteRenderer 组件，可能无法显示子弹。"); // 输出渲染器缺失警告
        }
    }

    /// <summary>
    /// 实体显示：应用出生数据与初始速度。
    /// </summary>
    /// <param name="userData">显示时传入的数据。</param>
    protected override void OnEntityShow(object userData) // 实体显示入口
    {
        base.OnEntityShow(userData); // 调用父类显示
        ReleaseSpawnUserData(); // 显示前回收旧数据
        ResetRuntimeState(); // 重置运行时状态

        if (userData is BulletSpawnUserData spawnData)
        {
            _spawnUserData = spawnData; // 缓存当前生成数据引用
            ApplySpawnData(spawnData); // 应用生成数据
        }
        else if (userData != null)
        {
            CY.LogWarning("[BulletEntity] UserData 类型不正确，已使用默认参数。"); // 输出类型不匹配警告
        }

        ApplySpawnVelocity(); // 应用初始速度
    }

    /// <summary>
    /// 实体隐藏：清理状态并停止运动。
    /// </summary>
    protected override void OnEntityHide() // 实体隐藏入口
    {
        ReleaseSpawnUserData(); // 回收生成数据
        ResetPhysicsState(); // 重置物理状态
        _isActive = false; // 关闭激活标记
        _canHit = false; // 关闭命中
        _owner = null; // 清理拥有者引用
        _hasHit = false; // 清理命中标记
        base.OnEntityHide(); // 调用父类隐藏
    }

    /// <summary>
    /// 实体回收：清理状态并停止运动。
    /// </summary>
    protected override void OnEntityRecycle() // 实体回收入口
    {
        ReleaseSpawnUserData(); // 回收生成数据
        ResetPhysicsState(); // 重置物理状态
        _isActive = false; // 关闭激活标记
        _canHit = false; // 关闭命中
        _owner = null; // 清理拥有者引用
        _hasHit = false; // 清理命中标记
        base.OnEntityRecycle(); // 调用父类回收
    }

    /// <summary>
    /// 回收当前持有的生成数据对象。
    /// </summary>
    private void ReleaseSpawnUserData() // 生成数据回收入口
    {
        if (_spawnUserData == null)
        {
            return; // 未持有数据时返回
        }

        ReturnSpawnUserData(_spawnUserData); // 归还到对象池
        _spawnUserData = null; // 清理引用
    }

    /// <summary>
    /// 实体暂停：记录并停止速度。
    /// </summary>
    protected override void OnEntityPause() // 实体暂停入口
    {
        base.OnEntityPause(); // 调用父类暂停
        CachePauseVelocity(); // 缓存暂停速度
    }

    /// <summary>
    /// 实体恢复：恢复暂停前速度。
    /// </summary>
    protected override void OnEntityResume() // 实体恢复入口
    {
        base.OnEntityResume(); // 调用父类恢复
        RestorePauseVelocity(); // 恢复暂停速度
    }

    /// <summary>
    /// 固定帧更新：推进寿命与运动。
    /// </summary>
    /// <param name="deltaTime">固定帧间隔。</param>
    protected override void OnEntityFixedUpdate(float deltaTime) // 固定帧更新入口
    {
        base.OnEntityFixedUpdate(deltaTime); // 调用父类固定更新
        if (!_isActive)
        {
            return; // 未激活时直接返回
        }

        TickLifetime(deltaTime); // 推进存活时间
        TickMovement(deltaTime); // 推进移动
    }

    /// <summary>
    /// 重置运行时状态到默认值。
    /// </summary>
    private void ResetRuntimeState() // 运行时状态重置入口
    {
        _speed = _defaultSpeed; // 重置速度
        _lifeTime = _defaultLifetime; // 重置存活时间
        _damage = _defaultDamage; // 重置伤害
        _camp = UnitCamp.Neutral; // 重置阵营
        _owner = null; // 清理拥有者
        _direction = _defaultDirection; // 重置方向
        _lifeTimer = _lifeTime; // 重置剩余时间
        _isActive = true; // 打开激活标记
        _canHit = true; // 允许命中
        _hasHit = false; // 清理命中标记
        _isCrit = false; // 清理暴击标记
        _hasPauseVelocity = false; // 清理暂停速度标记
        ResetPhysicsState(); // 重置物理状态
    }

    /// <summary>
    /// 应用生成数据到运行时状态。
    /// </summary>
    /// <param name="data">生成数据。</param>
    private void ApplySpawnData(BulletSpawnUserData data) // 生成数据应用入口
    {
        SetSpawnPosition(data.Position); // 设置出生位置
        SetDirection(data.Direction); // 设置方向

        if (data.Speed > 0f)
        {
            _speed = data.Speed; // 覆盖速度
        }

        if (data.Lifetime > 0f)
        {
            _lifeTime = data.Lifetime; // 覆盖存活时间
        }

        if (data.Damage > 0)
        {
            _damage = data.Damage; // 覆盖伤害
        }

        _camp = data.Camp; // 写入阵营
        _owner = data.Owner; // 写入拥有者
        _isCrit = data.IsCrit; // 写入暴击标记
        _lifeTimer = _lifeTime; // 刷新剩余时间
    }

    /// <summary>
    /// 设置出生位置。
    /// </summary>
    /// <param name="position">目标位置。</param>
    private void SetSpawnPosition(Vector2 position) // 位置设置入口
    {
        if (_cachedTransform != null)
        {
            _cachedTransform.position = new Vector3(position.x, position.y, _cachedTransform.position.z); // Transform 位置赋值
        }
    }

    /// <summary>
    /// 设置方向并可选对齐朝向。
    /// </summary>
    /// <param name="direction">目标方向。</param>
    private void SetDirection(Vector2 direction) // 方向设置入口
    {
        if (direction.sqrMagnitude <= 0f)
        {
            direction = _defaultDirection.sqrMagnitude > 0f ? _defaultDirection : Vector2.right; // 使用默认方向兜底
        }

        direction.Normalize(); // 归一化方向
        _direction = direction; // 写入方向

        if (_alignToDirection && _cachedTransform != null)
        {
            _cachedTransform.right = new Vector3(direction.x, direction.y, 0f); // 对齐朝向
        }
    }

    /// <summary>
    /// 应用初始速度（在生成后立即生效）。
    /// </summary>
    private void ApplySpawnVelocity() // 初始速度应用入口
    {
        if (_rigidbody2D == null)
        {
            return; // 无刚体时直接返回
        }

        if (_useRigidbodyVelocity)
        {
            _rigidbody2D.velocity = _direction * _speed; // 使用速度驱动
        }
        else
        {
            _rigidbody2D.velocity = Vector2.zero; // 使用 MovePosition 时清零速度
        }
    }

    /// <summary>
    /// 推进存活时间并在超时后回收。
    /// </summary>
    /// <param name="deltaTime">固定帧间隔。</param>
    private void TickLifetime(float deltaTime) // 生命周期推进入口
    {
        if (_lifeTime <= 0f)
        {
            return; // 未启用生命周期时返回
        }

        _lifeTimer -= deltaTime; // 递减剩余时间
        if (_lifeTimer > 0f)
        {
            return; // 仍存活时返回
        }

        RecycleSelf(); // 超时回收自身
    }

    /// <summary>
    /// 推进移动（速度或 MovePosition）。
    /// </summary>
    /// <param name="deltaTime">固定帧间隔。</param>
    private void TickMovement(float deltaTime) // 移动推进入口
    {
        if (_rigidbody2D == null)
        {
            return; // 无刚体时返回
        }

        if (_speed <= 0f)
        {
            return; // 无速度时返回
        }

        if (_direction.sqrMagnitude <= 0f)
        {
            return; // 无方向时返回
        }

        if (_useRigidbodyVelocity)
        {
            _rigidbody2D.velocity = _direction * _speed; // 速度驱动移动
            return; // 已处理则返回
        }

        var nextPos = _rigidbody2D.position + _direction * (_speed * deltaTime); // 计算目标位置
        _rigidbody2D.velocity = Vector2.zero; // 清零速度避免干扰
        _rigidbody2D.MovePosition(nextPos); // MovePosition 移动
    }

    /// <summary>
    /// 缓存暂停前速度并停止运动。
    /// </summary>
    private void CachePauseVelocity() // 暂停速度缓存入口
    {
        if (_rigidbody2D == null)
        {
            return; // 无刚体时返回
        }

        _cachedVelocityBeforePause = _rigidbody2D.velocity; // 缓存速度
        _hasPauseVelocity = true; // 标记已缓存
        _rigidbody2D.velocity = Vector2.zero; // 清零速度
    }

    /// <summary>
    /// 恢复暂停前速度。
    /// </summary>
    private void RestorePauseVelocity() // 暂停速度恢复入口
    {
        if (_rigidbody2D == null)
        {
            return; // 无刚体时返回
        }

        if (!_hasPauseVelocity)
        {
            return; // 未缓存时返回
        }

        if (_useRigidbodyVelocity)
        {
            _rigidbody2D.velocity = _cachedVelocityBeforePause; // 恢复速度
        }

        _hasPauseVelocity = false; // 清理缓存标记
    }

    /// <summary>
    /// 重置物理状态，避免复用残留速度。
    /// </summary>
    private void ResetPhysicsState() // 物理状态重置入口
    {
        if (_rigidbody2D == null)
        {
            return; // 无刚体时返回
        }

        _rigidbody2D.velocity = Vector2.zero; // 清零线速度
        _rigidbody2D.angularVelocity = 0f; // 清零角速度
    }


    /// <summary>
    /// 通过实体系统回收自身。
    /// </summary>
    private void RecycleSelf() // 自身回收入口
    {
        if (Id <= 0)
        {
            return; // 无效 Id 时返回
        }

        _isActive = false; // 关闭激活标记
        _canHit = false; // 关闭命中
        CY.Entity.RecycleEntity(Id); // 交给实体系统回收
    }

    /// <summary>
    /// 触发器命中回调。
    /// </summary>
    /// <param name="other">命中碰撞体。</param>
    private void OnTriggerEnter2D(Collider2D other) // 触发器命中入口
    {
        HandleCollision(other); // 统一处理碰撞
    }

    /// <summary>
    /// 碰撞体命中回调。
    /// </summary>
    /// <param name="collision">碰撞信息。</param>
    private void OnCollisionEnter2D(Collision2D collision) // 碰撞命中入口
    {
        HandleCollision(collision.collider); // 统一处理碰撞
    }

    /// <summary>
    /// 统一处理碰撞与命中逻辑。
    /// </summary>
    /// <param name="other">命中碰撞体。</param>
    private void HandleCollision(Collider2D other) // 命中处理入口
    {
        if (!_canHit)
        {
            return; // 不允许命中时返回
        }

        if (other == null)
        {
            return; // 空碰撞体时返回
        }

        if (_collider2D != null && other == _collider2D)
        {
            return; // 忽略自身碰撞体
        }

        if (_hitMask.value != 0 && ((_hitMask.value & (1 << other.gameObject.layer)) == 0))
        {
            return; // 命中层不匹配时返回
        }

        if (TryGetUnitEntity(other, out var unit))
        {
            if (!CanHitUnit(unit))
            {
                return; // 不可命中目标时返回
            }

            var hitPoint = GetHitPoint(other); // 计算命中点
            OnHitUnit(unit, hitPoint); // 命中单位处理
            MarkHit(); // 标记命中
            return; // 命中单位后返回
        }

        var otherHitPoint = GetHitPoint(other); // 计算命中点
        OnHitOther(other, otherHitPoint); // 命中其他物体处理
        MarkHit(); // 标记命中
    }

    /// <summary>
    /// 尝试从碰撞体上获取 UnitEntity。
    /// </summary>
    /// <param name="other">碰撞体。</param>
    /// <param name="unit">输出单位实体。</param>
    private bool TryGetUnitEntity(Collider2D other, out UnitEntity unit) // 单位获取入口
    {
        if (other.TryGetComponent<UnitEntity>(out unit))
        {
            return true; // 直接命中单位
        }

        unit = other.GetComponentInParent<UnitEntity>(); // 向父级查找单位
        return unit != null; // 返回查找结果
    }

    /// <summary>
    /// 判断当前阵营是否允许攻击目标阵营。
    /// </summary>
    /// <param name="attackerCamp">攻击方阵营。</param>
    /// <param name="targetCamp">目标方阵营。</param>
    private bool CanAttackCamp(UnitCamp attackerCamp, UnitCamp targetCamp) // 阵营攻击规则入口
    {
        if (attackerCamp == UnitCamp.Neutral)
        {
            return false; // 中立不能攻击任何单位
        }

        if (attackerCamp == UnitCamp.Player || attackerCamp == UnitCamp.Employee)
        {
            return targetCamp == UnitCamp.Enemy; // 玩家/员工只能攻击敌人
        }

        if (attackerCamp == UnitCamp.Enemy)
        {
            return targetCamp == UnitCamp.Player || targetCamp == UnitCamp.Employee || targetCamp == UnitCamp.Neutral; // 敌人可攻击玩家/员工/中立
        }

        return false; // 其他情况默认不攻击
    }

    /// <summary>
    /// 判断是否允许命中目标单位。
    /// </summary>
    /// <param name="target">目标单位。</param>
    protected virtual bool CanHitUnit(UnitEntity target) // 命中校验入口
    {
        if (target == null)
        {
            return false; // 目标为空时返回
        }

        if (target.LifeState == UnitLifeState.Dead)
        {
            return false; // 目标死亡时返回
        }

        if (!CanAttackCamp(_camp, target.Camp))
        {
            return false; // 阵营规则不允许时返回
        }

        if (_owner != null && _owner == target)
        {
            return false; // 忽略自身目标
        }

        return true; // 允许命中
    }

    /// <summary>
    /// 命中单位处理（默认执行伤害）。
    /// </summary>
    /// <param name="target">目标单位。</param>
    /// <param name="hitPoint">命中点。</param>
    protected virtual void OnHitUnit(UnitEntity target, Vector2 hitPoint) // 命中单位入口
    {
        if (_damage <= 0)
        {
            return; // 无伤害时返回
        }

        target.TryApplyDamage(_damage, _isCrit); // 施加伤害
    }

    /// <summary>
    /// 命中非单位物体处理（默认空实现）。
    /// </summary>
    /// <param name="other">碰撞体。</param>
    /// <param name="hitPoint">命中点。</param>
    protected virtual void OnHitOther(Collider2D other, Vector2 hitPoint) // 命中其他物体入口
    {
    }

    /// <summary>
    /// 获取命中点（使用 Collider2D.ClosestPoint）。
    /// </summary>
    /// <param name="other">碰撞体。</param>
    private Vector2 GetHitPoint(Collider2D other) // 命中点计算入口
    {
        if (other == null)
        {
            return Vector2.zero; // 空碰撞体返回零点
        }

        if (_cachedTransform == null)
        {
            return Vector2.zero; // 无 Transform 时返回零点
        }

        return other.ClosestPoint(_cachedTransform.position); // 计算最近点
    }

    /// <summary>
    /// 标记命中并根据配置回收。
    /// </summary>
    private void MarkHit() // 命中标记入口
    {
        _hasHit = true; // 写入命中标记
        if (_singleHit)
        {
            _canHit = false; // 单次命中后禁止继续命中
        }

        if (_recycleOnHit)
        {
            RecycleSelf(); // 命中后回收
        }
    }
}
