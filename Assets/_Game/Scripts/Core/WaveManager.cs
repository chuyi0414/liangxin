using CYFramework;
using CYFramework.Core; // Added for EntityGroup
using CYFramework.Infrastructure;
using UnityEngine;
using System.Collections.Generic;
using CYFramework.Core.Entity;

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
        _spawnedCount = 0;
        _waveEnemyIds = new List<string>();
        
        // 1. 解析怪物池 (EnemyPool: "101|102" or "101*10|102*5")
        // 这里先做最简单的：假设 EnemyPool 填的是 "1001" (数量由 Budget 控制)
        // TODO: 完整的 Budget 换算怪物数量逻辑
        
        // 临时：硬编码生成 10 只怪用于测试，ID 读 EnemyPool，默认 10001
        string enemyId = "10001";
        if (!string.IsNullOrEmpty(CurrentTemplate.EnemyPool))
        {
            enemyId = CurrentTemplate.EnemyPool.Split('|')[0]; // 暂时只取第一个
        }
        
        // 数量 = 基础10 * 预算倍率
        int count = Mathf.CeilToInt(10 * CurrentTemplate.BudgetMultiplier);
        _totalMonstersInWave = count;

        for (int i = 0; i < count; i++)
        {
            _waveEnemyIds.Add(enemyId);
        }

        // 2. 初始化策略参数
        _currentStrategy = CurrentTemplate.SpawnPointStrategy;
        _currentRhythm = CurrentTemplate.SpawnRhythm;
        
        // 预计算策略数据 (例如 Single 策略需要在波次开始时就定好这波走哪个门)
        if (_currentStrategy == "Single")
        {
            _strategyFixedPointIndex = UnityEngine.Random.Range(0, SpawnPoints.Count);
        }
        else
        {
            _strategyFixedPointIndex = -1;
        }

        // 3. 初始化第一只怪的时间
        _nextSpawnTime = 0; // 立即开始
        UpdateNextSpawnTime(); // 计算第一只怪之后的冷却，还是第一只就在0秒？ 
                               // 逻辑上：0秒刷第1只，然后冷却。所以 UpdateNextSpawnTime 在 Spawn 后调用更合理。
                               // 但为了 Update 循环简洁，我们在 Spawn 后调用。这里只需重置为 0。
        _nextSpawnTime = 0;
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
