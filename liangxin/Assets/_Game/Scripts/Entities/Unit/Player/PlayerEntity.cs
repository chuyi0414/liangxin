using CYFramework;
using CYFramework.Core.Entity;
using UnityEngine;
/// <summary>
/// 玩家预显示数据：用于在激活前设置出生位置。
/// </summary>
public struct PlayerPreShowData // 玩家预显示数据结构
{
    /// <summary>是否提供了有效位置。</summary>
    public bool HasPosition; // 位置有效标记
    /// <summary>预显示位置（世界坐标）。</summary>
    public Vector3 Position; // 预显示位置
}
/// <summary>
/// 老板单位实体（继承通用 UnitEntity）。
/// 仅作为类型标识，具体行为由后续系统扩展。
/// </summary>
[RequireComponent(typeof(HybridNavigationAgent))]
[EntityPrefab("Prefabs/Entities/Unit/Player/PlayerEntity", "Players", "Players")]
public sealed class PlayerEntity : UnitEntity, IEntityPreShowData<PlayerPreShowData> // 玩家实体定义
{
    /// <summary>缓存 Transform，减少高频访问开销。</summary>
    private Transform _cachedTransform;
    /// <summary>2D 刚体组件，用于物理移动与碰撞。</summary>
    private Rigidbody2D _rigidbody2D;
    /// <summary>攻击按键（默认空格）。</summary>
    [SerializeField] private KeyCode _attackKey = KeyCode.Space; // 攻击按键配置
    /// <summary>是否允许长按连续发射。</summary>
    [SerializeField] private bool _allowHoldAttack = true; // 长按攻击开关
    /// <summary>主摄像机缓存。</summary>
    private Camera _cachedCamera; // 摄像机缓存
    /// <summary>是否已输出缺少摄像机的日志。</summary>
    private bool _hasLoggedMissingCamera; // 缺少摄像机日志标记
    /// <summary>是否显示攻击范围 Gizmos。</summary>
    [SerializeField] private bool _showAttackRangeGizmos = true; // 攻击范围显示开关
    /// <summary>是否始终显示攻击范围（不选中也显示）。</summary>
    [SerializeField] private bool _showAttackRangeAlways = true; // 是否常显攻击范围
    /// <summary>攻击范围 Gizmos 颜色。</summary>
    [SerializeField] private Color _attackRangeGizmosColor = new Color(0.2f, 1f, 0.2f, 0.8f); // 攻击范围颜色
    [Header("攻击配置")]
    [SerializeField] private GameObject AttackLocation; // 攻击点
    /// <summary>攻击点 Transform 缓存。</summary>
    private Transform _attackLocationTransform; // 攻击点 Transform 缓存
    /// <summary>
    /// 拾取范围
    /// </summary>
    [SerializeField] private CircleCollider2D _selectionRange; // 拾取范围碰撞体
    /// <summary>黑心点击检测命中缓存（避免运行时分配）。</summary>
    private static readonly Collider2D[] _blackHeartClickHits = new Collider2D[16]; // 黑心点击命中缓存
    /// <summary>
    /// 初始化时缓存组件，避免在 Update 中重复查询。
    /// </summary>
    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        _cachedTransform = transform;
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _attackLocationTransform = AttackLocation != null ? AttackLocation.transform : null; // 缓存攻击点 Transform
        _cachedCamera = Camera.main; // 缓存主摄像机
        if (_cachedCamera == null)
        {
            _cachedCamera = FindObjectOfType<Camera>(); // 回退获取场景中的任意摄像机（低频）
        }
    }

    /// <summary>
    /// 应用预显示数据（激活前调用）。
    /// </summary>
    /// <param name="data">预显示数据（引用传递）。</param>
    public void ApplyPreShowData(ref PlayerPreShowData data) // 预显示数据应用入口
    {
        if (!data.HasPosition)
        {
            return; // 无有效位置时直接退出
        }

        var cachedTransform = _cachedTransform != null ? _cachedTransform : transform; // 获取可用 Transform
        if (_rigidbody2D != null)
        {
            cachedTransform.position = new Vector3(data.Position.x, data.Position.y, cachedTransform.position.z); // 同步 Transform 坐标并保持 Z
            _rigidbody2D.position = new Vector2(data.Position.x, data.Position.y); // 使用刚体设置位置
            _rigidbody2D.velocity = Vector2.zero; // 清空线速度
            _rigidbody2D.angularVelocity = 0f; // 清空角速度
            return; // 使用刚体时直接返回
        }

        cachedTransform.position = new Vector3(data.Position.x, data.Position.y, cachedTransform.position.z); // 设置位置并保持 Z
    }

    /// <summary>
    /// 显示时应用玩家数据表行，作为默认初始数据。
    /// </summary>
    protected override void OnEntityShow(object userData)
    {
        var row = userData as PlayerUnitRow;
        if (row == null)
        {
            base.OnEntityShow(userData);
            return;
        }

        var stats = new UnitStats
        {
            MaxHp = row.MaxHp,
            Attack = row.Attack,
            Defense = row.Defense,
            DefensePenetration = row.DefensePenetration,
            DefensePenetrationRate = row.DefensePenetrationRate,
            CritRate = row.CritRate,
            DodgeRate = row.DodgeRate,
            IsRanged = row.IsRanged,
            MoveSpeed = row.MoveSpeed,
            AttackRange = row.AttackRange,
            AttackInterval = row.AttackInterval
        };

        ApplyBaseData(row.Id, row.Code, row.Name, row.Camp, row.LifeState, row.Level, stats);
        ApplyBulletPrefabPath(row.BulletPrefabPath); // 应用子弹预制体路径
        ApplyBulletSpeed(row.BulletSpeed); // 应用子弹飞行速度
        base.OnEntityShow(userData);
    }

    /// <summary>
    /// WASD 移动控制（键盘输入）；移动端/手柄需替换输入来源。
    /// 边界：无输入或速度<=0时不移动，斜向会归一化避免加速。
    /// 物理：使用 Rigidbody2D.MovePosition，保证与场景碰撞体正常交互且不推动静态物体。
    /// </summary>
    protected override void OnEntityUpdate(float deltaTime)
    {
        base.OnEntityUpdate(deltaTime);

        if (_rigidbody2D != null)
        {
            float horizontal = 0f; // 水平输入
            if (Input.GetKey(KeyCode.A)) horizontal -= 1f; // A 向左
            if (Input.GetKey(KeyCode.D)) horizontal += 1f; // D 向右

            float vertical = 0f; // 垂直输入
            if (Input.GetKey(KeyCode.S)) vertical -= 1f; // S 向下
            if (Input.GetKey(KeyCode.W)) vertical += 1f; // W 向上

            if (horizontal != 0f || vertical != 0f)
            {
                var speed = BaseStats.MoveSpeed; // 读取移动速度
                if (speed > 0f)
                {
                    var direction = new Vector2(horizontal, vertical); // 组装移动方向
                    if (direction.sqrMagnitude > 1f)
                    {
                        direction.Normalize(); // 归一化避免斜向加速
                    }

                    _rigidbody2D.MovePosition(_rigidbody2D.position + direction * speed * deltaTime); // 使用刚体移动
                }
            }
        }

        TryHandleAttackInput(); // 处理攻击输入
        TryHandleBlackHeartClick(); // 处理黑心点击拾取
    }

    /// <summary>
    /// 判断目标碰撞体是否在拾取范围内。
    /// </summary>
    /// <param name="targetCollider">目标碰撞体。</param>
    public bool IsInSelectionRange(Collider2D targetCollider) // 拾取范围检测入口
    {
        if (_selectionRange == null)
        {
            return false; // 未配置拾取范围时返回 false
        }

        if (targetCollider == null)
        {
            return false; // 目标碰撞体为空时返回 false
        }

        var selectionPosition = (Vector2)_selectionRange.transform.position; // 读取拾取范围中心
        var targetClosest = targetCollider.ClosestPoint(selectionPosition); // 获取目标碰撞体最近点
        var rangeClosest = _selectionRange.ClosestPoint(targetClosest); // 获取拾取范围最近点
        var delta = targetClosest - rangeClosest; // 计算最近点偏差
        return delta.sqrMagnitude <= 0.0001f; // 判断是否在拾取范围内
    }

    /// <summary>
    /// 触发拾取范围时尝试拾取金币。
    /// </summary>
    /// <param name="other">进入触发器的碰撞体。</param>
    private void OnTriggerEnter2D(Collider2D other) // 拾取触发入口
    {
        if (_selectionRange == null)
        {
            return; // 未配置拾取范围时退出
        }

        if (!_selectionRange.IsTouching(other))
        {
            return; // 未进入拾取范围时退出
        }

        if (!other.TryGetComponent<MoneyEntity>(out var moneyEntity))
        {
            return; // 不是金币实体时退出
        }

        if (CY.BattleDataManager == null) // 战斗数据管理器未就绪时不允许拾取，避免结算阶段无法入账
        {
            return; // 管理器为空时退出
        }

        if (!moneyEntity.TryPickup(out _))
        {
            return; // 拾取失败时退出
        }

        var playerTransform = _cachedTransform != null ? _cachedTransform : transform; // 获取玩家 Transform
        moneyEntity.PlayPickupToTarget(playerTransform); // 播放拾取动画并在结束回收
    }

    protected override void OnEntityRecycle()
    {
        base.OnEntityRecycle();
        CY.Camera.ClearFollowTarget();//清理相机跟随目标
    }

    /// <summary>
    /// 绘制攻击范围（仅在编辑器 Scene 视图中显示）。
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!_showAttackRangeGizmos || !_showAttackRangeAlways)
        {
            return;
        }

        DrawAttackRangeGizmos(); // 绘制攻击范围
    }

    /// <summary>
    /// 绘制攻击范围（选中时显示）。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!_showAttackRangeGizmos)
        {
            return;
        }

        DrawAttackRangeGizmos(); // 绘制攻击范围
    }

    /// <summary>
    /// 绘制攻击范围（统一入口）。
    /// </summary>
    private void DrawAttackRangeGizmos()
    {
        if (!BaseStats.IsRanged)
        {
            return; // 近战不显示攻击范围
        }

        var cachedTransform = _cachedTransform != null ? _cachedTransform : transform; // 获取可用 Transform
        var attackRange = BaseStats.AttackRange; // 获取攻击范围
        if (attackRange <= 0f)
        {
            return;
        }

        Gizmos.color = _attackRangeGizmosColor; // 设置攻击范围颜色
        Gizmos.DrawWireSphere(cachedTransform.position, attackRange); // 绘制攻击范围圆
    }

    /// <summary>
    /// 处理黑心点击拾取。
    /// </summary>
    private void TryHandleBlackHeartClick() // 黑心点击拾取入口
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return; // 未点击左键时退出
        }

        if (_selectionRange == null)
        {
            return; // 拾取范围未配置时退出
        }

        var cameraManager = CY.Camera; // 获取相机管理器
        var worldCamera = cameraManager != null ? cameraManager.WorldCamera : null; // 获取世界相机
        if (worldCamera != null)
        {
            _cachedCamera = worldCamera; // 同步缓存相机引用
        }
        else
        {
            worldCamera = _cachedCamera; // 回退使用缓存相机
        }

        if (worldCamera == null)
        {
            _cachedCamera = Camera.main; // 回退获取主相机
            worldCamera = _cachedCamera; // 更新当前相机
        }

        if (worldCamera == null)
        {
            if (!_hasLoggedMissingCamera)
            {
                CY.LogError("[PlayerEntity] 未找到可用摄像机（需要 MainCamera 标签或场景摄像机）。"); // 输出摄像机缺失错误
                _hasLoggedMissingCamera = true; // 标记已输出日志
            }

            return; // 摄像机为空时退出
        }

        var playerTransform = _cachedTransform != null ? _cachedTransform : transform; // 获取玩家 Transform
        var playerPosition = playerTransform.position; // 获取玩家位置
        var screenPosition = Input.mousePosition; // 获取鼠标屏幕坐标
        screenPosition.z = Mathf.Abs(worldCamera.transform.position.z - playerPosition.z); // 设置投射深度
        var worldPosition = (Vector2)worldCamera.ScreenToWorldPoint(screenPosition); // 计算鼠标世界坐标
        var hitCount = Physics2D.OverlapPointNonAlloc(worldPosition, _blackHeartClickHits); // 获取命中碰撞体数量
        if (hitCount <= 0)
        {
            return; // 未命中任何碰撞体时退出
        }

        for (int i = 0; i < hitCount; i++)
        {
            var hitCollider = _blackHeartClickHits[i]; // 获取命中的碰撞体
            if (hitCollider == null)
            {
                continue; // 碰撞体为空时跳过
            }

            if (!BlackHeartEntity.TryGetEntityByCollider(hitCollider, out var blackHeartEntity))
            {
                continue; // 非黑心实体时跳过
            }

            if (blackHeartEntity == null)
            {
                continue; // 黑心实体为空时跳过
            }

            if (!IsInSelectionRange(hitCollider))
            {
                continue; // 未进入拾取范围时跳过
            }

            if (CY.BattleDataManager == null) // 战斗数据管理器未就绪时不允许拾取，避免结算阶段无法入账
            {
                return; // 管理器为空时退出
            }

            if (!blackHeartEntity.TryPickup(out _))
            {
                return; // 拾取失败时退出
            }

            blackHeartEntity.PlayPickupToTarget(playerTransform); // 播放拾取动画并回收
            return; // 成功拾取后退出
        }
    }

    /// <summary>
    /// 获取攻击起点：优先使用攻击点，缺失则回退到单位中心。
    /// </summary>
    /// <param name="origin">输出攻击起点世界坐标。</param>
    protected override bool TryGetAttackOrigin(out Vector2 origin) // 攻击起点覆盖入口
    {
        if (_attackLocationTransform == null && AttackLocation != null)
        {
            _attackLocationTransform = AttackLocation.transform; // 缓存攻击点 Transform
        }

        if (_attackLocationTransform != null)
        {
            origin = _attackLocationTransform.position; // 使用攻击点世界坐标
            return true; // 攻击点有效时返回
        }

        return base.TryGetAttackOrigin(out origin); // 回退到单位中心起点
    }

    /// <summary>
    /// 处理攻击输入：按键长按，根据鼠标方向发射子弹。
    /// </summary>
    private void TryHandleAttackInput()
    {
        if (!BaseStats.IsRanged)
        {
            return; // 近战不处理远程输入
        }

        var wantsFire = _allowHoldAttack ? Input.GetKey(_attackKey) : Input.GetKeyDown(_attackKey); // 判断是否触发攻击
        if (!wantsFire)
        {
            return; // 未触发攻击时退出
        }

        if (_cachedCamera == null)
        {
            _cachedCamera = Camera.main; // 重新获取主摄像机
        }

        if (_cachedCamera == null)
        {
            if (!_hasLoggedMissingCamera)
            {
                CY.LogError("[PlayerEntity] 未找到可用摄像机（需要 MainCamera 标签或场景摄像机）。"); // 输出摄像机缺失错误
                _hasLoggedMissingCamera = true; // 标记已输出日志
            }

            return; // 摄像机为空时无法计算世界坐标
        }

        if (!TryGetAttackOrigin(out var origin))
        {
            return; // 起点无效时退出
        }

        var originZ = _attackLocationTransform != null
            ? _attackLocationTransform.position.z // 优先使用攻击点的 Z 坐标
            : (_cachedTransform != null ? _cachedTransform.position.z : transform.position.z); // 回退到玩家本体 Z

        var screenPos = Input.mousePosition; // 读取鼠标屏幕坐标
        screenPos.z = Mathf.Abs(_cachedCamera.transform.position.z - originZ); // 设置投射深度
        var mouseWorld = _cachedCamera.ScreenToWorldPoint(screenPos); // 计算鼠标世界坐标
        var direction = (Vector2)mouseWorld - (Vector2)origin; // 计算发射方向
        if (direction.sqrMagnitude <= 0f)
        {
            return; // 方向无效时退出
        }

        TryAttackDirection(direction); // 按方向触发攻击
    }
}
