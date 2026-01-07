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
    /// <summary>公司良心当前值（运行时）。</summary>
    private int _companyConscienceCurrent;
    /// <summary>公司污染当前进度（运行时，0~阈值-1）。</summary>
    private int _companyPollutionCurrent;
    /// <summary>公司良心伤害累计缓冲（用于换算扣点）。</summary>
    private int _companyConscienceDamageBuffer;
    /// <summary>公司污染伤害累计缓冲（用于换算涨点）。</summary>
    private float _companyPollutionDamageBuffer;

    /// <summary>
    /// 当前战斗初始数据（已缓存）。
    /// </summary>
    public BattleData BattleData => _battleData;
    /// <summary>公司良心当前值（只读）。</summary>
    public int CompanyConscienceCurrent => _companyConscienceCurrent;
    /// <summary>公司良心最大值（只读）。</summary>
    public int CompanyConscienceMax => _battleData != null ? _battleData.CompanyConscience : 0;
    /// <summary>公司污染当前进度（只读）。</summary>
    public int CompanyPollutionCurrent => _companyPollutionCurrent;
    /// <summary>公司污染阈值（只读）。</summary>
    public int CompanyPollutionThreshold => _battleData != null ? _battleData.CompanyPollution : 0;

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
    /// 应用公司受击伤害（良心 + 污染）。
    /// </summary>
    /// <param name="conscienceDamage">良心伤害。</param>
    /// <param name="pollutionDamage">污染伤害。</param>
    public void ApplyCompanyDamage(int conscienceDamage, float pollutionDamage) // 公司伤害入口
    {
        if (_battleData == null)
        {
            return; // 未加载战斗数据时直接返回
        }

        if (conscienceDamage > 0)
        {
            var previous = _companyConscienceCurrent; // 缓存旧良心值
            _companyConscienceDamageBuffer += conscienceDamage; // 累加良心伤害
            var perPoint = GetCompanyConscienceDamagePerPoint(); // 读取每点扣减阈值
            var decrease = _companyConscienceDamageBuffer / perPoint; // 计算可扣减点数
            if (decrease > 0)
            {
                _companyConscienceDamageBuffer -= decrease * perPoint; // 扣除已换算伤害
                _companyConscienceCurrent = Mathf.Max(0, _companyConscienceCurrent - decrease); // 扣减良心并做下限保护
                if (_companyConscienceCurrent != previous)
                {
                    PostCompanyConscienceChanged(previous, _companyConscienceCurrent); // 派发良心变化事件
                }
            }
        }

        if (pollutionDamage > 0f)
        {
            var previous = _companyPollutionCurrent; // 缓存旧污染值
            _companyPollutionDamageBuffer += pollutionDamage; // 累加污染伤害
            var perPoint = GetCompanyPollutionDamagePerPoint(); // 读取每点增长阈值
            var increase = (int)(_companyPollutionDamageBuffer / perPoint); // 计算可增长点数
            if (increase > 0)
            {
                _companyPollutionDamageBuffer -= increase * perPoint; // 扣除已换算伤害
                var threshold = GetCompanyPollutionThreshold(); // 读取污染触发阈值
                var total = _companyPollutionCurrent + increase; // 计算累计污染值
                var triggerCount = total / threshold; // 计算触发次数
                _companyPollutionCurrent = total % threshold; // 回收为阈值内进度
                if (_companyPollutionCurrent != previous)
                {
                    PostCompanyPollutionChanged(previous, _companyPollutionCurrent, threshold); // 派发污染变化事件
                }

                if (triggerCount > 0)
                {
                    PostCompanyPollutionReached(triggerCount, threshold); // 派发污染触发事件
                }
            }
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
    /// 重置公司运行时数据（在数据表加载完成后调用）。
    /// </summary>
    private void ResetCompanyRuntimeState() // 公司运行时重置入口
    {
        _companyConscienceCurrent = Mathf.Max(0, CompanyConscienceMax); // 重置良心当前值
        _companyPollutionCurrent = 0; // 重置污染进度为 0
        _companyConscienceDamageBuffer = 0; // 清空良心伤害累计
        _companyPollutionDamageBuffer = 0f; // 清空污染伤害累计
    }

    /// <summary>
    /// 获取良心每点扣减所需累计伤害（带下限保护）。
    /// </summary>
    private int GetCompanyConscienceDamagePerPoint() // 良心阈值读取入口
    {
        var value = _battleData != null ? _battleData.CompanyConscienceDamagePerPoint : 0; // 读取配置值
        return value > 0 ? value : 1; // 返回有效值
    }

    /// <summary>
    /// 获取污染每点增长所需累计伤害（带下限保护）。
    /// </summary>
    private float GetCompanyPollutionDamagePerPoint() // 污染阈值读取入口
    {
        var value = _battleData != null ? _battleData.CompanyPollutionDamagePerPoint : 0; // 读取配置值
        return value > 0 ? value : 1f; // 返回有效值
    }

    /// <summary>
    /// 获取污染触发阈值（带下限保护）。
    /// </summary>
    private int GetCompanyPollutionThreshold() // 污染阈值读取入口
    {
        var value = _battleData != null ? _battleData.CompanyPollution : 0; // 读取配置值
        return value > 0 ? value : 1; // 返回有效值
    }

    /// <summary>
    /// 派发公司良心变化事件。
    /// </summary>
    /// <param name="previous">变化前良心。</param>
    /// <param name="current">变化后良心。</param>
    private void PostCompanyConscienceChanged(int previous, int current) // 良心事件派发入口
    {
        var evt = new CompanyConscienceChangedEvent // 创建事件结构体
        {
            CurrentValue = current, // 写入当前良心
            MaxValue = CompanyConscienceMax, // 写入良心最大值
            Delta = current - previous // 写入变化量
        };
        CY.Event.Post(ref evt); // 派发事件
    }

    /// <summary>
    /// 派发公司污染变化事件。
    /// </summary>
    /// <param name="previous">变化前污染进度。</param>
    /// <param name="current">变化后污染进度。</param>
    /// <param name="threshold">污染阈值。</param>
    private void PostCompanyPollutionChanged(int previous, int current, int threshold) // 污染事件派发入口
    {
        var evt = new CompanyPollutionChangedEvent // 创建事件结构体
        {
            CurrentValue = current, // 写入当前污染进度
            ThresholdValue = threshold, // 写入污染阈值
            Delta = current - previous // 写入变化量
        };
        CY.Event.Post(ref evt); // 派发事件
    }

    /// <summary>
    /// 派发公司污染触发事件。
    /// </summary>
    /// <param name="triggerCount">触发次数。</param>
    /// <param name="threshold">污染阈值。</param>
    private void PostCompanyPollutionReached(int triggerCount, int threshold) // 污染触发派发入口
    {
        var evt = new CompanyPollutionReachedEvent // 创建事件结构体
        {
            TriggerCount = triggerCount, // 写入触发次数
            ThresholdValue = threshold // 写入污染阈值
        };
        CY.Event.Post(ref evt); // 派发事件
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

        _battleData = row; // 缓存战斗数据
        ResetCompanyRuntimeState(); // 重置公司运行时状态
        return true;
    }
}
