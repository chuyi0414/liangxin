// ============================================================================
// CYFramework - 时间服务接口
// 平台抽象层：封装时间相关的平台差异
// ============================================================================

namespace CYFramework.Runtime.Platform
{
    /// <summary>
    /// 时间服务接口
    /// 用于隔离不同平台的时间 API 差异
    /// </summary>
    public interface ITimeService
    {
        /// <summary>
        /// 游戏启动后的真实时间（秒），不受 TimeScale 影响
        /// </summary>
        float RealtimeSinceStartup { get; }

        /// <summary>
        /// 上一帧到当前帧的时间间隔（秒），受 TimeScale 影响
        /// </summary>
        float DeltaTime { get; }

        /// <summary>
        /// 上一帧到当前帧的时间间隔（秒），不受 TimeScale 影响
        /// </summary>
        float UnscaledDeltaTime { get; }

        /// <summary>
        /// 当前时间缩放
        /// </summary>
        float TimeScale { get; set; }
    }

    /// <summary>
    /// Unity 平台的时间服务实现
    /// </summary>
    public class UnityTimeService : ITimeService
    {
        public float RealtimeSinceStartup => UnityEngine.Time.realtimeSinceStartup;
        public float DeltaTime => UnityEngine.Time.deltaTime;
        public float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;

        public float TimeScale
        {
            get => UnityEngine.Time.timeScale;
            set => UnityEngine.Time.timeScale = value;
        }
    }
}
