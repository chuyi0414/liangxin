using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==================== 游戏流程事件 ====================

public struct GameStartEvent
{
    public int StageId;
}

public struct GamePauseEvent
{
    public bool IsPaused;
}

public struct GameOverEvent
{
    public bool IsVictory;
    public int WaveReached;
    public int Score;
}

// ==================== 波次事件 ====================

public struct WaveStartEvent
{
    public int WaveNumber;
    public float PrepareTime;
}

public struct WaveEndEvent
{
    public int WaveNumber;
    public int EnemiesKilled;
}

// ==================== 资源事件 ====================

public struct GoldChangedEvent
{
    public int OldAmount;
    public int NewAmount;
    public int Delta;
}

public struct ConscienceChangedEvent
{
    public int OldAmount;
    public int NewAmount;
    public int Delta;
}