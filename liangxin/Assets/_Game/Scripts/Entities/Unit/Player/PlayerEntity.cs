using CYFramework;
using CYFramework.Core.Entity;
using CYFramework.Infrastructure; // ServiceLocator 引用
using UnityEngine;
using UnityEngine.EventSystems; // UI 事件系统引用（用于屏蔽点击 UI 时的世界交互）
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
    /// <summary>
    /// 漂浮点
    /// </summary>
    [SerializeField] private GameObject _floatingPoint; // 漂浮点对象
    /// <summary>漂浮点 Transform 缓存。</summary>
    private Transform _floatingPointTransform; // 漂浮点 Transform 缓存

    /// <summary>黑心点击检测命中缓存（避免运行时分配）。</summary>
    private static readonly Collider2D[] _blackHeartClickHits = new Collider2D[16]; // 黑心点击命中缓存

    /// <summary>
    /// 当前选中的员工（左键点击员工选中；支持多个不同员工脚本）。
    /// </summary>
    private IEmployeeControllable _selectedEmployee; // 当前选中员工接口引用
    /// <summary>
    /// 初始化时缓存组件，避免在 Update 中重复查询。
    /// </summary>
    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        _cachedTransform = transform;
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _attackLocationTransform = AttackLocation != null ? AttackLocation.transform : null; // 缓存攻击点 Transform
        _floatingPointTransform = _floatingPoint != null ? _floatingPoint.transform : null; // 缓存漂浮点 Transform
        _cachedCamera = ServiceLocator.TryGet<CameraManager>(out var cameraManager) && cameraManager != null && cameraManager.WorldCamera != null ? cameraManager.WorldCamera : Camera.main; // 优先使用相机管理器缓存的世界相机
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
        ApplyBulletSpeed(row.BulletSpeed); // 应用子弹飞行速度
        ApplyBulletArrayConfigFromRow(row); // 应用子弹数组配置
        base.OnEntityShow(userData);
    }

    /// <summary>
    /// 应用玩家子弹数组配置。
    /// </summary>
    /// <param name="row">玩家数据行。</param>
    private void ApplyBulletArrayConfigFromRow(PlayerUnitRow row) // 子弹数组应用入口
    {
        if (row == null)
        {
            return; // 数据行为空时直接退出
        }

        var bulletArrayId = row.BulletArrayId; // 读取子弹数组配置 Id
        if (bulletArrayId <= 0)
        {
            ApplyBulletArrayConfig(BulletSelectRule.Random, null); // Id 无效时清空子弹数组配置
            return; // 直接退出
        }

        var unitManager = CY.Unit; // 获取单位管理器
        if (unitManager == null)
        {
            CY.LogWarning("[PlayerEntity] UnitManager 未就绪，无法应用子弹数组配置。"); // 输出警告日志
            ApplyBulletArrayConfig(BulletSelectRule.Random, null); // 清空子弹数组配置
            return; // 直接退出
        }

        if (!unitManager.TryGetBulletArrayRow(bulletArrayId, out var bulletArrayRow))
        {
            CY.LogWarning($"[PlayerEntity] 未找到子弹数组配置，Id={bulletArrayId}"); // 输出缺失警告
            ApplyBulletArrayConfig(BulletSelectRule.Random, null); // 清空子弹数组配置
            return; // 直接退出
        }

        if (!bulletArrayRow.TryGetPrefabPaths(out var prefabPaths))
        {
            CY.LogWarning($"[PlayerEntity] 子弹数组配置无有效路径，Id={bulletArrayId}"); // 输出路径警告
            ApplyBulletArrayConfig(BulletSelectRule.Random, null); // 清空子弹数组配置
            return; // 直接退出
        }

        ApplyBulletArrayConfig(bulletArrayRow.SelectRule, prefabPaths); // 应用子弹数组配置
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
        TryHandleLeftClick(); // 处理左键：黑心优先，其次员工选中/取消
        TryHandleRightClick(); // 处理右键：黑心优先，否则命令选中员工移动
    }

    /// <summary>
    /// 处理左键点击：优先拾取黑心；否则尝试选中员工；点击空处则取消选中。
    /// </summary>
    private void TryHandleLeftClick() // 左键处理入口
    {
        if (!Input.GetMouseButtonDown(0)) // 左键点击判定
        {
            return; // 未点击时直接退出
        }

        if (IsPointerOverUI()) // 点击在 UI 上判定
        {
            return; // 在 UI 上时不处理世界点击
        }

        if (!TryGetMouseWorldPosition(out var worldPosition)) // 获取鼠标世界坐标
        {
            return; // 坐标获取失败时退出
        }

        var hitCount = Physics2D.OverlapPointNonAlloc(worldPosition, _blackHeartClickHits); // 获取鼠标点命中碰撞体
        if (TryHandleBlackHeartClickByHits(worldPosition, hitCount)) // 尝试拾取黑心（成功则消费点击）
        {
            return; // 黑心拾取成功时不再处理选中逻辑
        }

        if (TrySelectEmployeeByHits(hitCount)) // 尝试根据命中碰撞体选中员工
        {
            return; // 选中成功时退出
        }

        _selectedEmployee = null; // 点击空处则取消当前选中员工
    }

    /// <summary>
    /// 处理右键点击：优先拾取黑心；若黑心不可拾取且已选中员工，则命令员工移动到点击位置。
    /// </summary>
    private void TryHandleRightClick() // 右键处理入口
    {
        if (!Input.GetMouseButtonDown(1)) // 右键点击判定
        {
            return; // 未点击时直接退出
        }

        if (IsPointerOverUI()) // 点击在 UI 上判定
        {
            return; // 在 UI 上时不处理世界点击
        }

        if (_selectedEmployee == null) // 未选中员工判定
        {
            return; // 未选中员工时不处理移动命令
        }

        if (_selectedEmployee.Unit == null || _selectedEmployee.Unit.LifeState == UnitLifeState.Dead) // 选中员工无效/死亡判定
        {
            _selectedEmployee = null; // 死亡时清空选中引用
            return; // 直接退出
        }

        if (!TryGetMouseWorldPosition(out var worldPosition)) // 获取鼠标世界坐标
        {
            return; // 坐标获取失败时退出
        }

        var hitCount = Physics2D.OverlapPointNonAlloc(worldPosition, _blackHeartClickHits); // 获取鼠标点命中碰撞体
        if (TryHandleBlackHeartClickByHits(worldPosition, hitCount)) // 若黑心可拾取，则优先拾取（不下发移动）
        {
            return; // 黑心拾取成功时退出
        }

        _selectedEmployee.TryCommandMove(worldPosition); // 黑心不可拾取时，将右键位置作为移动目标点
    }

    /// <summary>
    /// 判断鼠标是否指向 UI：用于屏蔽 UI 点击对世界交互的影响。
    /// </summary>
    private bool IsPointerOverUI() // UI 指针检测入口
    {
        var eventSystem = EventSystem.current; // 获取当前 EventSystem
        if (eventSystem == null) // EventSystem 缺失判定
        {
            return false; // 缺失时认为不在 UI 上
        }

        return eventSystem.IsPointerOverGameObject(); // 判断当前指针是否在 UI 上
    }

    /// <summary>
    /// 获取鼠标点击的世界坐标（XY 平面）。
    /// </summary>
    /// <param name="worldPosition">输出世界坐标。</param>
    /// <returns>是否成功获取。</returns>
    private bool TryGetMouseWorldPosition(out Vector2 worldPosition) // 鼠标世界坐标获取入口
    {
        worldPosition = Vector2.zero; // 默认输出为零点

        var cameraManager = CY.Camera; // 获取相机管理器
        var worldCamera = cameraManager != null ? cameraManager.WorldCamera : null; // 获取世界相机
        if (worldCamera != null) // 世界相机存在判定
        {
            _cachedCamera = worldCamera; // 同步缓存相机引用
        }
        else // 世界相机缺失分支
        {
            worldCamera = _cachedCamera; // 回退使用缓存相机
        }

        if (worldCamera == null) // 仍缺失判定
        {
            _cachedCamera = Camera.main; // 回退获取主相机
            worldCamera = _cachedCamera; // 更新当前相机
        }

        if (worldCamera == null) // 相机仍缺失判定
        {
            if (!_hasLoggedMissingCamera) // 只输出一次日志判定
            {
                CY.LogError("[PlayerEntity] 未找到可用摄像机（需要 MainCamera 标签或场景摄像机）。"); // 输出摄像机缺失错误
                _hasLoggedMissingCamera = true; // 标记已输出日志
            }

            return false; // 相机缺失时返回失败
        }

        var playerTransform = _cachedTransform != null ? _cachedTransform : transform; // 获取玩家 Transform
        var playerPosition = playerTransform.position; // 获取玩家位置
        var screenPosition = Input.mousePosition; // 获取鼠标屏幕坐标
        screenPosition.z = Mathf.Abs(worldCamera.transform.position.z - playerPosition.z); // 设置投射深度（以玩家 Z 为参考）
        worldPosition = (Vector2)worldCamera.ScreenToWorldPoint(screenPosition); // 计算鼠标世界坐标（XY 平面）
        return true; // 返回获取成功
    }

    /// <summary>
    /// 尝试处理黑心拾取（使用外部命中结果，避免重复 Overlap）。
    /// </summary>
    /// <param name="worldPosition">鼠标世界坐标。</param>
    /// <param name="hitCount">命中碰撞体数量。</param>
    /// <returns>是否成功拾取黑心并消费本次点击。</returns>
    private bool TryHandleBlackHeartClickByHits(Vector2 worldPosition, int hitCount) // 黑心拾取（命中复用）入口
    {
        if (hitCount <= 0) // 未命中判定
        {
            return false; // 未命中任何碰撞体时返回失败
        }

        if (_selectionRange == null) // 拾取范围未配置判定
        {
            return false; // 未配置拾取范围时返回失败
        }

        for (int i = 0; i < hitCount; i++) // 遍历命中碰撞体
        {
            var hitCollider = _blackHeartClickHits[i]; // 获取命中的碰撞体
            if (hitCollider == null) // 碰撞体为空判定
            {
                continue; // 碰撞体为空时跳过
            }

            if (!BlackHeartEntity.TryGetEntityByCollider(hitCollider, out var blackHeartEntity)) // 尝试获取黑心实体
            {
                continue; // 非黑心实体时跳过
            }

            if (blackHeartEntity == null) // 黑心实体为空判定
            {
                continue; // 黑心实体为空时跳过
            }

            if (!IsInSelectionRange(hitCollider)) // 未进入拾取范围判定
            {
                continue; // 未进入拾取范围时跳过
            }

            if (CY.BattleDataManager == null) // 战斗数据管理器未就绪判定
            {
                return false; // 管理器为空时返回失败（不消费点击）
            }

            if (!blackHeartEntity.TryPickup(out _)) // 尝试拾取黑心
            {
                return false; // 拾取失败时返回失败（不消费点击）
            }

            var floatingTarget = GetBlackHeartFloatingTarget(); // 获取黑心漂浮目标
            blackHeartEntity.PlayPickupToTarget(floatingTarget); // 播放漂浮移动动画并进入漂浮状态
            return true; // 拾取成功时返回成功并消费点击
        }

        return false; // 未拾取到黑心时返回失败
    }

    /// <summary>
    /// 尝试根据点击命中碰撞体选中员工。
    /// </summary>
    /// <param name="hitCount">命中数量。</param>
    /// <returns>是否选中成功。</returns>
    private bool TrySelectEmployeeByHits(int hitCount) // 员工选中入口
    {
        if (hitCount <= 0) // 未命中判定
        {
            return false; // 未命中时返回失败
        }

        for (int i = 0; i < hitCount; i++) // 遍历命中碰撞体
        {
            var hitCollider = _blackHeartClickHits[i]; // 获取命中的碰撞体
            if (hitCollider == null) // 碰撞体为空判定
            {
                continue; // 碰撞体为空时跳过
            }

            if (!EmployeeClickRegistry.TryGetByCollider(hitCollider, out var employeeEntity)) // 尝试通过碰撞体获取员工接口
            {
                continue; // 非员工实体时跳过
            }

            if (employeeEntity == null) // 员工为空判定
            {
                continue; // 员工实体为空时跳过
            }

            if (employeeEntity.Unit == null || employeeEntity.Unit.LifeState == UnitLifeState.Dead) // 死亡/无效员工判定
            {
                continue; // 死亡员工不允许选中
            }

            _selectedEmployee = employeeEntity; // 写入选中员工
            return true; // 选中成功返回 true
        }

        return false; // 未命中任何员工时返回 false
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
    /// 获取黑心漂浮目标 Transform（优先漂浮点，缺失则回退玩家本体）。
    /// </summary>
    private Transform GetBlackHeartFloatingTarget() // 黑心漂浮目标获取入口
    {
        if (_floatingPointTransform == null && _floatingPoint != null)
        {
            _floatingPointTransform = _floatingPoint.transform; // 缓存漂浮点 Transform
        }

        if (_floatingPointTransform != null)
        {
            return _floatingPointTransform; // 优先使用漂浮点
        }

        return _cachedTransform != null ? _cachedTransform : transform; // 回退到玩家 Transform
    }

    /// <summary>
    /// 处理黑心点击拾取。
    /// </summary>
    private void TryHandleBlackHeartClick() // 黑心点击拾取入口
    {
        if (!Input.GetMouseButtonDown(0)) // 左键点击判定
        {
            return; // 未点击左键时退出
        }

        if (IsPointerOverUI()) // 点击在 UI 上判定
        {
            return; // 在 UI 上时不处理世界拾取
        }

        if (!TryGetMouseWorldPosition(out var worldPosition)) // 获取鼠标世界坐标
        {
            return; // 坐标获取失败时退出
        }

        var hitCount = Physics2D.OverlapPointNonAlloc(worldPosition, _blackHeartClickHits); // 获取命中碰撞体数量
        TryHandleBlackHeartClickByHits(worldPosition, hitCount); // 复用命中结果尝试拾取黑心
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
