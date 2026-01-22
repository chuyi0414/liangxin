using System;
using System.Collections.Generic;
using GameFramework;
using GameFramework.Camera;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 相机组件（框架模块包装）。
/// 仅提供 Unity 侧入口，更新由框架模块统一轮询驱动。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Camera")]
public sealed class CameraComponent : GameFrameworkComponent
{
    /// <summary>
    /// 默认主相机 Tag 名称。
    /// </summary>
    private const string DefaultMainCameraTag = "MainCamera";

    /// <summary>
    /// 默认相机引用（为空时会尝试从 Camera.main 刷新）。
    /// </summary>
    [SerializeField]
    private Camera m_DefaultCamera;

    /// <summary>
    /// 默认相机缓存 key（用于字典快速访问）。
    /// </summary>
    [SerializeField]
    private string m_DefaultCameraKey = "main";

    /// <summary>
    /// 相机缓存字典（key 为相机名称或自定义 key）。
    /// </summary>
    private readonly Dictionary<string, Camera> m_CameraMap = new Dictionary<string, Camera>(StringComparer.Ordinal);

    /// <summary>
    /// 相机模块实例（由框架创建并统一轮询）。
    /// </summary>
    private ICameraManager m_CameraManager;

    /// <summary>
    /// 获取已注册驱动数量。
    /// </summary>
    public int DriverCount
    {
        get
        {
            EnsureCameraManager();
            return m_CameraManager != null ? m_CameraManager.DriverCount : 0;
        }
    }

    /// <summary>
    /// 获取当前可更新驱动数量。
    /// </summary>
    public int ActiveDriverCount
    {
        get
        {
            EnsureCameraManager();
            return m_CameraManager != null ? m_CameraManager.ActiveDriverCount : 0;
        }
    }

    /// <summary>
    /// 获取或设置默认相机（为空时会尝试回退到 Camera.main）。
    /// </summary>
    public Camera DefaultCamera
    {
        get
        {
            if (m_DefaultCamera == null)
            {
                m_DefaultCamera = Camera.main;
            }

            return m_DefaultCamera;
        }
        set
        {
            m_DefaultCamera = value;
        }
    }

    /// <summary>
    /// 初始化组件并缓存相机模块。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        EnsureCameraManager();
        RegisterDefaultCameraIfNeeded();
    }

    /// <summary>
    /// 获取默认相机（可选择在为空时刷新）。
    /// </summary>
    /// <param name="refreshIfNull">为空时是否刷新 Camera.main。</param>
    /// <returns>默认相机引用。</returns>
    public Camera GetDefaultCamera(bool refreshIfNull = true)
    {
        if (m_DefaultCamera == null && refreshIfNull)
        {
            RefreshDefaultCamera();
        }

        return m_DefaultCamera;
    }

    /// <summary>
    /// 刷新默认相机引用（使用 Camera.main）。
    /// </summary>
    /// <returns>刷新后的默认相机。</returns>
    public Camera RefreshDefaultCamera()
    {
        m_DefaultCamera = Camera.main;
        return m_DefaultCamera;
    }

    /// <summary>
    /// 通过 key 获取相机（仅从缓存字典获取）。
    /// </summary>
    /// <param name="cameraName">相机缓存 key。</param>
    /// <returns>找到的相机引用。</returns>
    public Camera GetCameraByName(string cameraName)
    {
        if (string.IsNullOrWhiteSpace(cameraName))
        {
            Log.Warning("CameraComponent: 相机名称无效。");
            return null;
        }

        return GetCamera(cameraName);
    }

    /// <summary>
    /// 通过 Tag 查找相机（仅查找激活对象上的相机）。
    /// </summary>
    /// <param name="tag">Tag 名称。</param>
    /// <returns>找到的相机引用。</returns>
    public Camera GetCameraByTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            Log.Warning("CameraComponent: Tag 无效。");
            return null;
        }

        try
        {
            GameObject target = GameObject.FindGameObjectWithTag(tag);
            return target != null ? target.GetComponent<Camera>() : null;
        }
        catch (UnityException exception)
        {
            Log.Warning("CameraComponent: Tag 查找失败，原因：{0}", exception.Message);
            return null;
        }
    }

    /// <summary>
    /// 获取所有激活相机（使用外部列表缓存，避免 GC）。
    /// </summary>
    /// <param name="results">接收相机的列表。</param>
    /// <returns>实际填充数量。</returns>
    public int GetAllCameras(List<Camera> results)
    {
        if (results == null)
        {
            Log.Warning("CameraComponent: results 为空。");
            return 0;
        }

        results.Clear();

        int count = Camera.allCamerasCount;
        if (count <= 0)
        {
            return 0;
        }

        Camera[] cameras = new Camera[count];
        int actualCount = Camera.GetAllCameras(cameras);
        for (int i = 0; i < actualCount; i++)
        {
            if (cameras[i] != null)
            {
                results.Add(cameras[i]);
            }
        }

        return results.Count;
    }

    /// <summary>
    /// 获取所有激活相机数组（会产生分配）。
    /// </summary>
    /// <returns>相机数组。</returns>
    public Camera[] GetAllCameras()
    {
        int count = Camera.allCamerasCount;
        if (count <= 0)
        {
            return new Camera[0];
        }

        Camera[] cameras = new Camera[count];
        int actualCount = Camera.GetAllCameras(cameras);
        if (actualCount == count)
        {
            return cameras;
        }

        Camera[] actual = new Camera[actualCount];
        for (int i = 0; i < actualCount; i++)
        {
            actual[i] = cameras[i];
        }

        return actual;
    }

    /// <summary>
    /// 注册相机到缓存字典。
    /// </summary>
    /// <param name="camera">相机实例。</param>
    /// <param name="key">缓存 key（为空则使用相机名称）。</param>
    /// <returns>是否注册成功。</returns>
    public bool RegisterCamera(Camera camera, string key = null)
    {
        if (camera == null)
        {
            Log.Warning("CameraComponent: 注册失败，相机为空。");
            return false;
        }

        string resolvedKey = ResolveCameraKey(camera, key);
        if (string.IsNullOrWhiteSpace(resolvedKey))
        {
            Log.Warning("CameraComponent: 注册失败，key 无效。");
            return false;
        }

        if (m_CameraMap.TryGetValue(resolvedKey, out Camera existed))
        {
            if (existed == camera)
            {
                return true;
            }

            Log.Warning("CameraComponent: 注册失败，key 已存在：{0}", resolvedKey);
            return false;
        }

        m_CameraMap[resolvedKey] = camera;
        return true;
    }

    /// <summary>
    /// 通过 key 注销相机缓存。
    /// </summary>
    /// <param name="key">缓存 key。</param>
    /// <returns>是否注销成功。</returns>
    public bool UnregisterCamera(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Log.Warning("CameraComponent: 注销失败，key 无效。");
            return false;
        }

        return m_CameraMap.Remove(key);
    }

    /// <summary>
    /// 注销指定相机的所有缓存条目。
    /// </summary>
    /// <param name="camera">相机实例。</param>
    /// <returns>是否至少移除了一个条目。</returns>
    public bool UnregisterCamera(Camera camera)
    {
        if (camera == null)
        {
            return false;
        }

        bool removed = false;
        List<string> keysToRemove = null;

        foreach (KeyValuePair<string, Camera> pair in m_CameraMap)
        {
            if (pair.Value == camera)
            {
                if (keysToRemove == null)
                {
                    keysToRemove = new List<string>();
                }

                keysToRemove.Add(pair.Key);
            }
        }

        if (keysToRemove != null)
        {
            for (int i = 0; i < keysToRemove.Count; i++)
            {
                removed |= m_CameraMap.Remove(keysToRemove[i]);
            }
        }

        return removed;
    }

    /// <summary>
    /// 尝试通过 key 获取相机（不存在或已销毁则返回 false）。 
    /// </summary>
    /// <param name="key">缓存 key。</param>
    /// <param name="camera">输出相机。</param>
    /// <returns>是否获取成功。</returns>
    public bool TryGetCamera(string key, out Camera camera)
    {
        camera = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!m_CameraMap.TryGetValue(key, out Camera cached))
        {
            return false;
        }

        if (cached == null)
        {
            m_CameraMap.Remove(key);
            return false;
        }

        camera = cached;
        return true;
    }

    /// <summary>
    /// 通过 key 获取相机（不存在返回 null）。
    /// </summary>
    /// <param name="key">缓存 key。</param>
    /// <returns>相机实例。</returns>
    public Camera GetCamera(string key)
    {
        return TryGetCamera(key, out Camera camera) ? camera : null;
    }

    /// <summary>
    /// 通过 key 判断相机是否存在。
    /// </summary>
    /// <param name="key">缓存 key。</param>
    /// <returns>是否存在。</returns>
    public bool HasCamera(string key)
    {
        return TryGetCamera(key, out _);
    }

    /// <summary>
    /// 刷新相机缓存（仅包含激活相机）。
    /// </summary>
    public void RefreshCameraCache()
    {
        m_CameraMap.Clear();

        int count = Camera.allCamerasCount;
        if (count <= 0)
        {
            return;
        }

        Camera[] cameras = new Camera[count];
        int actualCount = Camera.GetAllCameras(cameras);
        for (int i = 0; i < actualCount; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            string key = ResolveCameraKey(camera, null);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!m_CameraMap.ContainsKey(key))
            {
                m_CameraMap[key] = camera;
            }
            else
            {
                Log.Warning("CameraComponent: 刷新缓存时发现重复 key，已忽略：{0}", key);
            }
        }
    }

    /// <summary>
    /// 创建相机（挂在当前 CameraComponent 下）。
    /// </summary>
    /// <param name="cameraName">相机名称。</param>
    /// <returns>创建的相机引用。</returns>
    public Camera CreateCamera(string cameraName)
    {
        return CreateCamera(cameraName, false, false, null, null);
    }

    /// <summary>
    /// 创建相机（可选择设置 MainCamera Tag）。挂在当前 CameraComponent 下。
    /// </summary>
    /// <param name="cameraName">相机名称。</param>
    /// <param name="setMainTag">是否设置为 MainCamera Tag。</param>
    /// <returns>创建的相机引用。</returns>
    public Camera CreateCamera(string cameraName, bool setMainTag)
    {
        return CreateCamera(cameraName, setMainTag, false, null, null);
    }

    /// <summary>
    /// 创建相机（可选择设置 MainCamera Tag/默认相机）。挂在当前 CameraComponent 下。
    /// </summary>
    /// <param name="cameraName">相机名称。</param>
    /// <param name="setMainTag">是否设置为 MainCamera Tag。</param>
    /// <param name="setAsDefault">是否设为默认相机。</param>
    /// <returns>创建的相机引用。</returns>
    public Camera CreateCamera(string cameraName, bool setMainTag, bool setAsDefault)
    {
        return CreateCamera(cameraName, setMainTag, setAsDefault, null, null);
    }

    /// <summary>
    /// 创建相机（可选择设置 MainCamera Tag/默认相机，并复制模板设置）。挂在当前 CameraComponent 下。
    /// </summary>
    /// <param name="cameraName">相机名称。</param>
    /// <param name="setMainTag">是否设置为 MainCamera Tag。</param>
    /// <param name="setAsDefault">是否设为默认相机。</param>
    /// <param name="template">模板相机（为空则使用默认相机）。</param>
    /// <returns>创建的相机引用。</returns>
    public Camera CreateCamera(string cameraName, bool setMainTag, bool setAsDefault, Camera template)
    {
        return CreateCamera(cameraName, setMainTag, setAsDefault, template, null);
    }

    /// <summary>
    /// 创建相机（可选择设置 MainCamera Tag/默认相机，并复制模板设置）。
    /// 默认挂在 CameraComponent 下，可自定义父节点。
    /// </summary>
    /// <param name="cameraName">相机名称。</param>
    /// <param name="setMainTag">是否设置为 MainCamera Tag。</param>
    /// <param name="setAsDefault">是否设为默认相机。</param>
    /// <param name="template">模板相机（为空则使用默认相机）。</param>
    /// <param name="parent">父节点（为空则挂在当前 CameraComponent 下）。</param>
    /// <returns>创建的相机引用。</returns>
    public Camera CreateCamera(string cameraName, bool setMainTag, bool setAsDefault, Camera template, Transform parent)
    {
        string finalName = string.IsNullOrWhiteSpace(cameraName) ? "Camera" : cameraName;
        GameObject cameraObject = new GameObject(finalName);

        Transform parentTransform = parent != null ? parent : transform;
        if (parentTransform != null)
        {
            cameraObject.transform.SetParent(parentTransform, false);
        }

        Camera camera = cameraObject.AddComponent<Camera>();

        Camera copySource = template != null ? template : GetDefaultCamera(false);
        if (copySource != null)
        {
            CopyCameraSettings(copySource, camera);
            CopyTransform(copySource.transform, camera.transform);
        }

        if (setMainTag)
        {
            TrySetMainCameraTag(cameraObject);
        }

        if (setAsDefault)
        {
            m_DefaultCamera = camera;
        }

        RegisterCamera(camera, finalName);
        return camera;
    }

    /// <summary>
    /// 创建相机并指定缓存 key（key 冲突时拒绝创建）。
    /// </summary>
    /// <param name="cameraName">相机名称。</param>
    /// <param name="key">缓存 key（为空则使用相机名称）。</param>
    /// <returns>创建的相机引用，失败返回 null。</returns>
    public Camera CreateCamera(string cameraName, string key)
    {
        return CreateCamera(cameraName, key, false, false, null, null);
    }

    /// <summary>
    /// 创建相机并指定缓存 key（key 冲突时拒绝创建）。
    /// </summary>
    /// <param name="cameraName">相机名称。</param>
    /// <param name="key">缓存 key（为空则使用相机名称）。</param>
    /// <param name="setMainTag">是否设置为 MainCamera Tag。</param>
    /// <returns>创建的相机引用，失败返回 null。</returns>
    public Camera CreateCamera(string cameraName, string key, bool setMainTag)
    {
        return CreateCamera(cameraName, key, setMainTag, false, null, null);
    }

    /// <summary>
    /// 创建相机并指定缓存 key（key 冲突时拒绝创建）。
    /// </summary>
    /// <param name="cameraName">相机名称。</param>
    /// <param name="key">缓存 key（为空则使用相机名称）。</param>
    /// <param name="setMainTag">是否设置为 MainCamera Tag。</param>
    /// <param name="setAsDefault">是否设为默认相机。</param>
    /// <returns>创建的相机引用，失败返回 null。</returns>
    public Camera CreateCamera(string cameraName, string key, bool setMainTag, bool setAsDefault)
    {
        return CreateCamera(cameraName, key, setMainTag, setAsDefault, null, null);
    }

    /// <summary>
    /// 创建相机并指定缓存 key（key 冲突时拒绝创建）。
    /// </summary>
    /// <param name="cameraName">相机名称。</param>
    /// <param name="key">缓存 key（为空则使用相机名称）。</param>
    /// <param name="setMainTag">是否设置为 MainCamera Tag。</param>
    /// <param name="setAsDefault">是否设为默认相机。</param>
    /// <param name="template">模板相机（为空则使用默认相机）。</param>
    /// <returns>创建的相机引用，失败返回 null。</returns>
    public Camera CreateCamera(string cameraName, string key, bool setMainTag, bool setAsDefault, Camera template)
    {
        return CreateCamera(cameraName, key, setMainTag, setAsDefault, template, null);
    }

    /// <summary>
    /// 创建相机并指定缓存 key（key 冲突时拒绝创建）。
    /// 默认挂在 CameraComponent 下，可自定义父节点。
    /// </summary>
    /// <param name="cameraName">相机名称。</param>
    /// <param name="key">缓存 key（为空则使用相机名称）。</param>
    /// <param name="setMainTag">是否设置为 MainCamera Tag。</param>
    /// <param name="setAsDefault">是否设为默认相机。</param>
    /// <param name="template">模板相机（为空则使用默认相机）。</param>
    /// <param name="parent">父节点（为空则挂在当前 CameraComponent 下）。</param>
    /// <returns>创建的相机引用，失败返回 null。</returns>
    public Camera CreateCamera(string cameraName, string key, bool setMainTag, bool setAsDefault, Camera template, Transform parent)
    {
        string finalName = string.IsNullOrWhiteSpace(cameraName) ? "Camera" : cameraName;
        string resolvedKey = string.IsNullOrWhiteSpace(key) ? finalName : key;

        if (m_CameraMap.ContainsKey(resolvedKey))
        {
            Log.Warning("CameraComponent: 创建失败，key 已存在：{0}", resolvedKey);
            return null;
        }

        GameObject cameraObject = new GameObject(finalName);

        Transform parentTransform = parent != null ? parent : transform;
        if (parentTransform != null)
        {
            cameraObject.transform.SetParent(parentTransform, false);
        }

        Camera camera = cameraObject.AddComponent<Camera>();

        Camera copySource = template != null ? template : GetDefaultCamera(false);
        if (copySource != null)
        {
            CopyCameraSettings(copySource, camera);
            CopyTransform(copySource.transform, camera.transform);
        }

        if (setMainTag)
        {
            TrySetMainCameraTag(cameraObject);
        }

        if (setAsDefault)
        {
            m_DefaultCamera = camera;
        }

        RegisterCamera(camera, resolvedKey);
        return camera;
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

        EnsureCameraManager();
        m_CameraManager?.RegisterDriver(driver);
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

        EnsureCameraManager();
        m_CameraManager?.UnregisterDriver(driver);
    }

    /// <summary>
    /// 检查指定驱动是否已注册。
    /// </summary>
    /// <param name="driver">驱动实例。</param>
    /// <returns>是否已注册。</returns>
    public bool HasDriver(ICameraDriver driver)
    {
        if (driver == null)
        {
            return false;
        }

        EnsureCameraManager();
        return m_CameraManager != null && m_CameraManager.HasDriver(driver);
    }

    /// <summary>
    /// 清空全部驱动。
    /// </summary>
    public void ClearDrivers()
    {
        EnsureCameraManager();
        m_CameraManager?.ClearDrivers();
    }

    /// <summary>
    /// 确保相机模块已创建并缓存。
    /// </summary>
    private void EnsureCameraManager()
    {
        if (m_CameraManager == null)
        {
            m_CameraManager = GameFrameworkEntry.GetModule<ICameraManager>();
        }
    }

    /// <summary>
    /// 解析相机缓存 key（为空则使用相机名称）。
    /// </summary>
    /// <param name="camera">相机实例。</param>
    /// <param name="key">自定义 key。</param>
    /// <returns>解析后的 key。</returns>
    private static string ResolveCameraKey(Camera camera, string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        return camera != null ? camera.name : null;
    }

    /// <summary>
    /// 若已配置默认相机，则在启动时注册到缓存字典。
    /// </summary>
    private void RegisterDefaultCameraIfNeeded()
    {
        if (m_DefaultCamera == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(m_DefaultCameraKey))
        {
            Log.Warning("CameraComponent: 默认相机 key 无效，跳过注册。");
            return;
        }

        RegisterCamera(m_DefaultCamera, m_DefaultCameraKey);
    }

    /// <summary>
    /// 复制相机基础设置（不包含 Transform）。
    /// </summary>
    /// <param name="source">来源相机。</param>
    /// <param name="destination">目标相机。</param>
    private static void CopyCameraSettings(Camera source, Camera destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.CopyFrom(source);
    }

    /// <summary>
    /// 复制 Transform 位姿与缩放。
    /// </summary>
    /// <param name="source">来源 Transform。</param>
    /// <param name="destination">目标 Transform。</param>
    private static void CopyTransform(Transform source, Transform destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.position = source.position;
        destination.rotation = source.rotation;
        destination.localScale = source.localScale;
    }

    /// <summary>
    /// 尝试将对象设置为 MainCamera Tag。
    /// </summary>
    /// <param name="cameraObject">相机对象。</param>
    private void TrySetMainCameraTag(GameObject cameraObject)
    {
        if (cameraObject == null)
        {
            return;
        }

        try
        {
            cameraObject.tag = DefaultMainCameraTag;
        }
        catch (UnityException exception)
        {
            Log.Warning("CameraComponent: 设置 MainCamera Tag 失败，原因：{0}", exception.Message);
        }
    }
}