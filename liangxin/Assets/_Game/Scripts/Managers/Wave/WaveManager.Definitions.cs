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
    /// 波次运行时数据。
    /// </summary>
    private sealed class WaveRuntime // 波次运行时
    {
        /// <summary>波次 Id。</summary>
        public int WaveId; // 波次 Id
        /// <summary>是否为奇袭波次。</summary>
        public bool IsAssault; // 奇袭标记
        /// <summary>下一次刷新倒计时（秒）。</summary>
        public float NextRefreshTimer; // 刷新计时
        /// <summary>运行时生成类型池。</summary>
        public readonly List<WeightedId> SpawnTypes; // 运行时生成池
        /// <summary>运行时生成类型权重总和。</summary>
        public int SpawnTypeWeightTotal; // 权重总和
        /// <summary>生成类型运行时列表。</summary>
        public readonly List<SpawnTypeRuntime> SpawnTypeRuntimes; // 生成类型运行时
        /// <summary>是否已进入刷怪阶段。</summary>
        public bool HasSpawnStarted; // 刷怪阶段标记

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="waveId">波次 Id。</param>
        /// <param name="isAssault">是否为奇袭波次。</param>
        public WaveRuntime(int waveId, bool isAssault) // 构造函数
        {
            WaveId = waveId; // 记录波次 Id
            IsAssault = isAssault; // 记录奇袭标记
            NextRefreshTimer = 0f; // 初始化刷新计时
            SpawnTypes = new List<WeightedId>(8); // 初始化生成池
            SpawnTypeWeightTotal = 0; // 初始化权重
            SpawnTypeRuntimes = new List<SpawnTypeRuntime>(8); // 初始化运行时列表
            HasSpawnStarted = false; // 初始化刷怪标记
        }
    }

    /// <summary>
    /// 生成类型内的敌人权重池。
    /// </summary>
    private sealed class SpawnTypeEnemyPool // 生成类型敌人池
    {
        /// <summary>总权重。</summary>
        public int TotalWeight; // 权重总和
        /// <summary>敌人条目列表。</summary>
        public readonly List<WeightedId> Entries; // 敌人条目列表

        /// <summary>
        /// 构造函数。
        /// </summary>
        public SpawnTypeEnemyPool() // 构造函数
        {
            TotalWeight = 0; // 初始化权重
            Entries = new List<WeightedId>(8); // 初始化列表
        }
    }

    /// <summary>
    /// 生成类型刷新点池。
    /// </summary>
    private sealed class SpawnTypePointPool // 刷新点池
    {
        /// <summary>刷新点 Id 列表。</summary>
        public readonly List<string> PointIds = new List<string>(8); // 刷新点列表
    }

    /// <summary>
    /// 生成类型运行时数据。
    /// </summary>
    private sealed class SpawnTypeRuntime // 生成类型运行时
    {
        /// <summary>配置 Id。</summary>
        public int ConfigId; // 配置 Id
        /// <summary>准备阶段剩余时间（秒）。</summary>
        public float PrepareRemaining; // 准备剩余
        /// <summary>刷怪阶段剩余时间（秒）。</summary>
        public float SpawnRemaining; // 刷怪剩余

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="configId">生成类型 Id。</param>
        /// <param name="prepareDuration">准备时长。</param>
        /// <param name="spawnDuration">刷怪时长。</param>
        public SpawnTypeRuntime(int configId, float prepareDuration, float spawnDuration) // 构造函数
        {
            ConfigId = configId; // 记录配置 Id
            PrepareRemaining = prepareDuration < 0f ? 0f : prepareDuration; // 初始化准备时间
            SpawnRemaining = spawnDuration < 0f ? 0f : spawnDuration; // 初始化刷怪时间
        }
    }
}
