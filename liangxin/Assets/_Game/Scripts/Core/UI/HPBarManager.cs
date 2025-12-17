using System.Collections.Generic;
using CYFramework;
using CYFramework.Core.UI;
using UnityEngine;

/// <summary>
/// 战斗血条集中管理器（UI 面板）。
/// 设计目标：
/// 1) 单一入口订阅事件，避免每个血条实例各自订阅导致管理复杂与泄漏风险。
/// 2) 支持多种血条样式（不同 Prefab/偏移）。
/// 3) 支持同一单位多条血条（例如 BaseCamp 两条血条），通过 (UnitID, Style, Slot) 区分。
/// </summary>
[UIPrefab("Prefabs/UI/Battle/HPBarPanel")]
public class HPBarManager : UIPanel
{
    /// <summary>
    /// 血条样式配置：用于支持不同预制体、偏移，以及同一单位多条血条（SlotSpacingY）。
    /// 注意：WorldOffset/SlotSpacingY 是世界坐标，用于 worldPos = target.position + offset。
    /// </summary>
    [System.Serializable]
    private struct HPBarStyleSetting
    {
        public HPBarStyle Style;
        public GameObject Prefab;
        public Vector3 WorldOffset;

        [Tooltip("同一单位多条血条的 Y 方向间距（世界坐标）。Slot=1 会在 WorldOffset 基础上再加 SlotSpacingY")]
        public float SlotSpacingY;
    }

    /// <summary>
    /// Dictionary Key：同一 UnitID 可通过 Style/Slot 区分多条血条。
    /// </summary>
    private readonly struct UnitBarKey : System.IEquatable<UnitBarKey>
    {
        public readonly int UnitId;
        public readonly HPBarStyle Style;
        public readonly byte Slot;

        public UnitBarKey(int unitId, HPBarStyle style, byte slot)
        {
            UnitId = unitId;
            Style = style;
            Slot = slot;
        }

        public bool Equals(UnitBarKey other) => UnitId == other.UnitId && Style == other.Style && Slot == other.Slot;
        public override bool Equals(object obj) => obj is UnitBarKey other && Equals(other);
        public override int GetHashCode() => ((UnitId * 397) ^ ((int)Style * 17)) ^ Slot;
    }

    /// <summary>
    /// 样式 + 槽位 Key：用于为同一 Style 的不同 Slot 配置独立的 Prefab/偏移。
    /// </summary>
    private readonly struct StyleSlotKey : System.IEquatable<StyleSlotKey>
    {
        public readonly HPBarStyle Style;
        public readonly byte Slot;

        public StyleSlotKey(HPBarStyle style, byte slot)
        {
            Style = style;
            Slot = slot;
        }

        public bool Equals(StyleSlotKey other) => Style == other.Style && Slot == other.Slot;
        public override bool Equals(object obj) => obj is StyleSlotKey other && Equals(other);
        public override int GetHashCode() => ((int)Style * 397) ^ Slot;
    }

    // 注意：不再暴露 Inspector 配置。样式在 BuildStyleMap 中统一写死，避免运行时误配置与 GC。
    private readonly Dictionary<UnitBarKey, UIUnitHPBar> _activeBars = new Dictionary<UnitBarKey, UIUnitHPBar>(256);
    private readonly Dictionary<HPBarStyle, Queue<UIUnitHPBar>> _pools = new Dictionary<HPBarStyle, Queue<UIUnitHPBar>>(8);
    private readonly Dictionary<HPBarStyle, HPBarStyleSetting> _styleMap = new Dictionary<HPBarStyle, HPBarStyleSetting>(8);
    private readonly Dictionary<StyleSlotKey, HPBarStyleSetting> _styleSlotMap = new Dictionary<StyleSlotKey, HPBarStyleSetting>(8);

    // 低频移除缓存：避免死亡销毁时每次都 new List（仅主线程使用）
    private static readonly List<UnitBarKey> s_removeKeysCache = new List<UnitBarKey>(32);

    private RectTransform _rectTransform;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);

        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform)
        {
            // 强制设为全屏拉伸，保证 Screen->Local 转换坐标系覆盖全屏。
            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }

        BuildStyleMap();

        CY.Event.Subscribe<UnitHPChangedEvent>(OnUnitHPChanged);
        CY.Event.Subscribe<UnitDeadEvent>(OnUnitDead);

        // 主动请求 BaseCamp 推送一次初始血量，避免“满血且长时间不受击”导致血条一直不生成。
        //TryRequestBaseCampInitialBars();
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        CY.Event.Unsubscribe<UnitHPChangedEvent>(OnUnitHPChanged);
        CY.Event.Unsubscribe<UnitDeadEvent>(OnUnitDead);
        base.OnClose(isShutdown, userData);
    }

    protected override void OnUpdate(float deltaTime, float realDeltaTime)
    {
        var cameraMgr = CY.CameraManager;
        if (cameraMgr == null) return;

        var mainCam = cameraMgr.MainCamera;
        var uiCam = cameraMgr.UICamera;
        if (mainCam == null) return;

        foreach (var bar in _activeBars.Values)
        {
            if (bar.IsActive)
            {
                bar.UpdatePosition(mainCam, uiCam, _rectTransform);
            }
        }
    }

    private void OnUnitHPChanged(ref UnitHPChangedEvent evt)
    {
        var key = new UnitBarKey(evt.UnitID, evt.BarStyle, evt.BarSlot);

        if (!_activeBars.TryGetValue(key, out var bar))
        {
            if (!TryResolveTargetTransform(ref evt, out var targetTransform))
            {
                CY.LogWarning($"[HPBarManager] 无法解析 UnitID={evt.UnitID} 的 Transform，血条不会显示。");
                return;
            }

            if (!TryGetStyleSetting(evt.BarStyle, evt.BarSlot, out var styleSetting))
            {
                CY.LogError($"[HPBarManager] 未配置血条样式：Style={evt.BarStyle} Slot={evt.BarSlot}，请补充 BuildStyleMap 配置。");
                return;
            }

            bar = SpawnBar(evt.BarStyle, styleSetting.Prefab);
            if (bar == null) return;

            var offset = styleSetting.WorldOffset + new Vector3(0f, styleSetting.SlotSpacingY * evt.BarSlot, 0f);
            bar.Init(evt.UnitID, targetTransform, offset);
            _activeBars[key] = bar;
        }

        bar.UpdateHP(evt.CurrentHP, evt.MaxHP);

        if (evt.IsDead || evt.CurrentHP <= 0f)
        {
            RecycleBar(evt.BarStyle, bar);
            _activeBars.Remove(key);
        }
    }

    private void OnUnitDead(ref UnitDeadEvent evt)
    {
        RemoveAllBarsForUnit(evt.UnitID);
    }

    /// <summary>
    /// 构建样式配置表：所有样式与资源路径写死，避免运行时误配置与 GC。
    /// </summary>
    private void BuildStyleMap()
    {
        _styleMap.Clear();
        _styleSlotMap.Clear();

        GameObject enemyPrefab = CY.Resource.Load<GameObject>("Prefabs/UI/Widgets/EnemyHPBar");
        GameObject enemyPrefab1 = CY.Resource.Load<GameObject>("Prefabs/UI/Widgets/BaseCampHPBar");
        GameObject enemyPrefab2 = CY.Resource.Load<GameObject>("Prefabs/UI/Widgets/BaseCampCorruptionBar");

        if (enemyPrefab == null)
        {
            //CY.LogError($"[HPBarManager] 无法在 Resources 中找到血条预制体: Prefabs/UI/Widgets/EnemyHPBar");
            return;
        }

        // Enemy：默认单条血条。
        _styleMap[HPBarStyle.Enemy] = new HPBarStyleSetting
        {
            Style = HPBarStyle.Enemy,
            Prefab = enemyPrefab,
            WorldOffset = new Vector3(0f, 1.2f, 0f),
            SlotSpacingY = 0.2f
        };

        // BaseCamp：Slot0 使用 BaseCampHPBar，Slot1 使用 BaseCampCorruptionBar；缺失则逐级回退到 EnemyHPBar。
        if (enemyPrefab1 == null)
        {
            CY.LogWarning("[HPBarManager] 未找到 BaseCampHPBar 资源，Slot0 临时回退 EnemyHPBar。");
            enemyPrefab1 = enemyPrefab;
        }

        if (enemyPrefab2 == null)
        {
            CY.LogWarning("[HPBarManager] 未找到 BaseCampCorruptionBar 资源，Slot1 临时回退 BaseCampHPBar/EnemyHPBar。");
            enemyPrefab2 = enemyPrefab1 ?? enemyPrefab;
        }

        var baseCampSlot0 = new HPBarStyleSetting
        {
            Style = HPBarStyle.BaseCamp,
            Prefab = enemyPrefab1,
            WorldOffset = new Vector3(0f, 0f, 0f),
            SlotSpacingY = 1f
        };

        var baseCampSlot1 = new HPBarStyleSetting
        {
            Style = HPBarStyle.BaseCamp,
            Prefab = enemyPrefab2,
            WorldOffset = new Vector3(0f, 0f, 0f),
            SlotSpacingY = 1f
        };

        _styleMap[HPBarStyle.BaseCamp] = baseCampSlot0;
        _styleSlotMap[new StyleSlotKey(HPBarStyle.BaseCamp, 0)] = baseCampSlot0;
        _styleSlotMap[new StyleSlotKey(HPBarStyle.BaseCamp, 1)] = baseCampSlot1;

        // Boss：默认复用 EnemyHPBar，后续如需专用样式可在此调整。
        _styleMap[HPBarStyle.Boss] = new HPBarStyleSetting
        {
            Style = HPBarStyle.Boss,
            Prefab = enemyPrefab,
            WorldOffset = new Vector3(0f, 0f, 0f),
            SlotSpacingY = 0f
        };
    }

    private bool TryGetStyleSetting(HPBarStyle style, byte slot, out HPBarStyleSetting setting)
    {
        if (_styleSlotMap.TryGetValue(new StyleSlotKey(style, slot), out setting))
        {
            return true;
        }

        return _styleMap.TryGetValue(style, out setting);
    }

    /// <summary>
    /// 解析血条跟随的目标 Transform。
    /// 规则：
    /// - 优先使用 UnitManager 的实体（CY.Unit.GetUnit）。
    /// - 若找不到且是 BaseCamp 样式，则使用 UnitManager.BaseCampPoint。
    /// </summary>
    private bool TryResolveTargetTransform(ref UnitHPChangedEvent evt, out Transform targetTransform)
    {
        targetTransform = null;

        if (CY.Unit != null)
        {
            var unit = CY.Unit.GetUnit(evt.UnitID);
            if (unit != null)
            {
                targetTransform = unit.transform;
                return true;
            }

            if (evt.BarStyle == HPBarStyle.BaseCamp && CY.Unit.BaseCampPoint != null)
            {
                targetTransform = CY.Unit.BaseCampPoint;
                return true;
            }
        }

        return false;
    }

    private UIUnitHPBar SpawnBar(HPBarStyle style, GameObject prefab)
    {
        if (prefab == null)
        {
            CY.LogError($"[HPBarManager] style={style} 的血条 Prefab 未配置！");
            return null;
        }

        if (!_pools.TryGetValue(style, out var pool))
        {
            pool = new Queue<UIUnitHPBar>(16);
            _pools[style] = pool;
        }

        UIUnitHPBar bar;
        if (pool.Count > 0)
        {
            bar = pool.Dequeue();
            bar.gameObject.SetActive(true);
        }
        else
        {
            var go = Instantiate(prefab, transform);
            bar = go.GetComponent<UIUnitHPBar>();
            if (bar == null) bar = go.AddComponent<UIUnitHPBar>();
        }

        return bar;
    }

    private void RecycleBar(HPBarStyle style, UIUnitHPBar bar)
    {
        if (bar == null) return;

        bar.gameObject.SetActive(false);

        if (!_pools.TryGetValue(style, out var pool))
        {
            pool = new Queue<UIUnitHPBar>(16);
            _pools[style] = pool;
        }

        pool.Enqueue(bar);
    }

    /// <summary>
    /// 移除某个 UnitID 的所有血条（用于 UnitDead/BaseCamp 销毁）。
    /// 注意：这是低频路径，使用静态 List 缓存避免反复分配。
    /// </summary>
    private void RemoveAllBarsForUnit(int unitId)
    {
        if (_activeBars.Count == 0) return;

        s_removeKeysCache.Clear();
        foreach (var kv in _activeBars)
        {
            if (kv.Key.UnitId == unitId)
            {
                s_removeKeysCache.Add(kv.Key);
            }
        }

        for (int i = 0; i < s_removeKeysCache.Count; i++)
        {
            var key = s_removeKeysCache[i];
            if (_activeBars.TryGetValue(key, out var bar))
            {
                RecycleBar(key.Style, bar);
            }
            _activeBars.Remove(key);
        }

        s_removeKeysCache.Clear();
    }

    /// <summary>
    /// 主动请求 BaseCamp 发送一次初始血量事件。
    /// 说明：事件总线默认不做缓存，若 BaseCamp 在本面板订阅之前已发过“初始 HP 事件”，UI 会错过。
    /// </summary>
    private void TryRequestBaseCampInitialBars()
    {
        if (CY.Unit == null || CY.Unit.BaseCampPoint == null) return;

        var baseCamp = CY.Unit.BaseCampPoint.GetComponent<BaseCamp>();
        if (baseCamp == null) return;

        baseCamp.PostInitialHPEvents();
    }
}
