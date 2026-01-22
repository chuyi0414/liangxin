using UnityGameFramework.Runtime;
using GFGameEntry = UnityGameFramework.Runtime.GameEntry;

public partial class GameEntry
{
    /// <summary>
    /// 计时器模块入口。
    /// </summary>
    public static TimerComponent Timer { get; private set; }

    /// <summary>
    /// 相机模块入口。
    /// </summary>
    public static CameraComponent Camera { get; private set; }

    /// <summary>
    /// 数据绑定模块入口。
    /// </summary>
    public static DataBindingComponent DataBinding { get; private set; }

    /// <summary>
    /// 实体实例 Id 池模块入口。
    /// </summary>
    public static EntityIdPoolComponent EntityIdPool { get; private set; }

    private static void InitCustomComponents()
    {
        // 自定义模块统一在这里获取（与框架组件一致的方式）。
        Timer = GFGameEntry.GetComponent<TimerComponent>();
        Camera = GFGameEntry.GetComponent<CameraComponent>();
        DataBinding = GFGameEntry.GetComponent<DataBindingComponent>();
        EntityIdPool = GFGameEntry.GetComponent<EntityIdPoolComponent>();
    }
}
