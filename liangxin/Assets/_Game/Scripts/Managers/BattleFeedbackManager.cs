using System.Collections.Generic; // 集合类型引用
using CYFramework; // CYFramework 入口引用
using CYFramework.Infrastructure; // 生命周期接口引用
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 战斗反馈管理器：统一管理血条与伤害飘字（单管理器方案）。
/// </summary>
public sealed class BattleFeedbackManager : MonoBehaviour, IInitializable, IUpdateable, IDisposableEx // 管理器组件
{
    /// <summary>是否在切场景时保留该对象。</summary>
    [SerializeField] private bool _dontDestroyOnLoad = true;
    /// <summary>UI 根节点（全屏 RectTransform）。</summary>
    [SerializeField] private RectTransform _uiRoot;
    /// <summary>世界相机（用于世界转屏幕坐标）。</summary>
    [SerializeField] private Camera _worldCamera;
    /// <summary>UI 相机（Overlay 可为空）。</summary>
    [SerializeField] private Camera _uiCamera;
    /// <summary>血条预制体。</summary>
    [SerializeField] private UnitHpBarItem _hpBarPrefab;
    /// <summary>伤害飘字预制体。</summary>
    [SerializeField] private DamageTextItem _damageTextPrefab;
    /// <summary>血条预热数量。</summary>
    [SerializeField] private int _prewarmHpBarCount = 16;
    /// <summary>飘字预热数量。</summary>
    [SerializeField] private int _prewarmDamageCount = 32;
    /// <summary>血条基础世界偏移。</summary>
    [SerializeField] private Vector2 _hpBarWorldOffset = new Vector2(0f, 0.6f);
    /// <summary>飘字基础世界偏移。</summary>
    [SerializeField] private Vector2 _damageWorldOffset = new Vector2(0f, 0.2f);
    /// <summary>是否使用碰撞体顶部作为额外偏移。</summary>
    [SerializeField] private bool _useColliderTopOffset = true;

    /// <summary>血条对象池。</summary>
    private readonly Queue<UnitHpBarItem> _hpBarPool = new Queue<UnitHpBarItem>(64);
    /// <summary>激活血条列表。</summary>
    private readonly List<UnitHpBarItem> _activeHpBars = new List<UnitHpBarItem>(64);
    /// <summary>单位 Id 对应血条。</summary>
    private readonly Dictionary<int, UnitHpBarItem> _hpBarMap = new Dictionary<int, UnitHpBarItem>(64);
    /// <summary>飘字对象池。</summary>
    private readonly Queue<DamageTextItem> _damagePool = new Queue<DamageTextItem>(128);
    /// <summary>激活飘字列表。</summary>
    private readonly List<DamageTextItem> _activeDamageTexts = new List<DamageTextItem>(128);

    /// <summary>对象池根节点。</summary>
    private RectTransform _poolRoot;
    /// <summary>是否已注册到 ServiceLocator。</summary>
    private bool _registered;
    /// <summary>是否已订阅事件。</summary>
    private bool _subscribed;
    /// <summary>是否已释放。</summary>
    private bool _disposed;
    /// <summary>是否已提示血条配置缺失。</summary>
    private bool _warnedHpConfig;
    /// <summary>是否已提示飘字配置缺失。</summary>
    private bool _warnedDamageConfig;
    /// <summary>是否已提示相机配置缺失。</summary>
    private bool _warnedCameraConfig;

    /// <summary>初始化顺序（数值小的先执行）。</summary>
    public int InitOrder => 180;
    /// <summary>更新顺序（数值小的先执行）。</summary>
    public int UpdateOrder => 380;
    /// <summary>释放顺序（数值大的先释放）。</summary>
    public int DisposeOrder => -180;

    private void Awake()
    {
        if (ServiceLocator.TryGet<BattleFeedbackManager>(out var existing) && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        if (_dontDestroyOnLoad && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        ServiceLocator.RegisterInstance(this);
        _registered = true;
    }

    private void OnDestroy()
    {
        if (_registered)
        {
            Dispose();
            ServiceLocator.Unregister<BattleFeedbackManager>();
            _registered = false;
        }
    }

    /// <summary>
    /// 初始化（由 ServiceLocator 驱动，只会执行一次）。
    /// </summary>
    public void Initialize()
    {
        CacheCamera();
        EnsurePoolRoot();
        PrewarmPools();
        EnsureSubscribed();
    }

    /// <summary>
    /// 每帧更新（刷新血条跟随与飘字动画）。
    /// </summary>
    /// <param name="deltaTime">帧间隔。</param>
    public void OnUpdate(float deltaTime)
    {
        if (_disposed)
        {
            return;
        }

        UpdateHpBars();
        UpdateDamageTexts(deltaTime);
    }

    /// <summary>
    /// 释放清理。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_subscribed)
        {
            CY.Event.UnsubscribeAll(this);
            _subscribed = false;
        }

        _hpBarMap.Clear();
        _activeHpBars.Clear();
        _activeDamageTexts.Clear();
    }

    /// <summary>
    /// 缓存世界相机（若未指定则尝试 Camera.main）。
    /// </summary>
    private void CacheCamera()
    {
        if (_worldCamera == null)
        {
            _worldCamera = Camera.main;
        }
    }

    /// <summary>
    /// 确保对象池根节点存在。
    /// </summary>
    private void EnsurePoolRoot()
    {
        if (_poolRoot != null)
        {
            return;
        }

        var go = new GameObject("[BattleFeedbackPools]");
        go.SetActive(false);
        _poolRoot = go.AddComponent<RectTransform>();
        _poolRoot.SetParent(transform, false);
    }

    /// <summary>
    /// 预热对象池。
    /// </summary>
    private void PrewarmPools()
    {
        if (_hpBarPrefab != null)
        {
            for (int i = 0; i < _prewarmHpBarCount; i++)
            {
                var item = CreateHpBarInstance();
                if (item != null)
                {
                    _hpBarPool.Enqueue(item);
                }
            }
        }

        if (_damageTextPrefab != null)
        {
            for (int i = 0; i < _prewarmDamageCount; i++)
            {
                var item = CreateDamageTextInstance();
                if (item != null)
                {
                    _damagePool.Enqueue(item);
                }
            }
        }
    }

    /// <summary>
    /// 确保事件订阅。
    /// </summary>
    private void EnsureSubscribed()
    {
        if (_subscribed)
        {
            return;
        }

        CY.Event.Subscribe<UnitSpawnedEvent>(OnUnitSpawned, this);
        CY.Event.Subscribe<UnitDespawnedEvent>(OnUnitDespawned, this);
        CY.Event.Subscribe<UnitHpChangedEvent>(OnUnitHpChanged, this);
        CY.Event.Subscribe<UnitDamagePopupEvent>(OnUnitDamagePopup, this);
        CY.Event.Subscribe<UnitLifeStateChangedEvent>(OnUnitLifeStateChanged, this);
        _subscribed = true;
    }

    /// <summary>
    /// 单位生成事件回调。
    /// </summary>
    private void OnUnitSpawned(ref UnitSpawnedEvent evt)
    {
        if (_disposed)
        {
            return;
        }

        if (evt.Unit == null || evt.MaxHp <= 0 || evt.Unit.LifeState == UnitLifeState.Dead)
        {
            return;
        }

        if (!EnsureHpUiReady())
        {
            return;
        }

        var unitId = evt.Unit.Id;
        if (_hpBarMap.TryGetValue(unitId, out var existing))
        {
            existing.SetHp(evt.CurrentHp, evt.MaxHp);
            return;
        }

        var item = GetHpBarFromPool();
        if (item == null)
        {
            return;
        }

        item.Bind(evt.Unit, _hpBarWorldOffset, _useColliderTopOffset);
        item.SetHp(evt.CurrentHp, evt.MaxHp);
        _hpBarMap[unitId] = item;
        _activeHpBars.Add(item);
    }

    /// <summary>
    /// 单位移除事件回调。
    /// </summary>
    private void OnUnitDespawned(ref UnitDespawnedEvent evt)
    {
        if (_disposed || evt.Unit == null)
        {
            return;
        }

        var unitId = evt.Unit.Id;
        if (_hpBarMap.TryGetValue(unitId, out var item))
        {
            _hpBarMap.Remove(unitId);
            RemoveHpBarFromList(item);
        }
    }

    /// <summary>
    /// 单位生命变化事件回调。
    /// </summary>
    private void OnUnitHpChanged(ref UnitHpChangedEvent evt)
    {
        if (_disposed)
        {
            return;
        }

        if (evt.Unit == null || evt.MaxHp <= 0 || evt.Unit.LifeState == UnitLifeState.Dead)
        {
            return;
        }

        if (!EnsureHpUiReady())
        {
            return;
        }

        var unitId = evt.Unit.Id;
        if (!_hpBarMap.TryGetValue(unitId, out var item))
        {
            item = GetHpBarFromPool();
            if (item == null)
            {
                return;
            }

            item.Bind(evt.Unit, _hpBarWorldOffset, _useColliderTopOffset);
            _hpBarMap[unitId] = item;
            _activeHpBars.Add(item);
        }

        item.SetHp(evt.CurrentHp, evt.MaxHp);
    }

    /// <summary>
    /// 伤害飘字事件回调。
    /// </summary>
    private void OnUnitDamagePopup(ref UnitDamagePopupEvent evt)
    {
        if (_disposed)
        {
            return;
        }

        if (evt.Unit == null || evt.Damage <= 0)
        {
            return;
        }

        if (!EnsureDamageUiReady())
        {
            return;
        }

        var item = GetDamageTextFromPool();
        if (item == null)
        {
            return;
        }

        var worldPos = ResolveDamageWorldPosition(evt.Unit);
        item.Show(worldPos, evt.Damage, evt.IsCrit);
        _activeDamageTexts.Add(item);
    }

    /// <summary>
    /// 单位生命状态变化事件回调。
    /// </summary>
    private void OnUnitLifeStateChanged(ref UnitLifeStateChangedEvent evt)
    {
        if (_disposed)
        {
            return;
        }

        if (evt.Unit == null)
        {
            return;
        }

        if (evt.NewState != UnitLifeState.Dead)
        {
            return;
        }

        var unitId = evt.Unit.Id;
        if (_hpBarMap.TryGetValue(unitId, out var item))
        {
            _hpBarMap.Remove(unitId);
            RemoveHpBarFromList(item);
        }
    }

    /// <summary>
    /// 更新血条跟随位置。
    /// </summary>
    private void UpdateHpBars()
    {
        if (_activeHpBars.Count == 0)
        {
            return;
        }

        if (_uiRoot == null || _worldCamera == null)
        {
            return;
        }

        for (int i = _activeHpBars.Count - 1; i >= 0; i--)
        {
            var item = _activeHpBars[i];
            if (item == null || !item.HasTarget)
            {
                RemoveHpBarAt(i);
                continue;
            }

            item.UpdatePosition(_uiRoot, _worldCamera, _uiCamera);
        }
    }

    /// <summary>
    /// 更新飘字动画与位置。
    /// </summary>
    /// <param name="deltaTime">帧间隔。</param>
    private void UpdateDamageTexts(float deltaTime)
    {
        if (_activeDamageTexts.Count == 0)
        {
            return;
        }

        for (int i = _activeDamageTexts.Count - 1; i >= 0; i--)
        {
            var item = _activeDamageTexts[i];
            if (item == null || !item.Tick(deltaTime, _uiRoot, _worldCamera, _uiCamera))
            {
                RemoveDamageTextAt(i);
            }
        }
    }

    /// <summary>
    /// 从池中获取血条实例。
    /// </summary>
    private UnitHpBarItem GetHpBarFromPool()
    {
        var item = _hpBarPool.Count > 0 ? _hpBarPool.Dequeue() : CreateHpBarInstance();
        if (item == null)
        {
            return null;
        }

        item.gameObject.SetActive(true);
        item.transform.SetParent(_uiRoot, false);
        return item;
    }

    /// <summary>
    /// 从池中获取飘字实例。
    /// </summary>
    private DamageTextItem GetDamageTextFromPool()
    {
        var item = _damagePool.Count > 0 ? _damagePool.Dequeue() : CreateDamageTextInstance();
        if (item == null)
        {
            return null;
        }

        item.gameObject.SetActive(true);
        item.transform.SetParent(_uiRoot, false);
        return item;
    }

    /// <summary>
    /// 创建血条实例（放入对象池）。
    /// </summary>
    private UnitHpBarItem CreateHpBarInstance()
    {
        if (_hpBarPrefab == null || _poolRoot == null)
        {
            return null;
        }

        var item = Instantiate(_hpBarPrefab, _poolRoot);
        item.gameObject.SetActive(false);
        return item;
    }

    /// <summary>
    /// 创建飘字实例（放入对象池）。
    /// </summary>
    private DamageTextItem CreateDamageTextInstance()
    {
        if (_damageTextPrefab == null || _poolRoot == null)
        {
            return null;
        }

        var item = Instantiate(_damageTextPrefab, _poolRoot);
        item.gameObject.SetActive(false);
        return item;
    }

    /// <summary>
    /// 回收血条实例。
    /// </summary>
    private void RecycleHpBar(UnitHpBarItem item)
    {
        if (item == null)
        {
            return;
        }

        item.Unbind();
        var parent = _poolRoot != null ? _poolRoot : transform;
        item.transform.SetParent(parent, false);
        item.gameObject.SetActive(false);
        _hpBarPool.Enqueue(item);
    }

    /// <summary>
    /// 从列表中移除并回收血条。
    /// </summary>
    private void RemoveHpBarAt(int index)
    {
        var item = _activeHpBars[index];
        _activeHpBars.RemoveAt(index);
        RecycleHpBar(item);
    }

    /// <summary>
    /// 从列表中移除并回收血条（指定实例）。
    /// </summary>
    private void RemoveHpBarFromList(UnitHpBarItem item)
    {
        _activeHpBars.Remove(item);
        RecycleHpBar(item);
    }

    /// <summary>
    /// 回收飘字实例。
    /// </summary>
    private void RemoveDamageText(DamageTextItem item)
    {
        if (item == null)
        {
            return;
        }

        var parent = _poolRoot != null ? _poolRoot : transform;
        item.transform.SetParent(parent, false);
        item.gameObject.SetActive(false);
        _damagePool.Enqueue(item);
    }

    /// <summary>
    /// 从列表中移除并回收飘字。
    /// </summary>
    private void RemoveDamageTextAt(int index)
    {
        var item = _activeDamageTexts[index];
        _activeDamageTexts.RemoveAt(index);
        RemoveDamageText(item);
    }

    /// <summary>
    /// 确保血条 UI 配置有效。
    /// </summary>
    private bool EnsureHpUiReady()
    {
        CacheCamera();
        EnsurePoolRoot();
        if (_uiRoot == null || _hpBarPrefab == null)
        {
            if (!_warnedHpConfig)
            {
                CY.LogWarning("[BattleFeedbackManager] 血条 UI 配置缺失，无法显示血条。");
                _warnedHpConfig = true;
            }

            return false;
        }

        if (_worldCamera == null)
        {
            if (!_warnedCameraConfig)
            {
                CY.LogWarning("[BattleFeedbackManager] 未设置世界相机，血条无法跟随。");
                _warnedCameraConfig = true;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 确保飘字 UI 配置有效。
    /// </summary>
    private bool EnsureDamageUiReady()
    {
        CacheCamera();
        EnsurePoolRoot();
        if (_uiRoot == null || _damageTextPrefab == null)
        {
            if (!_warnedDamageConfig)
            {
                CY.LogWarning("[BattleFeedbackManager] 飘字 UI 配置缺失，无法显示飘字。");
                _warnedDamageConfig = true;
            }

            return false;
        }

        if (_worldCamera == null)
        {
            if (!_warnedCameraConfig)
            {
                CY.LogWarning("[BattleFeedbackManager] 未设置世界相机，飘字无法跟随。");
                _warnedCameraConfig = true;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 计算世界偏移（可叠加碰撞体顶部偏移）。
    /// </summary>
    private Vector2 ResolveWorldOffset(UnitEntity unit, Vector2 baseOffset)
    {
        if (!_useColliderTopOffset || unit == null)
        {
            return baseOffset;
        }

        var collider = unit.GetComponent<Collider2D>();
        if (collider != null && collider.enabled)
        {
            var topOffset = collider.bounds.max.y - unit.transform.position.y;
            baseOffset.y += topOffset;
        }

        return baseOffset;
    }

    /// <summary>
    /// 计算伤害飘字的世界坐标（优先在 Box 范围内随机）。
    /// </summary>
    /// <param name="unit">受击单位。</param>
    private Vector2 ResolveDamageWorldPosition(UnitEntity unit)
    {
        if (unit == null)
        {
            return Vector2.zero;
        }

        var collider = unit.GetComponent<Collider2D>();
        if (collider != null && collider.enabled)
        {
            var bounds = collider.bounds;
            var x = Random.Range(bounds.min.x, bounds.max.x);
            var y = Random.Range(bounds.min.y, bounds.max.y);
            return new Vector2(x, y);
        }

        return (Vector2)unit.transform.position + _damageWorldOffset;
    }
}
