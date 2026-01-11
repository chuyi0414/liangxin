using System;
using CYFramework;
using CYFramework.Core.Entity;
using UnityEngine;

/// <summary>
/// 单位阵营（用于敌我识别与筛选）。
/// </summary>
public enum UnitCamp
{
    /// <summary>中立。</summary>
    Neutral = 0,
    /// <summary>玩家（老板单位）。</summary>
    Player = 1,
    /// <summary>员工（友方单位）。</summary>
    Employee = 2,
    /// <summary>敌人（敌方单位）。</summary>
    Enemy = 3
}

/// <summary>
/// 单位状态（基础生命状态）。
/// </summary>
public enum UnitLifeState
{
    /// <summary>存活。</summary>
    Alive = 0,
    /// <summary>濒死/不可行动（用于员工“自闭”表现）。</summary>
    Downed = 1,
    /// <summary>死亡。</summary>
    Dead = 2
}

/// <summary>
/// 单位基础属性（不含临时 Buff/DeBuff）。
/// </summary>
[Serializable]
public struct UnitStats
{
    /// <summary>最大生命值（>0）。</summary>
    public int MaxHp;
    /// <summary>攻击力（>=0）。</summary>
    public int Attack;
    /// <summary>防御力（>=0）。</summary>
    public int Defense;
    /// <summary>固定防御穿透值（>=0）。</summary>
    public int DefensePenetration;
    /// <summary>百分比防御穿透（0-1）。</summary>
    public float DefensePenetrationRate;
    /// <summary>暴击率（0-1）。</summary>
    public float CritRate;
    /// <summary>闪避率（0-1）。</summary>
    public float DodgeRate;
    /// <summary>是否远程单位。</summary>
    public bool IsRanged;
    /// <summary>移动速度（>=0）。</summary>
    public float MoveSpeed;
    /// <summary>攻击距离（>=0）。</summary>
    public float AttackRange;
    /// <summary>攻击间隔（秒，>0）。</summary>
    public float AttackInterval;
}

/// <summary>
/// 通用单位基类（玩家/员工/敌人通用）。
/// 仅提供基础属性，复杂行为由派生类实现。
/// </summary>
public abstract class UnitEntity : EntityBase
{
    /// <summary>策划配置表 ID。</summary>
    [SerializeField] private int _unitConfigId;
    /// <summary>单位编码（如 F01/E01）。</summary>
    [SerializeField] private string _unitCode;
    /// <summary>单位名称。</summary>
    [SerializeField] private string _unitName;
    /// <summary>单位阵营。</summary>
    [SerializeField] private UnitCamp _camp = UnitCamp.Neutral;
    /// <summary>单位状态（基础生命状态）。</summary>
    [SerializeField] private UnitLifeState _lifeState = UnitLifeState.Alive;
    /// <summary>单位等级（默认 1）。</summary>
    [SerializeField] private int _level = 1;
    /// <summary>单位基础属性。</summary>
    [SerializeField] private UnitStats _baseStats;
    /// <summary>当前生命值（运行时）。</summary>
    [SerializeField] private int _currentHp;
    /// <summary>是否已派发移除事件（避免重复）。</summary>
    private bool _hasDespawnedEvent;
    /// <summary>攻击冷却计时器（秒）。</summary>
    private float _attackCooldown;
    /// <summary>子弹预制体路径数组（Resources 相对路径，不含 .prefab）。</summary>
    private string[] _bulletPrefabPaths; // 子弹路径数组
    /// <summary>子弹选择规则。</summary>
    private BulletSelectRule _bulletSelectRule; // 子弹选择规则
    /// <summary>顺序轮播索引。</summary>
    private int _bulletSelectIndex; // 轮播索引
    /// <summary>子弹飞行速度（允许为 0，表示使用子弹默认速度）。</summary>
    private float _bulletSpeed; // 子弹速度
    /// <summary>Transform 缓存（避免高频访问开销）。</summary>
    private Transform _cachedTransform; // Transform 缓存引用
    /// <summary>碰撞体缓存（用于距离计算与命中点推导）。</summary>
    private Collider2D _cachedCollider2D; // 碰撞体缓存引用
    /// <summary>
    /// 最近一次 OnEntityShow 的帧数：用于调试“出生瞬间受伤”等同帧/跨帧问题。
    /// </summary>
    private int _shownFrame; // 显示帧数缓存
    /// <summary>
    /// 是否已输出“出生早期受伤”的调试日志（避免刷屏）。
    /// </summary>
    private bool _hasLoggedEarlyDamage; // 早期受伤日志标记

    /// <summary>策划配置表 ID（只读）。</summary>
    public int UnitConfigId => _unitConfigId;
    /// <summary>单位编码（只读）。</summary>
    public string UnitCode => _unitCode;
    /// <summary>单位名称（只读）。</summary>
    public string UnitName => _unitName;
    /// <summary>单位阵营（只读）。</summary>
    public UnitCamp Camp => _camp;
    /// <summary>单位状态（只读）。</summary>
    public UnitLifeState LifeState => _lifeState;
    /// <summary>单位等级（只读）。</summary>
    public int Level => _level;
    /// <summary>单位基础属性（只读）。</summary>
    public UnitStats BaseStats => _baseStats;
    /// <summary>当前生命值（只读）。</summary>
    public int CurrentHp => _currentHp;
    /// <summary>最大生命值（只读）。</summary>
    public int MaxHp => _baseStats.MaxHp;
    /// <summary>攻击冷却剩余时间（只读）。</summary>
    public float AttackCooldown => _attackCooldown;
    /// <summary>子弹飞行速度（只读，0 表示使用子弹默认速度）。</summary>
    public float BulletSpeed => _bulletSpeed; // 子弹速度只读访问
    /// <summary>Transform 缓存（只读）。</summary>
    public Transform CachedTransform => _cachedTransform; // 对外只读 Transform
    /// <summary>碰撞体缓存（只读）。</summary>
    public Collider2D CachedCollider2D => _cachedCollider2D; // 对外只读碰撞体

    /// <summary>
    /// 实体初始化：缓存组件引用。
    /// </summary>
    /// <param name="userData">初始化传入的数据。</param>
    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData); // 调用父类初始化
        _cachedTransform = transform; // 缓存 Transform
        _cachedCollider2D = GetComponent<Collider2D>(); // 缓存碰撞体组件
    }

    /// <summary>
    /// 应用基础数据（用于数据表初始化，避免在外部直接改字段）。
    /// </summary>
    protected void ApplyBaseData(int configId, string code, string name, UnitCamp camp, UnitLifeState lifeState, int level, UnitStats stats)
    {
        _unitConfigId = configId;
        _unitCode = code;
        _unitName = name;
        _camp = camp;
        _lifeState = lifeState;
        _level = level < 1 ? 1 : level;
        _baseStats = stats;
    }

    /// <summary>
    /// 应用子弹数组配置（来自数据表）。
    /// </summary>
    /// <param name="selectRule">子弹选择规则。</param>
    /// <param name="prefabPaths">子弹预制体路径数组。</param>
    protected void ApplyBulletArrayConfig(BulletSelectRule selectRule, string[] prefabPaths) // 子弹数组配置应用入口
    {
        _bulletSelectRule = selectRule; // 写入选择规则
        _bulletSelectIndex = 0; // 重置顺序轮播索引
        if (prefabPaths == null || prefabPaths.Length == 0)
        {
            _bulletPrefabPaths = Array.Empty<string>(); // 路径无效时写入空数组
            return; // 直接退出
        }

        _bulletPrefabPaths = prefabPaths; // 写入路径数组
    }

    /// <summary>
    /// 应用子弹飞行速度（来自配置表）。
    /// </summary>
    /// <param name="bulletSpeed">子弹速度（允许为 0，表示使用子弹默认速度）。</param>
    protected void ApplyBulletSpeed(float bulletSpeed)
    {
        if (bulletSpeed < 0f)
        {
            CY.LogWarning("[UnitEntity] 子弹速度小于 0，已回退为 0（使用子弹默认速度）。"); // 输出速度纠正警告
            _bulletSpeed = 0f; // 回退为 0
            return;
        }

        _bulletSpeed = bulletSpeed; // 写入子弹速度
    }

    /// <summary>
    /// 实体显示：重置生命并派发生成/血量事件。
    /// </summary>
    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);
        _shownFrame = Time.frameCount; // 记录本次显示的帧数（用于调试）
        _hasLoggedEarlyDamage = false; // 显示时重置日志标记，便于每次生成都能定位一次
        _hasDespawnedEvent = false;
        _attackCooldown = 0f;
        ResetHpToMax();
        PostUnitSpawnedEvent();
        PostHpChangedEvent();
    }

    /// <summary>
    /// 实体隐藏：派发移除事件（用于回收血条）。
    /// </summary>
    protected override void OnEntityHide()
    {
        PostUnitDespawnedEvent();
        base.OnEntityHide();
    }

    /// <summary>
    /// 实体回收：兜底派发移除事件（避免隐藏流程未走）。
    /// </summary>
    protected override void OnEntityRecycle()
    {
        PostUnitDespawnedEvent();
        base.OnEntityRecycle();
    }

    /// <summary>
    /// 单位通用 Update：推进攻击冷却（不依赖移动状态）。
    /// </summary>
    /// <param name="deltaTime">帧间隔。</param>
    protected override void OnEntityUpdate(float deltaTime)
    {
        base.OnEntityUpdate(deltaTime);
        TickAttackCooldown(deltaTime);
    }

    /// <summary>
    /// 重置生命值为最大生命值。
    /// </summary>
    public void ResetHpToMax()
    {
        var maxHp = _baseStats.MaxHp;
        if (_lifeState == UnitLifeState.Dead)
        {
            _currentHp = 0;
            return;
        }

        _currentHp = maxHp > 0 ? maxHp : 0;
    }

    /// <summary>
    /// 尝试应用伤害（damage 为最终伤害值）。
    /// </summary>
    /// <param name="damage">最终伤害值（>0 才生效）。</param>
    /// <param name="isCrit">是否暴击。</param>
    public bool TryApplyDamage(int damage, bool isCrit = false)
    {
        if (damage <= 0)
        {
            return false;
        }

        if (_lifeState == UnitLifeState.Dead)
        {
            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_hasLoggedEarlyDamage) // 仅输出一次判定
        {
            var frameDelta = Time.frameCount - _shownFrame; // 计算距离显示的帧差
            if (frameDelta >= 0 && frameDelta <= 5 && _camp == UnitCamp.Employee) // 仅关注员工在显示后的早期受伤
            {
                _hasLoggedEarlyDamage = true; // 标记已输出，避免刷屏
                var beforeHp = _currentHp; // 记录受伤前生命
                var afterHp = beforeHp - damage; // 计算受伤后生命（仅用于日志展示）
                var pos = _cachedTransform != null ? _cachedTransform.position : transform.position; // 获取当前世界坐标
                CY.LogWarning($"[DamageDebug] 员工早期受伤：damage={damage}, isCrit={isCrit}, hp={beforeHp}->{afterHp}, frameDelta={frameDelta}, pos=({pos.x:F3},{pos.y:F3}), code={_unitCode}, id={Id}\nStack:\n{System.Environment.StackTrace}"); // 输出调用栈帮助定位来源（子弹/近战）
            }
        }
#endif

        var newHp = _currentHp - damage;
        SetCurrentHp(newHp);
        PostDamagePopupEvent(damage, isCrit);

        if (_currentHp <= 0)
        {
            SetLifeState(UnitLifeState.Dead);
        }

        return true;
    }

    /// <summary>
    /// 尝试治疗生命值。
    /// </summary>
    /// <param name="amount">治疗量（>0 才生效）。</param>
    public bool TryHeal(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (_lifeState == UnitLifeState.Dead)
        {
            return false;
        }

        var newHp = _currentHp + amount;
        SetCurrentHp(newHp);
        return true;
    }

    /// <summary>
    /// 尝试攻击目标（按 AttackInterval 冷却）。
    /// </summary>
    /// <param name="target">攻击目标。</param>
    /// <param name="isCrit">是否暴击。</param>
    public bool TryAttackTarget(UnitEntity target, bool isCrit = false)
    {
        if (target == null)
        {
            return false;
        }

        if (_lifeState == UnitLifeState.Dead || target.LifeState == UnitLifeState.Dead)
        {
            return false;
        }

        if (_attackCooldown > 0f)
        {
            return false;
        }

        var damage = _baseStats.Attack;
        if (damage <= 0)
        {
            return false;
        }
        
        var attackSuccess = false; // 攻击成功标记
        if (_baseStats.IsRanged)
        {
            attackSuccess = TryFireBullet(target, damage, isCrit); // 远程攻击走发射子弹
        }
        else
        {
            attackSuccess = target.TryApplyDamage(damage, isCrit); // 近战攻击直接伤害
        }

        if (!attackSuccess)
        {
            return false; // 攻击未成功时返回失败
        }

        var interval = _baseStats.AttackInterval;
        _attackCooldown = interval > 0f ? interval : 0f;
        return true;
    }

    /// <summary>
    /// 尝试消耗攻击冷却（用于非 UnitEntity 目标的攻击）。
    /// </summary>
    /// <param name="damage">本次攻击伤害（用于校验）。</param>
    protected bool TryConsumeAttackCooldown(int damage) // 攻击冷却消耗入口
    {
        if (_lifeState == UnitLifeState.Dead)
        {
            return false; // 死亡时不可攻击
        }

        if (_attackCooldown > 0f)
        {
            return false; // 冷却未结束时返回
        }

        if (damage <= 0)
        {
            return false; // 伤害无效时返回
        }

        var interval = _baseStats.AttackInterval; // 读取攻击间隔
        _attackCooldown = interval > 0f ? interval : 0f; // 写入冷却时间
        return true; // 返回可攻击
    }

    /// <summary>
    /// 按方向尝试远程攻击（按 AttackInterval 冷却）。
    /// </summary>
    /// <param name="direction">发射方向。</param>
    /// <param name="isCrit">是否暴击。</param>
    public bool TryAttackDirection(Vector2 direction, bool isCrit = false)
    {
        if (_lifeState == UnitLifeState.Dead)
        {
            return false; // 死亡时不可攻击
        }

        if (!_baseStats.IsRanged)
        {
            return false; // 非远程单位不允许方向攻击
        }

        if (_attackCooldown > 0f)
        {
            return false; // 冷却未结束时返回
        }

        var damage = _baseStats.Attack; // 读取攻击力
        if (damage <= 0)
        {
            return false; // 攻击力无效时返回
        }

        if (direction.sqrMagnitude <= 0f)
        {
            return false; // 方向无效时返回
        }

        if (!TryFireBulletByDirection(direction, damage, isCrit))
        {
            return false; // 发射失败时返回
        }

        var interval = _baseStats.AttackInterval; // 读取攻击间隔
        _attackCooldown = interval > 0f ? interval : 0f; // 写入冷却时间
        return true; // 返回攻击成功
    }

    /// <summary>
    /// 获取攻击发射起点（默认使用单位中心，子类可覆盖为武器/攻击点）。
    /// </summary>
    /// <param name="origin">输出攻击起点世界坐标。</param>
    protected virtual bool TryGetAttackOrigin(out Vector2 origin) // 攻击起点获取入口
    {
        var t = _cachedTransform != null ? _cachedTransform : transform; // 获取可用 Transform
        origin = (Vector2)t.position; // 输出世界坐标
        return true; // 默认起点始终有效
    }

    /// <summary>
    /// 远程攻击：生成子弹并朝目标方向发射。
    /// </summary>
    /// <param name="target">攻击目标。</param>
    /// <param name="damage">子弹伤害值。</param>
    /// <param name="isCrit">是否暴击。</param>
    private bool TryFireBullet(UnitEntity target, int damage, bool isCrit)
    {
        if (target == null)
        {
            return false; // 目标为空时返回失败
        }

        if (damage <= 0)
        {
            return false; // 伤害无效时返回失败
        }

        if (!TryGetAttackOrigin(out var origin))
        {
            return false; // 起点无效时返回失败
        }

        var targetPos = origin; // 初始化目标点为出生点
        if (target.CachedCollider2D != null)
        {
            targetPos = target.CachedCollider2D.ClosestPoint(origin); // 使用碰撞体最近点作为命中目标点
        }
        else if (target.CachedTransform != null)
        {
            targetPos = (Vector2)target.CachedTransform.position; // 使用目标 Transform 坐标
        }
        else
        {
            targetPos = (Vector2)target.transform.position; // 兜底使用目标 Transform 坐标
        }

        var direction = targetPos - origin; // 计算发射方向
        if (direction.sqrMagnitude <= 0f)
        {
            return false; // 方向无效时返回失败
        }

        return TryFireBulletByDirection(direction, damage, isCrit); // 使用方向发射子弹
    }

    /// <summary>
    /// 按方向发射子弹（内部使用，需确保方向有效）。
    /// </summary>
    /// <param name="direction">发射方向。</param>
    /// <param name="damage">子弹伤害值。</param>
    /// <param name="isCrit">是否暴击。</param>
    private bool TryFireBulletByDirection(Vector2 direction, int damage, bool isCrit)
    {
        var bulletPrefabPaths = _bulletPrefabPaths; // 读取子弹路径数组
        if (bulletPrefabPaths == null || bulletPrefabPaths.Length == 0)
        {
            CY.LogError("[UnitEntity] 远程攻击缺少子弹数组配置。"); // 输出子弹数组缺失错误
            return false; // 无路径时返回失败
        }

        var bulletPrefabPath = ResolveBulletPrefabPath(bulletPrefabPaths); // 解析要使用的子弹路径
        if (string.IsNullOrEmpty(bulletPrefabPath))
        {
            CY.LogError("[UnitEntity] 子弹路径为空，无法发射。"); // 输出空路径错误
            return false; // 路径无效时返回失败
        }

        direction.Normalize(); // 归一化方向向量

        if (!TryGetAttackOrigin(out var origin))
        {
            return false; // 起点无效时返回失败
        }

        var spawnData = CY.Pool.GetOrCreatePool<BulletSpawnUserData>(() => new BulletSpawnUserData()).Get(); // 通过框架对象池申请子弹生成数据
        spawnData.Position = origin; // 子弹出生位置
        spawnData.Direction = direction; // 子弹飞行方向
        spawnData.Speed = _bulletSpeed; // 子弹速度（0 表示使用子弹默认速度）
        spawnData.Lifetime = 0f; // 生命周期为 0 表示使用子弹默认寿命
        spawnData.Damage = damage; // 子弹伤害
        spawnData.Camp = _camp; // 子弹阵营
        spawnData.Owner = this; // 子弹拥有者
        spawnData.IsCrit = isCrit; // 是否暴击

        var bullet = CY.Entity.SpawnEntity<BulletEntity>(bulletPrefabPath, bulletPrefabPath, EntityGroup.Projectiles, spawnData); // 使用 userData 生成子弹
        if (bullet == null)
        {
            CY.Pool.GetOrCreatePool<BulletSpawnUserData>(() => new BulletSpawnUserData()).Return(spawnData); // 通过框架对象池回收数据
            return false; // 子弹生成失败时返回失败
        }

        return true; // 返回发射成功
    }

    /// <summary>
    /// 根据选择规则获取子弹预制体路径。
    /// </summary>
    /// <param name="bulletPrefabPaths">子弹路径数组。</param>
    private string ResolveBulletPrefabPath(string[] bulletPrefabPaths) // 子弹路径解析入口
    {
        if (bulletPrefabPaths == null || bulletPrefabPaths.Length == 0)
        {
            return string.Empty; // 数组无效时返回空
        }

        if (_bulletSelectRule == BulletSelectRule.Sequential)
        {
            if (_bulletSelectIndex < 0)
            {
                _bulletSelectIndex = 0; // 轮播索引下限保护
            }

            if (_bulletSelectIndex >= bulletPrefabPaths.Length)
            {
                _bulletSelectIndex = 0; // 轮播索引上限回绕
            }

            var path = bulletPrefabPaths[_bulletSelectIndex]; // 获取当前轮播路径
            _bulletSelectIndex++; // 递增轮播索引
            if (_bulletSelectIndex >= bulletPrefabPaths.Length)
            {
                _bulletSelectIndex = 0; // 超过上限时回绕到 0
            }

            return path; // 返回轮播路径
        }

        var index = UnityEngine.Random.Range(0, bulletPrefabPaths.Length); // 生成随机索引
        return bulletPrefabPaths[index]; // 返回随机路径
    }

    /// <summary>
    /// 设置当前生命值并派发变化事件。
    /// </summary>
    /// <param name="newHp">新的生命值。</param>
    private void SetCurrentHp(int newHp)
    {
        var maxHp = _baseStats.MaxHp;
        if (newHp < 0)
        {
            newHp = 0;
        }

        if (maxHp > 0 && newHp > maxHp)
        {
            newHp = maxHp;
        }

        if (_currentHp == newHp)
        {
            return;
        }

        _currentHp = newHp;
        PostHpChangedEvent();
    }

    /// <summary>
    /// 推进攻击冷却计时。
    /// </summary>
    /// <param name="deltaTime">帧间隔。</param>
    private void TickAttackCooldown(float deltaTime)
    {
        if (_attackCooldown <= 0f)
        {
            return;
        }

        _attackCooldown -= deltaTime;
        if (_attackCooldown < 0f)
        {
            _attackCooldown = 0f;
        }
    }

    /// <summary>
    /// 切换生命状态并派发事件。
    /// </summary>
    /// <param name="newState">新的生命状态。</param>
    private void SetLifeState(UnitLifeState newState)
    {
        if (_lifeState == newState)
        {
            return;
        }

        var oldState = _lifeState;
        _lifeState = newState;
        var evt = new UnitLifeStateChangedEvent
        {
            Unit = this,
            OldState = oldState,
            NewState = newState
        };
        CY.Event.Post(ref evt);
    }

    /// <summary>
    /// 派发单位生成事件。
    /// </summary>
    private void PostUnitSpawnedEvent()
    {
        var evt = new UnitSpawnedEvent
        {
            Unit = this,
            CurrentHp = _currentHp,
            MaxHp = _baseStats.MaxHp
        };
        CY.Event.Post(ref evt);
    }

    /// <summary>
    /// 派发单位移除事件（带重复保护）。
    /// </summary>
    private void PostUnitDespawnedEvent()
    {
        if (_hasDespawnedEvent)
        {
            return;
        }

        _hasDespawnedEvent = true;
        var evt = new UnitDespawnedEvent
        {
            Unit = this
        };
        CY.Event.Post(ref evt);
    }

    /// <summary>
    /// 派发生命变化事件。
    /// </summary>
    private void PostHpChangedEvent()
    {
        var evt = new UnitHpChangedEvent
        {
            Unit = this,
            CurrentHp = _currentHp,
            MaxHp = _baseStats.MaxHp
        };
        CY.Event.Post(ref evt);
    }

    /// <summary>
    /// 派发伤害飘字事件。
    /// </summary>
    /// <param name="damage">伤害数值。</param>
    /// <param name="isCrit">是否暴击。</param>
    private void PostDamagePopupEvent(int damage, bool isCrit)
    {
        var evt = new UnitDamagePopupEvent
        {
            Unit = this,
            Damage = damage,
            IsCrit = isCrit
        };
        CY.Event.Post(ref evt);
    }
}
