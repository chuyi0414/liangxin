using System;
using System.Collections.Generic;
using GameFramework;

namespace GameFramework.Camera
{
    /// <summary>
    /// 相机模块实现（由框架统一轮询驱动）。
    /// </summary>
    internal sealed class CameraManager : GameFrameworkModule, ICameraManager
    {
        /// <summary>
        /// 已注册驱动列表。
        /// </summary>
        private readonly List<ICameraDriver> m_Drivers = new List<ICameraDriver>(16);

        /// <summary>
        /// 更新过程中待添加的驱动列表。
        /// </summary>
        private readonly List<ICameraDriver> m_PendingAdd = new List<ICameraDriver>(8);

        /// <summary>
        /// 更新过程中待移除的驱动列表。
        /// </summary>
        private readonly List<ICameraDriver> m_PendingRemove = new List<ICameraDriver>(8);

        /// <summary>
        /// 是否需要重新排序驱动列表。
        /// </summary>
        private bool m_IsDirty;

        /// <summary>
        /// 是否正在执行驱动更新。
        /// </summary>
        private bool m_IsUpdating;

        /// <summary>
        /// 获取已注册驱动数量。
        /// </summary>
        public int DriverCount => m_Drivers.Count;

        /// <summary>
        /// 获取当前可更新驱动数量。
        /// </summary>
        public int ActiveDriverCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < m_Drivers.Count; i++)
                {
                    ICameraDriver driver = m_Drivers[i];
                    if (driver != null && driver.IsActive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 注册相机驱动。
        /// </summary>
        /// <param name="driver">驱动实例。</param>
        public void RegisterDriver(ICameraDriver driver)
        {
            if (driver == null)
            {
                return;
            }

            if (m_IsUpdating)
            {
                if (m_PendingRemove.Contains(driver))
                {
                    m_PendingRemove.Remove(driver);
                }

                if (!m_PendingAdd.Contains(driver))
                {
                    m_PendingAdd.Add(driver);
                }

                return;
            }

            AddDriver(driver);
        }

        /// <summary>
        /// 注销相机驱动。
        /// </summary>
        /// <param name="driver">驱动实例。</param>
        public void UnregisterDriver(ICameraDriver driver)
        {
            if (driver == null)
            {
                return;
            }

            if (m_IsUpdating)
            {
                if (m_PendingAdd.Contains(driver))
                {
                    m_PendingAdd.Remove(driver);
                }

                if (!m_PendingRemove.Contains(driver))
                {
                    m_PendingRemove.Add(driver);
                }

                return;
            }

            RemoveDriver(driver);
        }

        /// <summary>
        /// 检查指定驱动是否已注册。
        /// </summary>
        /// <param name="driver">驱动实例。</param>
        /// <returns>是否已注册。</returns>
        public bool HasDriver(ICameraDriver driver)
        {
            return driver != null && m_Drivers.Contains(driver);
        }

        /// <summary>
        /// 清空全部驱动。
        /// </summary>
        public void ClearDrivers()
        {
            m_Drivers.Clear();
            m_PendingAdd.Clear();
            m_PendingRemove.Clear();
            m_IsDirty = false;
        }

        /// <summary>
        /// 模块轮询更新（由框架统一调用）。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            ApplyPending();
            RemoveNullDrivers();

            if (m_IsDirty)
            {
                SortDrivers();
                m_IsDirty = false;
            }

            m_IsUpdating = true;
            for (int i = 0; i < m_Drivers.Count; i++)
            {
                ICameraDriver driver = m_Drivers[i];
                if (driver == null || !driver.IsActive)
                {
                    continue;
                }

                try
                {
                    driver.OnUpdate(elapseSeconds, realElapseSeconds);
                }
                catch (Exception exception)
                {
                    GameFrameworkLog.Warning("Camera driver update exception: {0}", exception.Message);
                }
            }

            m_IsUpdating = false;
            ApplyPending();
        }

        /// <summary>
        /// 模块关闭并清理。
        /// </summary>
        internal override void Shutdown()
        {
            m_Drivers.Clear();
            m_PendingAdd.Clear();
            m_PendingRemove.Clear();
            m_IsDirty = false;
            m_IsUpdating = false;
        }

        /// <summary>
        /// 添加驱动到列表并标记排序。
        /// </summary>
        /// <param name="driver">驱动实例。</param>
        private void AddDriver(ICameraDriver driver)
        {
            if (driver == null || m_Drivers.Contains(driver))
            {
                return;
            }

            m_Drivers.Add(driver);
            m_IsDirty = true;
        }

        /// <summary>
        /// 从列表移除指定驱动。
        /// </summary>
        /// <param name="driver">驱动实例。</param>
        private void RemoveDriver(ICameraDriver driver)
        {
            if (driver == null)
            {
                return;
            }

            m_Drivers.Remove(driver);
        }

        /// <summary>
        /// 应用更新过程中收集到的增删操作。
        /// </summary>
        private void ApplyPending()
        {
            if (m_PendingRemove.Count > 0)
            {
                for (int i = 0; i < m_PendingRemove.Count; i++)
                {
                    RemoveDriver(m_PendingRemove[i]);
                }

                m_PendingRemove.Clear();
            }

            if (m_PendingAdd.Count > 0)
            {
                for (int i = 0; i < m_PendingAdd.Count; i++)
                {
                    AddDriver(m_PendingAdd[i]);
                }

                m_PendingAdd.Clear();
            }
        }

        /// <summary>
        /// 按优先级对驱动列表排序（降序）。
        /// </summary>
        private void SortDrivers()
        {
            m_Drivers.Sort((a, b) =>
            {
                if (a == null && b == null)
                {
                    return 0;
                }

                if (a == null)
                {
                    return 1;
                }

                if (b == null)
                {
                    return -1;
                }

                return b.Priority.CompareTo(a.Priority);
            });
        }

        /// <summary>
        /// 移除已失效的空驱动。
        /// </summary>
        private void RemoveNullDrivers()
        {
            for (int i = m_Drivers.Count - 1; i >= 0; i--)
            {
                if (m_Drivers[i] == null)
                {
                    m_Drivers.RemoveAt(i);
                }
            }
        }
    }
}