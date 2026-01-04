using CYFramework;
using CYFramework.Core.Entity;
using UnityEngine;
/// <summary>
/// 老板单位实体（继承通用 UnitEntity）。
/// 仅作为类型标识，具体行为由后续系统扩展。
/// </summary>
[RequireComponent(typeof(HybridNavigationAgent))]
[EntityPrefab("Prefabs/Entities/Unit/Player/PlayerEntity", "Players", "Players")]
public sealed class PlayerEntity : UnitEntity
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

    /// <summary>
    /// 初始化时缓存组件，避免在 Update 中重复查询。
    /// </summary>
    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        _cachedTransform = transform;
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _cachedCamera = Camera.main; // 缓存主摄像机
        if (_cachedCamera == null)
        {
            _cachedCamera = FindObjectOfType<Camera>(); // 回退获取场景中的任意摄像机（低频）
        }
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
    }

    protected override void OnEntityRecycle()
    {
        base.OnEntityRecycle();
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

        var t = _cachedTransform != null ? _cachedTransform : transform; // 获取可用 Transform
        var attackRange = BaseStats.AttackRange; // 获取攻击范围
        if (attackRange <= 0f)
        {
            return;
        }

        Gizmos.color = _attackRangeGizmosColor; // 设置攻击范围颜色
        Gizmos.DrawWireSphere(t.position, attackRange); // 绘制攻击范围圆
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

        var origin = _cachedTransform != null ? _cachedTransform.position : transform.position; // 获取自身位置
        var screenPos = Input.mousePosition; // 读取鼠标屏幕坐标
        screenPos.z = Mathf.Abs(_cachedCamera.transform.position.z - origin.z); // 设置投射深度
        var mouseWorld = _cachedCamera.ScreenToWorldPoint(screenPos); // 计算鼠标世界坐标
        var direction = (Vector2)mouseWorld - (Vector2)origin; // 计算发射方向
        if (direction.sqrMagnitude <= 0f)
        {
            return; // 方向无效时退出
        }

        TryAttackDirection(direction); // 按方向触发攻击
    }
}
