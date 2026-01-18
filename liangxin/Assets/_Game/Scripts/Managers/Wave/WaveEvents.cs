// 引用 UnityEngine，使用 Vector2
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 波次准备阶段开始事件。
/// </summary>
public struct WavePrepareStartedEvent // 准备阶段事件结构体
{
    /// <summary>波次 Id。</summary>
    public int WaveId; // 波次 Id
    /// <summary>是否为奇袭波次。</summary>
    public bool IsAssault; // 奇袭标记
}

/// <summary>
/// 波次刷怪阶段开始事件。
/// </summary>
public struct WaveSpawnStartedEvent // 刷怪阶段事件结构体
{
    /// <summary>波次 Id。</summary>
    public int WaveId; // 波次 Id
    /// <summary>是否为奇袭波次。</summary>
    public bool IsAssault; // 奇袭标记
}

/// <summary>
/// 波次刷怪生成事件。
/// </summary>
public struct WaveSpawnedEvent // 刷怪生成事件结构体
{
    /// <summary>波次 Id。</summary>
    public int WaveId; // 波次 Id
    /// <summary>是否为奇袭波次。</summary>
    public bool IsAssault; // 奇袭标记
    /// <summary>刷怪组 Id。</summary>
    public int SpawnGroupId; // 刷怪组 Id
    /// <summary>敌人 Id。</summary>
    public int EnemyId; // 敌人 Id
    /// <summary>生成位置。</summary>
    public Vector2 Position; // 生成位置
}

/// <summary>
/// 波次结束事件（刷怪阶段结束）。
/// </summary>
public struct WaveFinishedEvent // 波次结束事件结构体
{
    /// <summary>波次 Id。</summary>
    public int WaveId; // 波次 Id
    /// <summary>是否为奇袭波次。</summary>
    public bool IsAssault; // 奇袭标记
    /// <summary>是否允许自动推进。</summary>
    public bool AutoAdvance; // 自动推进标记
}

/// <summary>
/// 波次暂停事件。
/// </summary>
public struct WavePauseEvent // 波次暂停事件结构体
{
    /// <summary>是否暂停。</summary>
    public bool IsPaused; // 是否暂停
}

/// <summary>
/// 波次触发事件（用于脚本/剧情触发波次轨道）。
/// </summary>
public struct WaveTriggerEvent // 波次触发事件结构体
{
    /// <summary>触发标识 Id。</summary>
    public string TriggerId; // 触发标识
}

/// <summary>
/// 波次区域触发事件（进入/离开区域）。
/// </summary>
public struct WaveAreaTriggerEvent // 波次区域事件结构体
{
    /// <summary>区域 Id。</summary>
    public string AreaId; // 区域 Id
    /// <summary>是否进入区域。</summary>
    public bool IsEnter; // 进入标记
}
