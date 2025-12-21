using CYFramework;
using CYFramework.Infrastructure;
using UnityEngine;

/// <summary>
/// 战斗数据管理器（单类方案：可挂载 + 完整生命周期接口）。
/// 设计意图：由框架驱动生命周期，Unity 只负责创建/销毁。
/// </summary>
public sealed class BattleDataManager : MonoBehaviour,
    IInitializable, ITickable, IUpdateable, ILateUpdateable, IPausable, IDisposableEx
{
    private const string BattleDataTableName = "BattleData";

    [SerializeField] private bool _dontDestroyOnLoad = true;

    // 数值越小越早执行；Dispose 为数值大的先销毁。
    public int InitOrder => 100;
    public int TickOrder => 50;
    public int UpdateOrder => 200;
    public int LateUpdateOrder => 200;
    public int DisposeOrder => -100;

    private bool _registered;
    private bool _disposed;
    private bool _needRetryLoad;
    private BattleData _battleData;

    /// <summary>
    /// 当前战斗初始数据（已缓存）。
    /// </summary>
    public BattleData BattleData => _battleData;

    private void Awake()
    {
        // 场景可能重复挂载，使用 ServiceLocator 保证单例并避免重复注册。
        if (ServiceLocator.TryGet<BattleDataManager>(out var existing) && existing != this)
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
        // 只注销自身注册，避免误删其它实例。
        if (_registered)
        {
            Dispose();
            ServiceLocator.Unregister<BattleDataManager>();
            _registered = false;
        }
    }

    /// <summary>
    /// 初始化（由 ServiceLocator 驱动，只会执行一次）。
    /// </summary>
    public void Initialize()
    {
        // 初始化时尝试缓存战斗数据；若尚未加载则延迟到 Update 再试。
        _needRetryLoad = !TryCacheBattleData();
    }

    /// <summary>
    /// 固定帧逻辑（FixedUpdate）。
    /// </summary>
    public void Tick(float deltaTime)
    {
        // TODO: 固定帧逻辑
    }

    /// <summary>
    /// 每帧逻辑（Update）。
    /// </summary>
    public void OnUpdate(float deltaTime)
    {
        if (_needRetryLoad)
        {
            _needRetryLoad = !TryCacheBattleData();
        }
    }

    /// <summary>
    /// 收尾逻辑（LateUpdate）。
    /// </summary>
    public void OnLateUpdate(float deltaTime)
    {
        // TODO: 收尾逻辑
    }

    /// <summary>
    /// 暂停回调（切后台）。
    /// </summary>
    public void OnPause()
    {
        // TODO: 暂停逻辑
    }

    /// <summary>
    /// 恢复回调（切前台）。
    /// </summary>
    public void OnResume(float pauseDuration)
    {
        // TODO: 恢复逻辑
    }

    /// <summary>
    /// 销毁清理。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // TODO: 清理资源
    }

    /// <summary>
    /// 从数据表缓存一次战斗数据（JSON 单对象加载默认 Id=1）。
    /// </summary>
    private bool TryCacheBattleData()
    {
        var table = CY.Data.GetDataTable<BattleData>(BattleDataTableName);
        if (table == null || table.Count == 0)
        {
            return false;
        }

        // JSON 单对象自动补 Id=1，因此优先读取 Id=1。
        var row = table.GetRow(1);
        if (row == null)
        {
            var rows = table.GetAllRows();
            row = rows != null && rows.Count > 0 ? rows[0] : null;
        }

        if (row == null)
        {
            return false;
        }

        _battleData = row;
        return true;
    }
}
