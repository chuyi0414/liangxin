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

    /// <summary>
    /// 初始化时缓存组件，避免在 Update 中重复查询。
    /// </summary>
    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        _cachedTransform = transform;
        _rigidbody2D = GetComponent<Rigidbody2D>();
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

        if (_rigidbody2D == null)
        {
            return;
        }

        float horizontal = 0f;
        if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.D)) horizontal += 1f;

        float vertical = 0f;
        if (Input.GetKey(KeyCode.S)) vertical -= 1f;
        if (Input.GetKey(KeyCode.W)) vertical += 1f;

        if (horizontal == 0f && vertical == 0f)
        {
            return;
        }

        var speed = BaseStats.MoveSpeed;
        if (speed <= 0f)
        {
            return;
        }

        var direction = new Vector2(horizontal, vertical);
        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        _rigidbody2D.MovePosition(_rigidbody2D.position + direction * speed * deltaTime);
    }

    protected override void OnEntityRecycle()
    {
        base.OnEntityRecycle();
    }
}
