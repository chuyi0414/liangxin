// 引用泛型集合命名空间，使用 List
using System.Collections.Generic; // 集合类型引用

public sealed partial class WaveManager // 波次管理器分部定义
{
    /// <summary>
    /// 权重条目（Id + 权重）。
    /// </summary>
    private struct WeightedId // 权重条目结构体
    {
        /// <summary>条目 Id。</summary>
        public int Id; // 条目 Id
        /// <summary>条目权重。</summary>
        public int Weight; // 条目权重

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="id">条目 Id。</param>
        /// <param name="weight">权重值。</param>
        public WeightedId(int id, int weight) // 构造函数
        {
            Id = id; // 记录 Id
            Weight = weight; // 记录权重
        }
    }

    /// <summary>
    /// 刷怪组敌人池。
    /// </summary>
    private sealed class SpawnGroupEnemyPool // 刷怪组敌人池定义
    {
        /// <summary>总权重。</summary>
        public int TotalWeight; // 权重总和
        /// <summary>敌人条目列表。</summary>
        public readonly List<WeightedId> Entries; // 敌人条目列表

        /// <summary>
        /// 构造函数。
        /// </summary>
        public SpawnGroupEnemyPool() // 构造函数
        {
            TotalWeight = 0; // 初始化权重
            Entries = new List<WeightedId>(8); // 初始化列表
        }
    }

    /// <summary>
    /// 刷怪组运行时数据。
    /// </summary>
    private sealed class SpawnGroupRuntime // 刷怪组运行时定义
    {
        /// <summary>刷怪组配置。</summary>
        public WaveSpawnGroupRow Row; // 刷怪组配置
        /// <summary>敌人权重池。</summary>
        public SpawnGroupEnemyPool EnemyPool; // 敌人池
        /// <summary>刷新点 Id 列表。</summary>
        public readonly List<string> PointIds; // 刷新点列表

        /// <summary>
        /// 构造函数。
        /// </summary>
        public SpawnGroupRuntime() // 构造函数
        {
            Row = null; // 初始化配置
            EnemyPool = null; // 初始化敌人池
            PointIds = new List<string>(8); // 初始化刷新点列表
        }
    }

    /// <summary>
    /// 轨道运行时数据。
    /// </summary>
    private sealed class TrackRuntime // 轨道运行时定义
    {
        /// <summary>轨道配置。</summary>
        public WaveTrackRow Row; // 轨道配置
        /// <summary>刷怪组运行时数据。</summary>
        public SpawnGroupRuntime SpawnGroup; // 刷怪组运行时
        /// <summary>是否已满足开始条件。</summary>
        public bool StartConditionMet; // 开始条件标记
        /// <summary>是否已开始。</summary>
        public bool IsStarted; // 开始标记
        /// <summary>是否已结束。</summary>
        public bool IsFinished; // 结束标记
        /// <summary>开始延迟剩余时间（秒）。</summary>
        public float StartDelayRemaining; // 开始延迟剩余
        /// <summary>轨道已运行时间（秒）。</summary>
        public float ElapsedTime; // 轨道时间
        /// <summary>下一次刷新倒计时（秒）。</summary>
        public float NextSpawnTimer; // 刷新计时
        /// <summary>累计刷怪数量。</summary>
        public int SpawnedCount; // 刷怪数量
        /// <summary>当前存活数量。</summary>
        public int AliveCount; // 存活数量

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="row">轨道配置。</param>
        /// <param name="spawnGroup">刷怪组运行时。</param>
        public TrackRuntime(WaveTrackRow row, SpawnGroupRuntime spawnGroup) // 构造函数
        {
            Row = row; // 记录轨道配置
            SpawnGroup = spawnGroup; // 记录刷怪组
            StartConditionMet = false; // 初始化开始条件标记
            IsStarted = false; // 初始化开始标记
            IsFinished = false; // 初始化结束标记
            StartDelayRemaining = 0f; // 初始化开始延迟
            ElapsedTime = 0f; // 初始化轨道时间
            NextSpawnTimer = 0f; // 初始化刷新计时
            SpawnedCount = 0; // 初始化刷怪数量
            AliveCount = 0; // 初始化存活数量
        }
    }

    /// <summary>
    /// 波次运行时数据。
    /// </summary>
    private sealed class WaveRuntime // 波次运行时定义
    {
        /// <summary>波次配置。</summary>
        public WavePlanRow Plan; // 波次配置
        /// <summary>轨道运行时列表。</summary>
        public readonly List<TrackRuntime> Tracks; // 轨道列表
        /// <summary>是否已进入刷怪阶段。</summary>
        public bool HasSpawnStarted; // 刷怪阶段标记
        /// <summary>波次已运行时间（秒）。</summary>
        public float ElapsedTime; // 波次时间
        /// <summary>累计刷怪数量。</summary>
        public int TotalSpawned; // 总刷怪数量
        /// <summary>累计击杀数量。</summary>
        public int TotalKilled; // 总击杀数量
        /// <summary>当前存活数量。</summary>
        public int EnemyAliveCount; // 存活数量

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="plan">波次配置。</param>
        public WaveRuntime(WavePlanRow plan) // 构造函数
        {
            Plan = plan; // 记录波次配置
            Tracks = new List<TrackRuntime>(8); // 初始化轨道列表
            HasSpawnStarted = false; // 初始化刷怪标记
            ElapsedTime = 0f; // 初始化波次时间
            TotalSpawned = 0; // 初始化刷怪计数
            TotalKilled = 0; // 初始化击杀计数
            EnemyAliveCount = 0; // 初始化存活计数
        }
    }
}
