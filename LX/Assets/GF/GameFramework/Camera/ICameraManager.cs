namespace GameFramework.Camera
{
    /// <summary>
    /// 相机模块接口（由框架统一轮询驱动）。
    /// </summary>
    public interface ICameraManager
    {
        /// <summary>
        /// 获取已注册驱动数量。
        /// </summary>
        int DriverCount { get; }

        /// <summary>
        /// 获取当前可更新驱动数量。
        /// </summary>
        int ActiveDriverCount { get; }

        /// <summary>
        /// 注册相机驱动。
        /// </summary>
        /// <param name="driver">驱动实例。</param>
        void RegisterDriver(ICameraDriver driver);

        /// <summary>
        /// 注销相机驱动。
        /// </summary>
        /// <param name="driver">驱动实例。</param>
        void UnregisterDriver(ICameraDriver driver);

        /// <summary>
        /// 检查指定驱动是否已注册。
        /// </summary>
        /// <param name="driver">驱动实例。</param>
        /// <returns>是否已注册。</returns>
        bool HasDriver(ICameraDriver driver);

        /// <summary>
        /// 清空全部驱动。
        /// </summary>
        void ClearDrivers();
    }
}