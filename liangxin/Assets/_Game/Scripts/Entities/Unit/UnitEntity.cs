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
    /// 实体显示：重置生命并派发生成/血量事件。
    /// </summary>
    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);
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

        if (!target.TryApplyDamage(damage, isCrit))
        {
            return false;
        }

        var interval = _baseStats.AttackInterval;
        _attackCooldown = interval > 0f ? interval : 0f;
        return true;
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
