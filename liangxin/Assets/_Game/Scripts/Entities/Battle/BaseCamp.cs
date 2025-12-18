using CYFramework;
using CYFramework.Core.Entity;
using System;
using UnityEngine;

/// <summary>
/// 大本营（BaseCamp）。
/// 说明：
/// - 该类专注“血量数据 + 事件发布”，不承担 UI 实例化职责（由 HPBarManager 管理）。
/// - BaseCamp 默认有两条血条（Slot0/Slot1），可用于“护盾/城墙 + 本体”等需求。
/// - 事件发布必须使用 CY.Event.Post(ref evt)，并且事件必须是 struct（符合 CYFramework 约束）。
/// </summary>
public class BaseCamp : EntityBase
{
    /// <summary>
    /// BaseCamp 的事件 UnitID（BaseCamp 不是 EntityBase，这里使用固定 ID 作为事件键）。
    /// 注意：避免与普通单位 Id 冲突即可；项目内如有统一 ID 体系，可改为从配置/管理器获取。
    /// </summary>
    public const int BaseCampUnitId = 0;

    [Header("Core Resources (Mapped from DepartmentManager)")]
    [Tooltip("Slot 0: 公司良心 (生存血条，归零失败)")]
    [SerializeField] private float _conscienceHp; 
    [SerializeField] private float _conscienceMaxHp;

    [Tooltip("Slot 1: 公司黑心 (污染指数，满值可能有负面效果)")]
    [SerializeField] private float _corruptionHp;
    [SerializeField] private float _corruptionMaxHp;

    /// <summary>Slot0: 公司良心 (CompanyConscience)</summary>
    public float Slot0CurrentHp => _conscienceHp;
    public float Slot0MaxHp => _conscienceMaxHp;

    /// <summary>Slot1: 公司黑心 (CompanyCorruption)</summary>
    public float Slot1CurrentHp => _corruptionHp;
    public float Slot1MaxHp => _corruptionMaxHp;

    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);

        CY.Event.Subscribe<StartGameEvent>(StartGame, this);
        CY.Event.Subscribe<OverGameEvent>(OverGame, this);
    }

    /// <summary>
    /// 开始游戏事件接收
    /// </summary>
    /// <param name="evt"></param>
    private void StartGame(ref StartGameEvent evt)
    {
        CY.Timer.NextFrame(PostInitialHPEvents);
    }

    /// <summary>
    /// 结束游戏事件接收
    /// </summary>
    /// <param name="evt"></param>
    private void OverGame(ref OverGameEvent evt)
    {
        // 通知 UI 回收所有血条（Slot0/Slot1）。
        UnitDeadEvent deadEvt = new UnitDeadEvent { UnitID = BaseCampUnitId };
        CY.Event.Post(ref deadEvt);
    }

    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);
        
    }

    protected override void OnEntityRecycle()
    {
        base.OnEntityRecycle();
        // BaseCamp 被销毁时，通知 UI 回收所有血条（Slot0/Slot1）。
        UnitDeadEvent deadEvt = new UnitDeadEvent { UnitID = BaseCampUnitId };
        CY.Event.Post(ref deadEvt);
    }

    /// <summary>
    /// 主动推送一次初始血量（用于生成血条）。
    /// 注意：EventBus 默认不缓存事件，因此初始事件应在 UI 订阅后至少触发一次。
    /// </summary>
    /// <summary>
    /// 主动推送一次初始血量（用于生成血条）。
    /// 注意：EventBus 默认不缓存事件，因此初始事件应在 UI 订阅后至少触发一次。
    /// </summary>
    public void PostInitialHPEvents()
    {
        _conscienceMaxHp = CY.Department.MaxConscience;
        _conscienceHp = CY.Department.Data.CompanyConscience;

        _corruptionMaxHp = CY.Department.MaxCompanyCorruption;
        _corruptionHp = CY.Department.Data.CompanyCorruption;
        // 允许多次调用：EventBus 不缓存事件，且 HPBarManager 可能晚于 BaseCamp Start 才订阅。
        // 重复发送不会重复生成血条（HPBarManager 以 (UnitID, Style, Slot) 作为唯一键）。
        PostHPChanged(0, _conscienceHp, _conscienceMaxHp, 0, false);
        PostHPChanged(1, _corruptionHp, _corruptionMaxHp, 0, false);
    }

    /// <summary>
    /// 修改 Slot0: 公司良心 (生存血条)
    /// </summary>
    public void SetSlot0Hp(float current, float max, int damage = 0, bool isCritical = false)
    {
        float oldVal = _conscienceHp;
        _conscienceMaxHp = Mathf.Max(0f, max);
        _conscienceHp = Mathf.Clamp(current, 0f, _conscienceMaxHp);
        
        // 双向同步：通知 DepartmentManager 更新全局数据
        // 注意：DepartmentManager 使用 int，这里会有精度丢弃
        if (CY.Department != null)
        {
            int delta = (int)(_conscienceHp - oldVal);
            if (delta != 0) CY.Department.ChangeCompanyConscience(delta);
        }

        PostHPChanged(0, _conscienceHp, _conscienceMaxHp, damage, isCritical);
    }

    /// <summary>
    /// 修改 Slot1: 公司黑心 (污染指数)
    /// </summary>
    public void SetSlot1Hp(float current, float max, int damage = 0, bool isCritical = false)
    {
        float oldVal = _corruptionHp;
        _corruptionMaxHp = Mathf.Max(0f, max);
        _corruptionHp = Mathf.Clamp(current, 0f, _corruptionMaxHp);

         // 双向同步：通知 DepartmentManager 更新全局数据
        if (CY.Department != null)
        {
            int delta = (int)(_corruptionHp - oldVal);
            if (delta != 0) CY.Department.ChangeCompanyCorruption(delta);
        }

        PostHPChanged(1, _corruptionHp, _corruptionMaxHp, damage, isCritical);
    }

    /// <summary>
    /// 统一发布 HP 变化事件（0GC：struct + ref）。
    /// </summary>
    private void PostHPChanged(byte slot, float current, float max, int damage, bool isCritical)
    {
        UnitHPChangedEvent evt = new UnitHPChangedEvent
        {
            UnitID = BaseCampUnitId,
            CurrentHP = current,
            MaxHP = max,
            Damage = damage,
            IsCritical = isCritical,
            WorldPosition = transform.position,
            // 只有 Slot 0 (良心血条) 归零才代表实体死亡；Slot 1 (污染) 归零是好事，不应销毁
            IsDead = (slot == 0 && current <= 0f),
            BarStyle = HPBarStyle.BaseCamp,
            BarSlot = slot
        };
        CY.Event.Post(ref evt);
    }
}
