using UnityGameFramework.Runtime;
using GFGameEntry = UnityGameFramework.Runtime.GameEntry;

public partial class GameEntry
{
    /// <summary>
    /// 计时器模块入口。
    /// </summary>
    public static TimerComponent Timer { get; private set; }

    private static void InitCustomComponents()
    {
        // 自定义模块统一在这里获取（与框架组件一致的方式）。
        Timer = GFGameEntry.GetComponent<TimerComponent>();
    }
}
