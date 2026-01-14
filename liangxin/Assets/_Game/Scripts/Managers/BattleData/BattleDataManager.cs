using CYFramework;
using CYFramework.Core.Timer;
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
    /// <summary>是否有“新游戏重置”待执行（用于数据表尚未加载的场景）。</summary>
    private bool _pendingResetForNewGame; // 新游戏重置待执行标记
    /// <summary>待执行的新游戏重置是否需要派发事件。</summary>
    private bool _pendingResetPostEvents; // 新游戏重置事件派发标记
    private BattleData _battleData;
    /// <summary>资金当前值（运行时）。</summary>
    private int _moneyCurrent; // 资金当前值
    /// <summary>良心当前值（运行时）。</summary>
    private int _conscienceCurrent; // 良心当前值
    /// <summary>黑心当前值（运行时）。</summary>
    private int _blackHeartCurrent; // 黑心当前值
    /// <summary>黑心自动转换计时器（用于按配置时间自动转良心）。</summary>
    private Timer _blackHeartConvertTimer; // 黑心转换计时器
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
    /// <summary>资金当前值（只读）。</summary>
    public int MoneyCurrent => _moneyCurrent; // 对外只读资金
    /// <summary>良心当前值（只读）。</summary>
    public int ConscienceCurrent => _conscienceCurrent; // 对外只读良心
    /// <summary>黑心当前值（只读）。</summary>
    public int BlackHeartCurrent => _blackHeartCurrent; // 对外只读黑心
    /// <summary>黑心并发吸收槽位数量（只读）。</summary>
    public int BlackHeartAbsorbCount => GetBlackHeartAbsorbCount(); // 对外只读黑心吸收槽位数量
    /// <summary>公司良心当前值（只读）。</summary>
    public int CompanyConscienceCurrent => _companyConscienceCurrent;
    /// <summary>公司良心最大值（只读）。</summary>
    public int CompanyConscienceMax => _battleData != null ? _battleData.CompanyConscience : 0;
    /// <summary>公司污染当前进度（只读）。</summary>
    public int CompanyPollutionCurrent => _companyPollutionCurrent;
    /// <summary>公司污染阈值（只读）。</summary>
    public int CompanyPollutionThreshold => _battleData != null ? _battleData.CompanyPollution : 0;

    /// <summary>
    /// 为“新游戏”重置运行时数值（资金/良心/黑心/公司良心/公司污染）。
    /// </summary>
    /// <param name="postEvents">是否派发变化事件（用于同步 UI/系统状态）。</param>
    /// <returns>是否已立即完成重置；若数据表未加载则返回 false 并延迟到加载完成后执行。</returns>
    public bool ResetRuntimeForNewGame(bool postEvents) // 新游戏运行时重置入口
    {
        if (_battleData == null) // 战斗数据未加载时无法立即重置
        {
            _pendingResetForNewGame = true; // 标记需要在数据加载后重置
            _pendingResetPostEvents = postEvents; // 记录是否需要派发事件
            _needRetryLoad = true; // 强制进入重试加载，确保尽快获得数据表
            return false; // 返回延迟执行
        }

        var previousMoney = _moneyCurrent; // 缓存重置前资金
        var previousConscience = _conscienceCurrent; // 缓存重置前良心
        var previousBlackHeart = _blackHeartCurrent; // 缓存重置前黑心
        var previousCompanyConscience = _companyConscienceCurrent; // 缓存重置前公司良心
        var previousCompanyPollution = _companyPollutionCurrent; // 缓存重置前公司污染进度

        ResetCompanyRuntimeState(); // 重置公司运行时状态
        ResetMoneyRuntimeState(); // 重置资金运行时状态
        ResetConscienceRuntimeState(); // 重置良心运行时状态
        ResetBlackHeartRuntimeState(); // 重置黑心运行时状态

        if (!postEvents)
        {
            return true; // 不需要派发事件时直接返回完成
        }

        PostMoneyChanged(previousMoney, _moneyCurrent); // 派发资金变化事件（允许 delta=0）
        PostConscienceChanged(previousConscience, _conscienceCurrent); // 派发良心变化事件（允许 delta=0）
        PostBlackHeartChanged(previousBlackHeart, _blackHeartCurrent); // 派发黑心变化事件（允许 delta=0）
        PostCompanyConscienceChanged(previousCompanyConscience, _companyConscienceCurrent); // 派发公司良心变化事件（允许 delta=0）
        PostCompanyPollutionChanged(previousCompanyPollution, _companyPollutionCurrent, GetCompanyPollutionThreshold()); // 派发公司污染变化事件（允许 delta=0）
        return true; // 返回已完成
    }

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
    /// 增加资金并派发变化事件。
    /// </summary>
    /// <param name="amount">增加数量（必须大于 0）。</param>
    public void AddMoney(int amount) // 资金增加入口
    {
        if (_battleData == null)
        {
            return; // 未加载战斗数据时直接返回
        }

        if (amount <= 0)
        {
            return; // 无效增量时直接返回
        }

        var previous = _moneyCurrent; // 缓存旧资金值
        _moneyCurrent += amount; // 累加资金
        if (_moneyCurrent != previous)
        {
            PostMoneyChanged(previous, _moneyCurrent); // 派发资金变化事件
        }
    }

    /// <summary>
    /// 增加良心并派发变化事件。
    /// </summary>
    /// <param name="amount">增加数量（必须大于 0）。</param>
    public void AddConscience(int amount) // 良心增加入口
    {
        if (_battleData == null)
        {
            return; // 未加载战斗数据时直接返回
        }

        if (amount <= 0)
        {
            return; // 无效增量时直接返回
        }

        var previous = _conscienceCurrent; // 缓存旧良心值
        _conscienceCurrent += amount; // 累加良心
        if (_conscienceCurrent != previous)
        {
            PostConscienceChanged(previous, _conscienceCurrent); // 派发良心变化事件
        }
    }

    /// <summary>
    /// 增加黑心并派发变化事件。
    /// </summary>
    /// <param name="amount">增加数量（必须大于 0）。</param>
    public void AddBlackHeart(int amount) // 黑心增加入口
    {
        if (_battleData == null)
        {
            return; // 未加载战斗数据时直接返回
        }

        if (amount <= 0)
        {
            return; // 无效增量时直接返回
        }

        var previous = _blackHeartCurrent; // 缓存旧黑心值
        _blackHeartCurrent += amount; // 累加黑心
        if (_blackHeartCurrent != previous)
        {
            PostBlackHeartChanged(previous, _blackHeartCurrent); // 派发黑心变化事件
        }

        EnsureBlackHeartConvertTimerState(); // 刷新黑心自动转换计时器状态
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
    /// 重置资金运行时数据（在数据表加载完成后调用）。
    /// </summary>
    private void ResetMoneyRuntimeState() // 资金运行时重置入口
    {
        var initialMoney = _battleData != null ? _battleData.Money : 0; // 读取初始资金
        _moneyCurrent = Mathf.Max(0, initialMoney); // 重置资金当前值
    }

    /// <summary>
    /// 重置良心运行时数据（在数据表加载完成后调用）。
    /// </summary>
    private void ResetConscienceRuntimeState() // 良心运行时重置入口
    {
        var initialConscience = _battleData != null ? _battleData.Conscience : 0; // 读取初始良心
        _conscienceCurrent = Mathf.Max(0, initialConscience); // 重置良心当前值
    }

    /// <summary>
    /// 重置黑心运行时数据（在数据表加载完成后调用）。
    /// </summary>
    private void ResetBlackHeartRuntimeState() // 黑心运行时重置入口
    {
        var initialBlackHeart = _battleData != null ? _battleData.BlackHeart : 0; // 读取初始黑心
        _blackHeartCurrent = Mathf.Max(0, initialBlackHeart); // 重置黑心当前值
        EnsureBlackHeartConvertTimerState(); // 刷新黑心自动转换计时器状态
    }

    /// <summary>
    /// 获取黑心转换时间（秒，<=0 表示禁用自动转换）。
    /// </summary>
    private float GetBlackHeartConvertTime() // 黑心转换时间读取入口
    {
        var value = _battleData != null ? _battleData.BlackHeartConvertTime : 0f; // 读取配置值
        return value > 0f ? value : 0f; // 返回有效值
    }

    /// <summary>
    /// 获取黑心并发吸收槽位数量（<=0 时使用 1）。
    /// </summary>
    private int GetBlackHeartAbsorbCount() // 黑心吸收槽位读取入口
    {
        var value = _battleData != null ? _battleData.BlackHeartAbsorbCount : 0; // 读取配置值
        return value > 0 ? value : 1; // 返回有效值
    }

    /// <summary>
    /// 刷新黑心自动转换计时器状态（有黑心且配置有效时启动，否则停止）。
    /// </summary>
    private void EnsureBlackHeartConvertTimerState() // 黑心自动转换计时器刷新入口
    {
        var convertTime = GetBlackHeartConvertTime(); // 读取转换时间
        if (convertTime <= 0f)
        {
            StopBlackHeartConvertTimer(); // 配置无效时停止计时器
            return; // 直接退出
        }

        if (_blackHeartCurrent <= 0)
        {
            StopBlackHeartConvertTimer(); // 无黑心时停止计时器
            return; // 直接退出
        }

        if (_blackHeartConvertTimer != null)
        {
            return; // 计时器已在运行时直接返回
        }

        _blackHeartConvertTimer = CY.Timer.Loop(convertTime, ConvertOneBlackHeartToConscience); // 启动黑心自动转换循环计时器
    }

    /// <summary>
    /// 停止黑心自动转换计时器。
    /// </summary>
    private void StopBlackHeartConvertTimer() // 黑心自动转换计时器停止入口
    {
        if (_blackHeartConvertTimer == null)
        {
            return; // 计时器为空时直接退出
        }

        _blackHeartConvertTimer.Stop(); // 停止计时器
        _blackHeartConvertTimer = null; // 清空计时器引用
    }

    /// <summary>
    /// 将 1 点黑心自动转换为 1 点良心（按配置周期触发）。
    /// </summary>
    private void ConvertOneBlackHeartToConscience() // 黑心自动转换入口
    {
        if (_battleData == null)
        {
            StopBlackHeartConvertTimer(); // 数据未加载时停止计时器，避免空引用
            return; // 直接退出
        }

        if (_blackHeartCurrent <= 0)
        {
            StopBlackHeartConvertTimer(); // 无黑心时停止计时器，避免空转
            return; // 直接退出
        }

        var previousBlackHeart = _blackHeartCurrent; // 缓存转换前黑心
        _blackHeartCurrent = Mathf.Max(0, _blackHeartCurrent - 1); // 扣除 1 点黑心并做下限保护
        if (_blackHeartCurrent != previousBlackHeart)
        {
            PostBlackHeartChanged(previousBlackHeart, _blackHeartCurrent); // 派发黑心变化事件
        }

        AddConscience(1); // 增加 1 点良心并派发事件
        EnsureBlackHeartConvertTimerState(); // 刷新计时器状态（黑心归零时停止）
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
    /// 派发资金变化事件。
    /// </summary>
    /// <param name="previous">变化前资金。</param>
    /// <param name="current">变化后资金。</param>
    private void PostMoneyChanged(int previous, int current) // 资金事件派发入口
    {
        var evt = new MoneyChangedEvent // 创建事件结构体
        {
            CurrentValue = current, // 写入当前资金
            Delta = current - previous // 写入变化量
        };
        CY.Event.Post(ref evt); // 派发事件
    }

    /// <summary>
    /// 派发良心变化事件。
    /// </summary>
    /// <param name="previous">变化前良心。</param>
    /// <param name="current">变化后良心。</param>
    private void PostConscienceChanged(int previous, int current) // 良心事件派发入口
    {
        var evt = new ConscienceChangedEvent // 创建事件结构体
        {
            CurrentValue = current, // 写入当前良心
            Delta = current - previous // 写入变化量
        };
        CY.Event.Post(ref evt); // 派发事件
    }

    /// <summary>
    /// 派发黑心变化事件。
    /// </summary>
    /// <param name="previous">变化前黑心。</param>
    /// <param name="current">变化后黑心。</param>
    private void PostBlackHeartChanged(int previous, int current) // 黑心事件派发入口
    {
        var evt = new BlackHeartChangedEvent // 创建事件结构体
        {
            CurrentValue = current, // 写入当前黑心
            Delta = current - previous // 写入变化量
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
        StopBlackHeartConvertTimer(); // 销毁时停止黑心自动转换计时器，避免回调访问已销毁对象
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
        ResetMoneyRuntimeState(); // 重置资金运行时状态
        ResetConscienceRuntimeState(); // 重置良心运行时状态
        ResetBlackHeartRuntimeState(); // 重置黑心运行时状态

        if (_pendingResetForNewGame) // 存在“新游戏重置”延迟请求时执行
        {
            var shouldPostEvents = _pendingResetPostEvents; // 缓存是否需要派发事件
            _pendingResetForNewGame = false; // 清除延迟重置标记
            _pendingResetPostEvents = false; // 清除派发标记
            ResetRuntimeForNewGame(shouldPostEvents); // 在数据加载完成后执行一次新游戏重置
        }

        return true;
    }
}
