// 引用 UnityEngine，使用 ScriptableObject
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 敌人 AI 基类（ScriptableObject 资产）。
/// </summary>
public abstract class EnemyAIBase : ScriptableObject // 敌人 AI 抽象基类
{
    /// <summary>AI 名称（用于调试显示）。</summary>
    public abstract string AIName { get; } // AI 名称属性

    /// <summary>AI 进入时回调。</summary>
    /// <param name="enemy">敌人实体。</param>
    public virtual void OnEnter(EnemyEntity enemy) { } // AI 进入回调

    /// <summary>AI 退出时回调。</summary>
    /// <param name="enemy">敌人实体。</param>
    public virtual void OnExit(EnemyEntity enemy) { } // AI 退出回调

    /// <summary>AI 每帧逻辑。</summary>
    /// <param name="enemy">敌人实体。</param>
    /// <param name="deltaTime">帧时间。</param>
    public abstract void Tick(EnemyEntity enemy, float deltaTime); // AI Tick 抽象接口
}
