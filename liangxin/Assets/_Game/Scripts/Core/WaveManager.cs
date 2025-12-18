using CYFramework;
using CYFramework.Core; // Added for EntityGroup
using CYFramework.Infrastructure;
using UnityEngine;
using System.Collections.Generic;
using CYFramework.Core.Entity;
using CYFramework.Core.DataTable;

/// <summary>
/// 波数管理器
/// 负责：波次控制、怪物刷新、胜负判定辅助
/// </summary>
public class WaveManager : MonoBehaviour, IInitializable, IUpdateable, IDisposableEx
{
    // ═══════════ 配置 ═══════════
    [Header("场景配置")]
    [Tooltip("怪物出生点列表 (建议按8方向顺时针: 北, 东北, 东, 东南, 南, 西南, 西, 西北)")]
    public List<Transform> SpawnPoints;

    [Header("调试配置")]
    /// <summary>
    /// 是否在启动时自动开始第一波（调试用）
    /// </summary>
    public bool AutoStartWave = true;

    // ═══════════ 运行时数据 ═══════════
    public enum WaveState
    {
        None,
        Preparing, // 准备阶段 (波次间隙)
        Fighting   // 战斗阶段 (刷怪中)
    }

    /// <summary>
    /// 当前波次状态
    /// </summary>
    public WaveState State { get; private set; } = WaveState.None;

    /// <summary>
    /// 当前波次索引 (从1开始，0表示未开始)
    /// </summary>
    public int CurrentWaveIndex { get; private set; } = 0;

    /// <summary>
    /// 当前阶段的剩余时间 (秒)
    /// </summary>
    public float RemainingTime { get; private set; }

    /// <summary>
    /// 对外接口：是否处于战斗波次中
    /// </summary>
    public bool IsWaveActive => State == WaveState.Fighting;

    /// <summary>
    /// 波次计时器 (累计时间，仅供内部或统计使用)
    /// </summary>
    public float WaveTimer { get; private set; } // 兼容旧代码，保留但主要逻辑走 RemainingTime

    /// <summary>
    /// 当前使用中的波次模板
    /// </summary>
    public WaveTemplateRow CurrentTemplate { get; private set; }

    /// <summary>
    /// 缓存的敌人数据表引用（CY.Data 内部已缓存，这里显式持有可避免每波重复查表并提升可读性）
    /// </summary>
    private DataTable<EnemyRow> _enemyTable;

    // ═══════════ 框架生命周期 ═══════════
    /// <summary>
    /// 初始化顺序：110
    /// </summary>
    public int InitOrder => 110;
    public int UpdateOrder => 0;
    public int DisposeOrder => 110;

    /// <summary>
    /// 框架初始化
    /// </summary>
    public void Initialize()
    {
        CY.Log("[WaveManager] Initialize");

        ResetBattle();

        // 缓存敌人表（若初始化时未加载，将在 PrepareWaveSpawns 再兜底获取）
        if (CY.Data.HasDataTable("Enemy"))
        {
            _enemyTable = CY.Data.GetDataTable<EnemyRow>("Enemy");
        }

        // 订阅游戏结束事件：游戏失败/退出时立即停止刷怪，避免继续生成敌人
        // 防止重复订阅
        CY.Event.Unsubscribe<OverGameEvent>(OnGameOver);
        CY.Event.Subscribe<OverGameEvent>(OnGameOver, this);

        if (AutoStartWave)
        {
            StartBattle();
        }
    }

    /// <summary>
    /// 框架每帧更新
    /// </summary>
    public void OnUpdate(float deltaTime)
    {
        if (State == WaveState.None) return;

        // 更新剩余时间
        if (RemainingTime > 0)
        {
            RemainingTime -= deltaTime;
            if (RemainingTime <= 0)
            {
                RemainingTime = 0;
                OnStateTimerComplete();
            }
        }

        // 兼容旧的累计计时器
        if (State == WaveState.Fighting)
        {
            WaveTimer += deltaTime;
            CheckSpawns(WaveTimer);
        }
    }

    /// <summary>
    /// 框架销毁清理
    /// </summary>
    public void Dispose()
    {
        CY.Log("[WaveManager] Dispose");
        State = WaveState.None;
        _enemyTable = null;
        // 显式反订阅，防止对象销毁后仍被事件总线持有引用
        CY.Event.UnsubscribeAll(this);
    }

    // ═══════════ Unity 桥接 ═══════════
    private void Awake()
    {
        // 自动注册到服务定位器
        if (!ServiceLocator.IsRegistered<WaveManager>())
        {
            ServiceLocator.RegisterInstance(this);
        }
        else
        {
            // 防止重复挂载
            Destroy(gameObject);
            Initialize(); 
        }
    }

    private void OnDestroy()
    {
        Dispose();
        if (ServiceLocator.IsRegistered<WaveManager>())
        {
            ServiceLocator.Unregister<WaveManager>();
        }
    }

    // ═══════════ 业务逻辑 ═══════════

    public void ResetBattle()
    {
        CurrentWaveIndex = 0;
        State = WaveState.None;
        WaveTimer = 0;
        RemainingTime = 0;
        CurrentTemplate = null;
        _enemyTable = CY.Data.HasDataTable("Enemy") ? CY.Data.GetDataTable<EnemyRow>("Enemy") : null;
        ClearWaveRuntimeState();
    }

    /// <summary>
    /// 开始战斗流程 (进入首波准备)
    /// </summary>
    public void StartBattle()
    {
        CY.Log("[WaveManager] StartBattle - Begin Flow");
        
        float prepareTime;
        
        if (DifficultyConfig.FirstWavePrepareTime >= 0)
        {
            prepareTime = DifficultyConfig.FirstWavePrepareTime;
        }
        else
        {
            prepareTime = DifficultyConfig.NormalPrepareTime;
        }

        EnterPreparation(prepareTime);
    }

    /// <summary>
    /// 计时器结束回调
    /// </summary>
    private void OnStateTimerComplete()
    {
        switch (State)
        {
            case WaveState.Preparing:
                // 准备结束 -> 开始下一波战斗
                StartNextWave();
                break;
            case WaveState.Fighting:
                // 战斗时间结束 -> 停止本波 -> 进入下一波准备
                FinishCurrentWave();
                break;
        }
    }

    /// <summary>
    /// 进入准备阶段
    /// </summary>
    private void EnterPreparation(float duration)
    {
        State = WaveState.Preparing;
        RemainingTime = duration;
        CY.Log($"[WaveManager] 进入准备阶段，时长: {duration}s");
    }

    /// <summary>
    /// 开始下一波 (进入战斗阶段)
    /// </summary>
    public void StartNextWave()
    {
        CurrentWaveIndex++;
        
        // 1. 选择模板
        CurrentTemplate = SelectWaveTemplate(CurrentWaveIndex);
        if (CurrentTemplate == null)
        {
            CY.LogError($"[WaveManager] 第 {CurrentWaveIndex} 波找不到合适的模板！使用默认配置。");
            EnterFighting(DifficultyConfig.BaseDuration); 
            return;
        }

        // 2. 计算战斗时长
        float duration = DifficultyConfig.BaseDuration;
        if (CurrentTemplate.DurationMultiplier > 0)
        {
            duration *= CurrentTemplate.DurationMultiplier;
        }

        EnterFighting(duration);
        
        CY.Log($"[WaveManager] 第 {CurrentWaveIndex} 波战斗开始！(模板: {CurrentTemplate.Name}, 时长: {duration}s)");
    }

    private void EnterFighting(float duration)
    {
        State = WaveState.Fighting;
        RemainingTime = duration;
        WaveTimer = 0;
        
        PrepareWaveSpawns(); // 核心：进入战斗时，准备刷怪队列
        CY.Log($"[WaveManager] 进入战斗阶段，时长: {duration}s, 预计刷怪: {_totalMonstersInWave}");
    }

    /// <summary>
    /// 结束当前波次 (进入下一次准备)
    /// </summary>
    public void FinishCurrentWave()
    {
        CY.Log($"[WaveManager] 第 {CurrentWaveIndex} 波结束！");
        EnterPreparation(DifficultyConfig.NormalPrepareTime);
    }
    
    /// <summary>
    /// 强制停止 (游戏结束)
    /// </summary>
    public void StopWave()
    {
        State = WaveState.None;
        RemainingTime = 0f;
        WaveTimer = 0f;
        CurrentTemplate = null;
        ClearWaveRuntimeState();
    }

    // ═══════════ 刷怪核心逻辑 ═══════════

    // 运行时刷怪状态
    private float _nextSpawnTime;
    private int _totalMonstersInWave;       // 本波总怪数
    private int _spawnedCount;              // 已生成数量
    private List<string> _waveEnemyIds;     // 本波待刷怪物ID队列 (简单实现：ID列表)
    
    // 缓存当前波次的策略 (避免每帧解析)
    private string _currentStrategy;
    private string _currentRhythm;
    
    // 缓存策略用的临时变量
    private int _strategyFixedPointIndex = -1; // 用于 Single/Cross 等固定点策略

    /// <summary>
    /// 每帧检测刷怪
    /// </summary>
    private void CheckSpawns(float waveTime)
    {
        // 如果怪刷完了，就不跑了
        if (_spawnedCount >= _totalMonstersInWave) return;

        // Rhythm: 决定 "When"
        if (waveTime >= _nextSpawnTime)
        {
            SpawnNextMonster();
            UpdateNextSpawnTime();
        }
    }
    
    private void PrepareWaveSpawns()
    {
        // ① 初始化内存状态
        _spawnedCount = 0;
        if (_waveEnemyIds == null) _waveEnemyIds = new List<string>();
        else _waveEnemyIds.Clear();

        // ② 计算预算：BaseBudget * WaveGrowth^(wave-1) * Template倍率
        float budget = DifficultyConfig.BaseBudget;
        int waveForBudget = Mathf.Max(0, CurrentWaveIndex - 1);
        if (waveForBudget > 0)
        {
            budget *= Mathf.Pow(DifficultyConfig.WaveGrowth, waveForBudget);
        }
        float templateBudgetMultiplier = (CurrentTemplate != null && CurrentTemplate.BudgetMultiplier > 0f)
            ? CurrentTemplate.BudgetMultiplier
            : 1f;
        budget *= templateBudgetMultiplier;

        // ③ 获取候选敌人（优先使用缓存的表引用）
        var enemyTable = _enemyTable ?? CY.Data.GetDataTable<EnemyRow>("Enemy");
        if (enemyTable == null)
        {
            CY.LogError("[WaveManager] Enemy 数据表未加载，无法准备波次阵容！");
            _totalMonstersInWave = 0;
            return;
        }
        if (_enemyTable == null) _enemyTable = enemyTable; // 首次获取时缓存

        var allEnemies = enemyTable.GetAllRows();
        List<EnemyRow> candidates = new List<EnemyRow>();

        // 模板限定：EnemyPool 存在则只允许池内 ID
        List<int> allowedIds = null;
        if (CurrentTemplate != null && !string.IsNullOrEmpty(CurrentTemplate.EnemyPool))
        {
            allowedIds = new List<int>();
            string[] parts = CurrentTemplate.EnemyPool.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                int parsedId;
                if (int.TryParse(parts[i], out parsedId))
                {
                    allowedIds.Add(parsedId);
                }
            }
        }

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyRow row = allEnemies[i];
            if (row == null) continue;
            if (row.MinWave > CurrentWaveIndex) continue;

            if (allowedIds != null && allowedIds.Count > 0)
            {
                bool match = false;
                for (int k = 0; k < allowedIds.Count; k++)
                {
                    if (row.Id == allowedIds[k])
                    {
                        match = true;
                        break;
                    }
                }
                if (!match) continue;
            }
            candidates.Add(row);
        }

        // 兜底：配置错误时退回符合波次的所有敌人
        if (candidates.Count == 0)
        {
            for (int i = 0; i < allEnemies.Count; i++)
            {
                EnemyRow row = allEnemies[i];
                if (row != null && row.MinWave <= CurrentWaveIndex)
                {
                    candidates.Add(row);
                }
            }
        }

        if (candidates.Count == 0)
        {
            CY.LogError($"[WaveManager] 第{CurrentWaveIndex}波无可用敌人，不刷怪。");
            _totalMonstersInWave = 0;
            return;
        }

        // 预计算权重与最小Cost
        int totalWeight = 0;
        int minCost = int.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            EnemyRow row = candidates[i];
            int weight = row.Weight > 0 ? row.Weight : 1;
            totalWeight += weight;

            if (row.Cost > 0 && row.Cost < minCost)
            {
                minCost = row.Cost;
            }
        }
        if (minCost <= 0) minCost = 1;

        // ④ 按预算组装刷怪队列
        float budgetLeft = budget;
        int maxCount = DifficultyConfig.MaxEnemyCount > 0 ? DifficultyConfig.MaxEnemyCount : 200;
        int safetyCounter = 0;

        while (budgetLeft >= minCost && _waveEnemyIds.Count < maxCount && safetyCounter < 5000)
        {
            EnemyRow chosen = PickEnemyByWeight(candidates, totalWeight);
            if (chosen == null) break;

            int cost = chosen.Cost > 0 ? chosen.Cost : minCost;
            if (budgetLeft < cost)
            {
                if (_waveEnemyIds.Count == 0)
                {
                    _waveEnemyIds.Add(chosen.Id.ToString());
                }
                break;
            }

            _waveEnemyIds.Add(chosen.Id.ToString());
            budgetLeft -= cost;
            safetyCounter++;
        }

        if (_waveEnemyIds.Count == 0)
        {
            _waveEnemyIds.Add(candidates[0].Id.ToString());
        }

        _totalMonstersInWave = _waveEnemyIds.Count;

        // ⑤ 策略/节奏初始化
        _currentStrategy = (CurrentTemplate != null && !string.IsNullOrEmpty(CurrentTemplate.SpawnPointStrategy))
            ? CurrentTemplate.SpawnPointStrategy
            : "Single";
        _currentRhythm = (CurrentTemplate != null && !string.IsNullOrEmpty(CurrentTemplate.SpawnRhythm))
            ? CurrentTemplate.SpawnRhythm
            : "Linear";

        if (_currentStrategy == "Single")
        {
            _strategyFixedPointIndex = (SpawnPoints != null && SpawnPoints.Count > 0)
                ? UnityEngine.Random.Range(0, SpawnPoints.Count)
                : -1;
        }
        else
        {
            _strategyFixedPointIndex = -1;
        }

        // ⑥ 初始化第一只怪的时间
        _nextSpawnTime = 0f;
        UpdateNextSpawnTime();
    }

    private EnemyRow PickEnemyByWeight(List<EnemyRow> pool, int totalWeight)
    {
        if (pool == null || pool.Count == 0) return null;
        if (totalWeight <= 0)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            return pool[idx];
        }

        int rnd = UnityEngine.Random.Range(0, totalWeight);
        int current = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            EnemyRow row = pool[i];
            int weight = row.Weight > 0 ? row.Weight : 1;
            current += weight;
            if (rnd < current)
            {
                return row;
            }
        }

        return pool[pool.Count - 1];
    }

    /// <summary>
    /// 游戏失败/退出时的清理：清空刷怪状态，避免残留计时与队列
    /// </summary>
    /// <param name="evt"></param>
    private void OnGameOver(ref OverGameEvent evt)
    {
        CY.Log("[WaveManager] 接收到游戏结束事件，停止刷怪");
        StopWave();
    }

    /// <summary>
    /// 清理波次运行时状态，防止重开时使用旧的刷怪队列/计时
    /// </summary>
    private void ClearWaveRuntimeState()
    {
        _nextSpawnTime = 0f;
        _totalMonstersInWave = 0;
        _spawnedCount = 0;
        _currentStrategy = null;
        _currentRhythm = null;
        _strategyFixedPointIndex = -1;
        if (_waveEnemyIds != null)
        {
            _waveEnemyIds.Clear();
        }
    }

    private void SpawnNextMonster()
    {
        if (_spawnedCount >= _waveEnemyIds.Count) return;

        string enemyIdStr = _waveEnemyIds[_spawnedCount];
        int enemyId = int.Parse(enemyIdStr); // 需确保配置表填的是 Int ID

        // Strategy: 决定 "Where"
        Transform point = GetSpawnPoint(_spawnedCount, _totalMonstersInWave);
        
        // 执行生成
        SpawnMonsterEntity(enemyId, point.position);

        _spawnedCount++;
    }

    /// <summary>
    /// 核心：出生点选择器
    /// </summary>
    private Transform GetSpawnPoint(int index, int total)
    {
        if (SpawnPoints == null || SpawnPoints.Count == 0) return transform; // 兜底

        // 用 Switch 派发 20+ 种策略
        // 目前只实现 Single，其余 TODO
        switch (_currentStrategy)
        {
            case "Single":
                // 整个波次固定走一个门 (在 Prepare 时随机好了)
                if (_strategyFixedPointIndex < 0 || _strategyFixedPointIndex >= SpawnPoints.Count)
                    _strategyFixedPointIndex = 0;
                return SpawnPoints[_strategyFixedPointIndex];

            case "All":
                // 简单的轮询：让每个怪依次走不同的门，实现分散
                return SpawnPoints[index % SpawnPoints.Count];
            
            case "Clockwise":
                // 顺时针旋转：随着波次进度，出怪点顺时针移动
                // 比如总共 8 个点，前 1/8 的怪走北，接下来的 1/8 走东北...
                if (total <= 0) return SpawnPoints[0];
                float progress = (float)index / total; 
                // 映射到 [0, Count-1]
                int directionIndex = Mathf.FloorToInt(progress * SpawnPoints.Count) % SpawnPoints.Count;
                return SpawnPoints[directionIndex];

            // ... 更多策略 TODO (Pincer, Cross, Hunter, Kite...)

            default:
                // 默认策略 = Single (或者 Random)
                return SpawnPoints[0];
        }
    }

    private void UpdateNextSpawnTime()
    {
        // Rhythm: 计算下一只怪的间隔
        // 目前只实现 Linear (匀速)
        float totalDuration = RemainingTime; // 剩余时间不准，应该用 TotalDuration
        // 简单起见，假设波次总长 30s
        float waveDuration = 30f * (CurrentTemplate.DurationMultiplier > 0 ? CurrentTemplate.DurationMultiplier : 1f);
        
        switch (_currentRhythm)
        {
            case "Linear":
                // 间隔 = 总时长 / 总怪数
                float interval = waveDuration / Mathf.Max(1, _totalMonstersInWave);
                _nextSpawnTime = WaveTimer + interval;
                break;
                
            case "Burst":
                // TODO: 爆发
                 _nextSpawnTime = WaveTimer + 0.1f;
                 break;

            default: 
                // 默认 Linear
                _nextSpawnTime = WaveTimer + (waveDuration / Mathf.Max(1, _totalMonstersInWave));
                break;
        }
    }

    private void SpawnMonsterEntity(int enemyId, Vector3 pos)
    {
        // 调用数据转换
        var table = CY.Data.GetDataTable<EnemyRow>("Enemy");
        var row = table?.GetRow(enemyId);
        
        if (row != null)
        {
            // 随机一点偏移，防止重叠
            Vector3 randomOffset = UnityEngine.Random.insideUnitCircle * 1.0f;
            
            // 使用 SpawnEntity 生成
            // 注意：EntityType 最好也由 DataRow 提供，这里暂时硬编码前缀或由 Row 提供
            string entityType = $"Enemy_{row.Id}"; // 简单区分
            
            var enemy = CY.Entity.SpawnEntity<EnemyEntity>(entityType, row.PrefabPath, EntityGroup.Enemies, row);
            if (enemy != null)
            {
                enemy.transform.position = pos + randomOffset;
            }
        }
        else
        {
            CY.LogError($"[WaveManager] 无法生成怪物 ID {enemyId}，配置不存在");
        }
    }

    // ═══════════ 模板选择逻辑 ═══════════
    // ... (保持原有的 SelectWaveTemplate 不变)
    private WaveTemplateRow SelectWaveTemplate(int waveIndex)
    {
        var table = CY.Data.GetDataTable<WaveTemplateRow>("WaveTemplate");
        if (table == null) return null;

        var allRows = table.GetAllRows();
        List<WaveTemplateRow> candidates = new List<WaveTemplateRow>();

        // 1. 优先查固定周期 (Period > 0 且整除)
        foreach (var row in allRows)
        {
            if (row.Period > 0 && waveIndex % row.Period == 0)
            {
                return row;
            }
        }

        // 2. 查随机池 (Period == 0 且在 Min/Max 范围内)
        foreach (var row in allRows)
        {
            if (row.Period == 0 && waveIndex >= row.MinWave && (row.MaxWave == 9999 || waveIndex <= row.MaxWave))
            {
                candidates.Add(row);
            }
        }

        if (candidates.Count == 0) return null;

        // 3. 权重随机
        int totalWeight = 0;
        foreach (var c in candidates) totalWeight += c.RandomWeight;

        int rnd = UnityEngine.Random.Range(0, totalWeight);
        int current = 0;
        foreach (var c in candidates)
        {
            current += c.RandomWeight;
            if (rnd < current) return c;
        }

        return candidates[candidates.Count - 1];
    }
}
