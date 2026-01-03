// 引用 UnityEngine，使用 Vector2
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 波次准备阶段开始事件。
/// </summary>
public struct WavePrepareStartedEvent // 准备阶段事件结构体
{
    /// <summary>波次 Id。</summary>
    public int WaveId; // 波次 Id
    /// <summary>通道类型。</summary>
    public WaveChannel Channel; // 波次通道
}

/// <summary>
/// 波次刷怪阶段开始事件。
/// </summary>
public struct WaveSpawnStartedEvent // 刷怪阶段事件结构体
{
    /// <summary>波次 Id。</summary>
    public int WaveId; // 波次 Id
    /// <summary>通道类型。</summary>
    public WaveChannel Channel; // 波次通道
}

/// <summary>
/// 波次刷怪生成事件。
/// </summary>
public struct WaveSpawnedEvent // 刷怪生成事件结构体
{
    /// <summary>波次 Id。</summary>
    public int WaveId; // 波次 Id
    /// <summary>生成类型 Id。</summary>
    public int SpawnTypeId; // 生成类型 Id
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
    /// <summary>通道类型。</summary>
    public WaveChannel Channel; // 波次通道
}

/// <summary>
/// 波次暂停事件。
/// </summary>
public struct WavePauseEvent // 波次暂停事件结构体
{
    /// <summary>是否暂停。</summary>
    public bool IsPaused; // 是否暂停
}
