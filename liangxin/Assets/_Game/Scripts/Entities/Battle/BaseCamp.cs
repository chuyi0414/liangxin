using CYFramework;
using UnityEngine;

/// <summary>
/// 大本营（BaseCamp）。
/// 说明：
/// - 该类专注“血量数据 + 事件发布”，不承担 UI 实例化职责（由 HPBarManager 管理）。
/// - BaseCamp 默认有两条血条（Slot0/Slot1），可用于“护盾/城墙 + 本体”等需求。
/// - 事件发布必须使用 CY.Event.Post(ref evt)，并且事件必须是 struct（符合 CYFramework 约束）。
/// </summary>
public class BaseCamp : MonoBehaviour
{
    /// <summary>
    /// BaseCamp 的事件 UnitID（BaseCamp 不是 EntityBase，这里使用固定 ID 作为事件键）。
    /// 注意：避免与普通单位 Id 冲突即可；项目内如有统一 ID 体系，可改为从配置/管理器获取。
    /// </summary>
    public const int BaseCampUnitId = 0;

    [Header("HP (Slot0/Slot1)")]
    [SerializeField] private float _slot0MaxHp = 1000f;
    [SerializeField] private float _slot0CurrentHp = 1000f;

    [SerializeField] private float _slot1MaxHp = 500f;
    [SerializeField] private float _slot1CurrentHp = 500f;

    /// <summary>Slot0 当前/最大（通常作为主血条）</summary>
    public float Slot0CurrentHp => _slot0CurrentHp;
    public float Slot0MaxHp => _slot0MaxHp;

    /// <summary>Slot1 当前/最大（第二条血条：护盾/城墙等）</summary>
    public float Slot1CurrentHp => _slot1CurrentHp;
    public float Slot1MaxHp => _slot1MaxHp;


    private void Awake()
    {
        // 保证初始值不越界（编辑器配置错误时兜底）
        if (_slot0MaxHp < 0f) _slot0MaxHp = 0f;
        if (_slot1MaxHp < 0f) _slot1MaxHp = 0f;
        _slot0CurrentHp = Mathf.Clamp(_slot0CurrentHp, 0f, _slot0MaxHp);
        _slot1CurrentHp = Mathf.Clamp(_slot1CurrentHp, 0f, _slot1MaxHp);
    }

    private void Start()
    {
        // 低频：延迟到下一帧发布，增加“HPBarManager 已订阅事件”的概率。
        // 同时 HPBarManager 也会主动调用 PostInitialHPEvents()，双保险。
        CY.Timer.NextFrame(PostInitialHPEvents);
    }

    private void OnDestroy()
    {
        // BaseCamp 被销毁时，通知 UI 回收所有血条（Slot0/Slot1）。
        UnitDeadEvent deadEvt = new UnitDeadEvent { UnitID = BaseCampUnitId };
        CY.Event.Post(ref deadEvt);
    }

    /// <summary>
    /// 主动推送一次初始血量（用于生成血条）。
    /// 注意：EventBus 默认不缓存事件，因此初始事件应在 UI 订阅后至少触发一次。
    /// </summary>
    public void PostInitialHPEvents()
    {
        // 允许多次调用：EventBus 不缓存事件，且 HPBarManager 可能晚于 BaseCamp Start 才订阅。
        // 重复发送不会重复生成血条（HPBarManager 以 (UnitID, Style, Slot) 作为唯一键）。
        PostHPChanged(0, _slot0CurrentHp, _slot0MaxHp, 0, false);
        PostHPChanged(1, _slot1CurrentHp, _slot1MaxHp, 0, false);
    }

    /// <summary>
    /// 修改 Slot0 血量（可被战斗逻辑调用）。
    /// </summary>
    public void SetSlot0Hp(float current, float max, int damage = 0, bool isCritical = false)
    {
        _slot0MaxHp = Mathf.Max(0f, max);
        _slot0CurrentHp = Mathf.Clamp(current, 0f, _slot0MaxHp);
        PostHPChanged(0, _slot0CurrentHp, _slot0MaxHp, damage, isCritical);
    }

    /// <summary>
    /// 修改 Slot1 血量（第二条血条）。
    /// </summary>
    public void SetSlot1Hp(float current, float max, int damage = 0, bool isCritical = false)
    {
        _slot1MaxHp = Mathf.Max(0f, max);
        _slot1CurrentHp = Mathf.Clamp(current, 0f, _slot1MaxHp);
        PostHPChanged(1, _slot1CurrentHp, _slot1MaxHp, damage, isCritical);
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
            IsDead = current <= 0f,
            BarStyle = HPBarStyle.BaseCamp,
            BarSlot = slot
        };
        CY.Event.Post(ref evt);
    }
}
