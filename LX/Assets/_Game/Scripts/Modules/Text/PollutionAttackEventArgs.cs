using GameFramework;
using GameFramework.Event;

/// <summary>
/// 按下 Q 键事件参数。
/// </summary>
public sealed class PollutionAttackEventArgs : GameEventArgs
{
    /// <summary>
    /// 按下 Q 键事件编号。
    /// </summary>
    public static readonly int EventId = typeof(PollutionAttackEventArgs).GetHashCode();

    /// <summary>
    /// 初始化按下 Q 键事件的新实例。
    /// </summary>
    public PollutionAttackEventArgs()
    {
        UserData = null;
    }

    /// <summary>
    /// 获取事件编号。
    /// </summary>
    public override int Id
    {
        get
        {
            return EventId;
        }
    }

    /// <summary>
    /// 获取用户自定义数据（可选）。
    /// </summary>
    public object UserData
    {
        get;
        private set;
    }

    /// <summary>
    /// 创建按下 Q 键事件。
    /// </summary>
    /// <param name="userData">用户自定义数据（可选）。</param>
    /// <returns>创建的按下 Q 键事件实例。</returns>
    public static PollutionAttackEventArgs Create(object userData = null)
    {
        PollutionAttackEventArgs qKeyPressedEventArgs = ReferencePool.Acquire<PollutionAttackEventArgs>();
        qKeyPressedEventArgs.UserData = userData;
        return qKeyPressedEventArgs;
    }

    /// <summary>
    /// 清理事件数据。
    /// </summary>
    public override void Clear()
    {
        UserData = null;
    }
}
