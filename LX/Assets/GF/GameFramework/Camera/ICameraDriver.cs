namespace GameFramework.Camera
{
    /// <summary>
    /// 相机驱动接口（由相机模块统一轮询驱动）。
    /// </summary>
    public interface ICameraDriver
    {
        /// <summary>
        /// 驱动优先级（值越大越先更新）。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 是否处于可更新状态（一般与组件启用状态一致）。
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// 模块更新回调（由相机模块统一轮询调用）。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
        void OnUpdate(float elapseSeconds, float realElapseSeconds);
    }
}