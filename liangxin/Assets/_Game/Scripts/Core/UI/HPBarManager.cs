using System.Collections.Generic;
using CYFramework;
using CYFramework.Core.UI;
using UnityEngine;

// 专门管理战斗血条的 Manager (不是 UIPanel，而是挂在 Panel 下或者独立存在的逻辑)
// 建议作为一个 UIPanel (Always Open) 或者一个 MonoManager
[UIPrefab("Prefabs/UI/Battle/HPBarPanel")]
public class HPBarManager : UIPanel
{
    [Header("Config")]
    [SerializeField] private GameObject _hpBarPrefab; // 血条预制体
    [SerializeField] private Vector3 _offset = new Vector3(0, 1.2f, 0); // 血条偏移量

    // 活动中的血条：UnitID -> Bar
    private Dictionary<int, UIUnitHPBar> _activeBars = new Dictionary<int, UIUnitHPBar>();
    
    // 对象池
    private Queue<UIUnitHPBar> _pool = new Queue<UIUnitHPBar>();

    private RectTransform _rectTransform;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _rectTransform = GetComponent<RectTransform>();

        // [Fix] 强制设置自身为全屏拉伸，确保坐标系覆盖全屏
        // 如果 Panel 很小，WorldToScreenPoint 转 Local 后的坐标就会偏离视觉预期
        if (_rectTransform)
        {
            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }

        // 自动加载血条预制体 (路径: Assets/_Game/Resources/Prefabs/UI/Widgets/EnemyHPBar.prefab)
        if (_hpBarPrefab == null)
        {
            _hpBarPrefab = CY.Resource.Load<GameObject>("Prefabs/UI/Widgets/EnemyHPBar");
            if (_hpBarPrefab == null)
            {
                CY.LogError("[HPBarManager] 无法在 Resources 中找到血条预制体: Prefabs/UI/Widgets/EnemyHPBar");
            }
        }

        // 订阅事件
        CY.Event.Subscribe<UnitHPChangedEvent>(OnUnitHPChanged);
        CY.Event.Subscribe<UnitDeadEvent>(OnUnitDead);
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        CY.Event.Unsubscribe<UnitHPChangedEvent>(OnUnitHPChanged);
        CY.Event.Unsubscribe<UnitDeadEvent>(OnUnitDead);
        base.OnClose(isShutdown, userData);
    }

    protected override void OnUpdate(float deltaTime, float realDeltaTime)
    {
        // 1. 获取 CameraManager
        var cameraMgr = CY.CameraManager;
        if (cameraMgr == null) return;

        var mainCam = cameraMgr.MainCamera;
        var uiCam = cameraMgr.UICamera;

        if (mainCam == null) return;

        // 2. 集中更新所有血条
        foreach (var kvp in _activeBars)
        {
            var bar = kvp.Value;
            if (bar.IsActive)
            {
                bar.UpdatePosition(mainCam, uiCam, _rectTransform);
            }
        }
    }

    private void OnUnitHPChanged(ref UnitHPChangedEvent evt)
    {
        // 获取或创建血条
        if (!_activeBars.TryGetValue(evt.UnitID, out var bar))
        {
            // 如果是第一次受伤，且还没有血条，则创建
            // 这里假设：只有受伤了才显示血条 (节省性能)
            // 如果需要常驻，可以在 Unit 初始化时发一个 FullHP 事件
            
            // 修正逻辑：必须通过 ID 拿到 Transform 才能跟随。
            // 我们去 CY.Unit 查
            var unit = CY.Unit.GetUnit(evt.UnitID);
            
            if (unit != null)
            {
                bar = SpawnBar();
                if (bar == null) return; // 防御性编程：如果 Prefab 没配，直接返回
                bar.Init(evt.UnitID, unit.transform, _offset);
                _activeBars[evt.UnitID] = bar;
            }
            else
            {
                CY.LogWarning($"[HPBarManager] Unit {evt.UnitID} not found in UnitManager!");
                // Unit 找不到了？那就不显示了
                if (bar != null) RecycleBar(bar);
                return;
            }
        }

        // 更新血量
        bar.UpdateHP(evt.CurrentHP, evt.MaxHP);
        
        // 死亡逻辑由 DeadEvent 处理，或者 HP <= 0 处理
        if (evt.IsDead || evt.CurrentHP <= 0)
        {
            // 可以延迟回收，播个死亡动画
            RecycleBar(bar);
            _activeBars.Remove(evt.UnitID);
        }
    }

    private void OnUnitDead(ref UnitDeadEvent evt)
    {
        if (_activeBars.TryGetValue(evt.UnitID, out var bar))
        {
            RecycleBar(bar);
            _activeBars.Remove(evt.UnitID);
        }
    }

    // 对象池逻辑
    private UIUnitHPBar SpawnBar()
    {
        if (_hpBarPrefab == null)
        {
            CY.LogError("[HPBarManager] Critical Error: _hpBarPrefab is not assigned in Inspector!");
            return null;
        }

        UIUnitHPBar bar;
        if (_pool.Count > 0)
        {
            bar = _pool.Dequeue();
            bar.gameObject.SetActive(true);
        }
        else
        {
            var go = Instantiate(_hpBarPrefab, transform);
            bar = go.GetComponent<UIUnitHPBar>();
            if (bar == null) bar = go.AddComponent<UIUnitHPBar>();
        }
        return bar;
    }

    private void RecycleBar(UIUnitHPBar bar)
    {
        bar.gameObject.SetActive(false);
        _pool.Enqueue(bar);
    }
}
