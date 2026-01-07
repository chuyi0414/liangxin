using System.Collections.Generic; // 集合类型引用
using CYFramework; // CYFramework 入口引用
using CYFramework.Core.Config; // 配置系统引用
using CYFramework.Core.Pool; // 对象池系统引用
using CYFramework.Core.UI; // UI 系统引用
using CYFramework.Infrastructure; // 生命周期接口引用
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 战斗反馈管理器：统一管理血条与伤害飘字（单管理器方案）。
/// </summary>
public sealed class BattleFeedbackManager : MonoBehaviour, IInitializable, IUpdateable, IDisposableEx // 管理器组件
{
    /// <summary>是否在切场景时保留该对象。</summary>
    [SerializeField] private bool _dontDestroyOnLoad = true;
    /// <summary>UI 层级根节点（运行时创建的 BattleFeedback 容器）。</summary>
    private RectTransform _uiLayerRoot;
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

    /// <summary>UI 层级名称（与 Main/Popup/Tips 同级）。</summary>
    private const string UiLayerName = "BattleFeedback";
    /// <summary>UI 层级排序（固定使用 50）。</summary>
    private const int UiLayerOrder = 50;
    /// <summary>血条对象池 Key。</summary>
    private const string HpBarPoolKey = "BattleFeedback_HpBar";
    /// <summary>飘字对象池 Key。</summary>
    private const string DamagePoolKey = "BattleFeedback_DamageText";

    /// <summary>血条对象池（框架 UIElementPool）。</summary>
    private UIElementPool _hpBarPool;
    /// <summary>激活血条列表。</summary>
    private readonly List<UnitHpBarItem> _activeHpBars = new List<UnitHpBarItem>(64);
    /// <summary>单位 Id 对应血条。</summary>
    private readonly Dictionary<int, UnitHpBarItem> _hpBarMap = new Dictionary<int, UnitHpBarItem>(64);
    /// <summary>飘字对象池（框架 UIElementPool）。</summary>
    private UIElementPool _damagePool;
    /// <summary>激活飘字列表。</summary>
    private readonly List<DamageTextItem> _activeDamageTexts = new List<DamageTextItem>(128);

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
    /// <summary>是否已提示 UI 层级创建失败。</summary>
    private bool _warnedUiLayerRoot;

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
        CacheCamera(); // 缓存相机引用
        EnsureUiLayerRoot(); // 创建 BattleFeedback UI 层级
        PrewarmPools(); // 预热对象池
        EnsureSubscribed(); // 订阅事件
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

        _disposed = true; // 标记已释放
        if (_subscribed)
        {
            CY.Event.UnsubscribeAll(this); // 取消事件订阅
            _subscribed = false; // 标记已取消订阅
        }

        for (int i = _activeHpBars.Count - 1; i >= 0; i--)
        {
            RemoveHpBarAt(i); // 回收血条实例
        }

        _hpBarMap.Clear(); // 清理血条映射

        for (int i = _activeDamageTexts.Count - 1; i >= 0; i--)
        {
            RemoveDamageTextAt(i); // 回收飘字实例
        }

        if (_hpBarPool != null)
        {
            _hpBarPool = null; // 释放血条池引用
        }

        if (_damagePool != null)
        {
            _damagePool = null; // 释放飘字池引用
        }
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
    /// 获取 UI 容器（BattleFeedback 层级）。
    /// </summary>
    private RectTransform GetUiContainer()
    {
        if (!EnsureUiLayerRoot())
        {
            return null; // UI 层级不可用
        }

        return _uiLayerRoot; // 返回 UI 层级根节点
    }

    /// <summary>
    /// 确保 UI 层级根节点已创建。
    /// </summary>
    private bool EnsureUiLayerRoot()
    {
        if (_uiLayerRoot != null)
        {
            ApplyUiLayerOrder(_uiLayerRoot); // 刷新层级排序
            return true; // 已有缓存
        }

        Transform layerRoot; // 层级节点
        if (CY.UI.HasLayer(UiLayerName))
        {
            layerRoot = CY.UI.GetLayerContainer(UiLayerName); // 获取已存在层级
        }
        else
        {
            layerRoot = CY.UI.CreateLayer(UiLayerName, UiLayerOrder); // 创建自定义层级
        }

        _uiLayerRoot = layerRoot as RectTransform; // 缓存层级节点
        if (_uiLayerRoot == null)
        {
            if (!_warnedUiLayerRoot)
            {
                CY.LogWarning("[BattleFeedbackManager] 创建 BattleFeedback UI 层级失败。"); // 输出警告日志
                _warnedUiLayerRoot = true; // 仅提示一次
            }

            return false; // UI 层级创建失败
        }

        ApplyUiLayerOrder(_uiLayerRoot); // 应用层级排序
        return true; // 层级创建完成
    }

    /// <summary>
    /// 应用 BattleFeedback UI 层级排序。
    /// </summary>
    /// <param name="layerRoot">层级根节点。</param>
    private void ApplyUiLayerOrder(RectTransform layerRoot)
    {
        if (layerRoot == null)
        {
            return;
        }

        var canvas = layerRoot.GetComponent<Canvas>(); // BattleFeedback Canvas
        if (canvas == null)
        {
            return; // Canvas 缺失
        }

        canvas.overrideSorting = true; // 开启排序覆盖
        canvas.sortingOrder = UiLayerOrder; // 设置固定排序

        if (_uiCamera == null && canvas.worldCamera != null)
        {
            _uiCamera = canvas.worldCamera; // 缓存 UI 相机
        }
    }

    /// <summary>
    /// 创建池配置（优先读取框架默认配置）。
    /// </summary>
    /// <param name="warmupCount">预热数量。</param>
    private PoolConfig CreatePoolConfig(int warmupCount)
    {
        var config = new PoolConfig(); // 对象池配置
        var configurator = CYConfigurator.Instance; // 配置入口
        if (configurator != null)
        {
            var poolConfig = configurator.GetConfig<PoolManagerConfig>(); // 框架池配置
            if (poolConfig != null)
            {
                config.InitialCapacity = poolConfig.DefaultInitialCapacity; // 读取默认初始容量
                config.MaxCapacity = poolConfig.DefaultMaxCapacity; // 读取默认最大容量
                config.WarmupCount = poolConfig.DefaultWarmupCount; // 读取默认预热数量
            }
        }

        config.WarmupCount = Mathf.Max(0, warmupCount); // 覆盖预热数量
        if (config.WarmupCount > config.InitialCapacity)
        {
            config.InitialCapacity = config.WarmupCount; // 保证初始容量不小于预热数量
        }

        if (config.WarmupCount > config.MaxCapacity)
        {
            config.MaxCapacity = config.WarmupCount; // 保证最大容量不小于预热数量
        }

        return config;
    }

    /// <summary>
    /// 确保血条对象池已创建。
    /// </summary>
    private void EnsureHpBarPool()
    {
        if (_hpBarPool != null)
        {
            return;
        }

        if (_hpBarPrefab == null)
        {
            return;
        }

        var config = CreatePoolConfig(_prewarmHpBarCount); // 血条池配置
        _hpBarPool = CY.UI.GetOrCreateUIElementPool(HpBarPoolKey, _hpBarPrefab.gameObject, config); // 创建血条对象池
        if (_hpBarPool == null) // 判空检查
        {
            return; // 对象池创建失败
        }

        _hpBarPool.Warmup(); // 预热血条对象池
    }

    /// <summary>
    /// 确保飘字对象池已创建。
    /// </summary>
    private void EnsureDamagePool()
    {
        if (_damagePool != null)
        {
            return;
        }

        if (_damageTextPrefab == null)
        {
            return;
        }

        var config = CreatePoolConfig(_prewarmDamageCount); // 飘字池配置
        _damagePool = CY.UI.GetOrCreateUIElementPool(DamagePoolKey, _damageTextPrefab.gameObject, config); // 创建飘字对象池
        if (_damagePool == null) // 判空检查
        {
            return; // 对象池创建失败
        }

        _damagePool.Warmup(); // 预热飘字对象池
    }

    /// <summary>
    /// 预热对象池。
    /// </summary>
    private void PrewarmPools()
    {
        EnsureHpBarPool(); // 确保血条池已创建并预热
        EnsureDamagePool(); // 确保飘字池已创建并预热
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

        var uiRoot = GetUiContainer(); // UI 容器
        if (uiRoot == null || _worldCamera == null)
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

            item.UpdatePosition(uiRoot, _worldCamera, _uiCamera); // 刷新血条位置
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

        var uiRoot = GetUiContainer(); // UI 容器
        if (uiRoot == null || _worldCamera == null)
        {
            return; // UI 层级或相机不可用
        }

        for (int i = _activeDamageTexts.Count - 1; i >= 0; i--)
        {
            var item = _activeDamageTexts[i];
            if (item == null || !item.Tick(deltaTime, uiRoot, _worldCamera, _uiCamera))
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
        EnsureHpBarPool(); // 确保血条池可用
        var uiRoot = GetUiContainer(); // UI 容器
        if (_hpBarPool == null || uiRoot == null)
        {
            return null;
        }

        var go = _hpBarPool.Get(uiRoot); // 取出血条实例
        if (go == null)
        {
            return null;
        }

        var item = go.GetComponent<UnitHpBarItem>(); // 获取血条组件
        if (item == null)
        {
            _hpBarPool.Return(go); // 归还异常实例
            return null;
        }

        return item;
    }

    /// <summary>
    /// 从池中获取飘字实例。
    /// </summary>
    private DamageTextItem GetDamageTextFromPool()
    {
        EnsureDamagePool(); // 确保飘字池可用
        var uiRoot = GetUiContainer(); // UI 容器
        if (_damagePool == null || uiRoot == null)
        {
            return null;
        }

        var go = _damagePool.Get(uiRoot); // 取出飘字实例
        if (go == null)
        {
            return null;
        }

        var item = go.GetComponent<DamageTextItem>(); // 获取飘字组件
        if (item == null)
        {
            _damagePool.Return(go); // 归还异常实例
            return null;
        }

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

        item.Unbind(); // 解除目标绑定
        if (_hpBarPool != null)
        {
            _hpBarPool.Return(item.gameObject); // 归还到血条对象池
            return;
        }

        var parent = transform; // 回收父节点
        item.transform.SetParent(parent, false); // 挂回回收节点
        item.gameObject.SetActive(false); // 关闭对象
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

        if (_damagePool != null)
        {
            _damagePool.Return(item.gameObject); // 归还到飘字对象池
            return;
        }

        var parent = transform; // 回收父节点
        item.transform.SetParent(parent, false); // 挂回回收节点
        item.gameObject.SetActive(false); // 关闭对象
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
        CacheCamera(); // 缓存相机引用
        if (!EnsureUiLayerRoot())
        {
            return false; // UI 层级不可用
        }

        if (_hpBarPrefab == null)
        {
            if (!_warnedHpConfig)
            {
                CY.LogWarning("[BattleFeedbackManager] 血条 UI 配置缺失，无法显示血条。"); // 输出警告日志
                _warnedHpConfig = true; // 仅提示一次
            }

            return false; // 血条配置缺失
        }

        if (_worldCamera == null)
        {
            if (!_warnedCameraConfig)
            {
                CY.LogWarning("[BattleFeedbackManager] 未设置世界相机，血条无法跟随。"); // 输出警告日志
                _warnedCameraConfig = true; // 仅提示一次
            }

            return false; // 相机配置缺失
        }

        return true; // 血条 UI 配置有效
    }

    /// <summary>
    /// 确保飘字 UI 配置有效。
    /// </summary>
    private bool EnsureDamageUiReady()
    {
        CacheCamera(); // 缓存相机引用
        if (!EnsureUiLayerRoot())
        {
            return false; // UI 层级不可用
        }

        if (_damageTextPrefab == null)
        {
            if (!_warnedDamageConfig)
            {
                CY.LogWarning("[BattleFeedbackManager] 飘字 UI 配置缺失，无法显示飘字。"); // 输出警告日志
                _warnedDamageConfig = true; // 仅提示一次
            }

            return false; // 飘字配置缺失
        }

        if (_worldCamera == null)
        {
            if (!_warnedCameraConfig)
            {
                CY.LogWarning("[BattleFeedbackManager] 未设置世界相机，飘字无法跟随。"); // 输出警告日志
                _warnedCameraConfig = true; // 仅提示一次
            }

            return false; // 相机配置缺失
        }

        return true; // 飘字 UI 配置有效
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
