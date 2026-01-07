// 引用 CYFramework 命名空间，使用 CY.Log 等入口
using CYFramework; // CYFramework 入口引用
// 引用基础设施命名空间，使用 ServiceLocator 与生命周期接口
using CYFramework.Infrastructure; // ServiceLocator 与生命周期接口引用
// 引用 UnityEngine 命名空间，使用 MonoBehaviour/Camera/Vector 等类型
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 相机管理器：集中缓存相机并负责跟随逻辑。
/// </summary>
public sealed class CameraManager : MonoBehaviour, IInitializable, IUpdateable, ILateUpdateable, IPausable, IDisposableEx // 相机管理器定义
{
    /// <summary>是否在切场景时保留该对象。</summary>
    [SerializeField] private bool _dontDestroyOnLoad = true; // 常驻开关
    /// <summary>世界相机引用。</summary>
    [SerializeField] private Camera _worldCamera; // 世界相机缓存
    /// <summary>UI 相机引用（Overlay 可为空）。</summary>
    [SerializeField] private Camera _uiCamera; // UI 相机缓存
    /// <summary>是否使用 LateUpdate 驱动跟随。</summary>
    [SerializeField] private bool _useLateUpdate = true; // LateUpdate 跟随开关
    /// <summary>相机跟随偏移（XY 平面）。</summary>
    [SerializeField] private Vector2 _followOffset = Vector2.zero; // 跟随偏移
    /// <summary>相机跟随平滑时间（<=0 表示不平滑）。</summary>
    [SerializeField] private float _followSmoothTime = 0.15f; // 平滑时间

    /// <summary>当前跟随目标。</summary>
    private Transform _followTarget; // 跟随目标缓存
    /// <summary>平滑移动速度缓存。</summary>
    private Vector3 _followVelocity; // 平滑速度缓存
    /// <summary>是否已注册到 ServiceLocator。</summary>
    private bool _registered; // 注册标记
    /// <summary>是否已初始化。</summary>
    private bool _initialized; // 初始化标记
    /// <summary>是否处于暂停状态。</summary>
    private bool _paused; // 暂停标记
    /// <summary>是否已释放。</summary>
    private bool _disposed; // 销毁标记
    /// <summary>是否已提示世界相机缺失。</summary>
    private bool _warnedWorldCamera; // 世界相机缺失提示标记
    /// <summary>是否已提示 UI 相机缺失。</summary>
    private bool _warnedUiCamera; // UI 相机缺失提示标记

    /// <summary>世界相机只读访问。</summary>
    public Camera WorldCamera => _worldCamera; // 世界相机只读访问
    /// <summary>UI 相机只读访问。</summary>
    public Camera UICamera => _uiCamera; // UI 相机只读访问
    /// <summary>当前跟随目标只读访问。</summary>
    public Transform FollowTarget => _followTarget; // 跟随目标只读访问

    /// <summary>
    /// Unity Awake：注册到 ServiceLocator 并处理常驻。
    /// </summary>
    private void Awake() // 生命周期：Awake
    {
        if (ServiceLocator.TryGet<CameraManager>(out var existing) && existing != this)
        {
            Destroy(gameObject); // 场景重复挂载时销毁
            return; // 直接退出
        }

        if (_dontDestroyOnLoad && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject); // 设置常驻
        }

        ServiceLocator.RegisterInstance(this); // 注册到服务定位器
        _registered = true; // 标记已注册
    }

    /// <summary>
    /// Unity OnDestroy：注销服务并清理。
    /// </summary>
    private void OnDestroy() // 生命周期：OnDestroy
    {
        if (_registered)
        {
            Dispose(); // 释放资源
            ServiceLocator.Unregister<CameraManager>(); // 注销服务
            _registered = false; // 清理注册标记
        }
    }

    /// <summary>
    /// 初始化：缓存相机引用。
    /// </summary>
    public void Initialize() // 生命周期：Initialize
    {
        CacheCameras(); // 缓存相机引用
        _initialized = true; // 标记初始化完成
    }

    /// <summary>
    /// Update：当使用 Update 驱动时执行跟随。
    /// </summary>
    /// <param name="deltaTime">帧间隔时间。</param>
    public void OnUpdate(float deltaTime) // 生命周期：OnUpdate
    {
        if (!_useLateUpdate)
        {
            UpdateFollow(deltaTime); // Update 驱动跟随
        }
    }

    /// <summary>
    /// LateUpdate：当使用 LateUpdate 驱动时执行跟随。
    /// </summary>
    /// <param name="deltaTime">帧间隔时间。</param>
    public void OnLateUpdate(float deltaTime) // 生命周期：OnLateUpdate
    {
        if (_useLateUpdate)
        {
            UpdateFollow(deltaTime); // LateUpdate 驱动跟随
        }
    }

    /// <summary>
    /// 暂停回调：停止跟随刷新。
    /// </summary>
    public void OnPause() // 生命周期：OnPause
    {
        _paused = true; // 标记暂停
    }

    /// <summary>
    /// 恢复回调：恢复跟随刷新。
    /// </summary>
    /// <param name="pauseDuration">暂停时长。</param>
    public void OnResume(float pauseDuration) // 生命周期：OnResume
    {
        _paused = false; // 取消暂停
        _followVelocity = Vector3.zero; // 清空平滑速度缓存
    }

    /// <summary>
    /// 释放资源：清理引用。
    /// </summary>
    public void Dispose() // 生命周期：Dispose
    {
        _disposed = true; // 标记已释放
        _followTarget = null; // 清理跟随目标
        _followVelocity = Vector3.zero; // 清空平滑速度缓存
    }

    /// <summary>
    /// 设置世界相机引用。
    /// </summary>
    /// <param name="camera">世界相机。</param>
    public void SetWorldCamera(Camera camera) // 世界相机设置入口
    {
        _worldCamera = camera; // 写入世界相机引用
        _warnedWorldCamera = false; // 重置提示标记
    }

    /// <summary>
    /// 设置 UI 相机引用。
    /// </summary>
    /// <param name="camera">UI 相机。</param>
    public void SetUICamera(Camera camera) // UI 相机设置入口
    {
        _uiCamera = camera; // 写入 UI 相机引用
        _warnedUiCamera = false; // 重置提示标记
    }

    /// <summary>
    /// 设置相机跟随目标。
    /// </summary>
    /// <param name="target">跟随目标。</param>
    /// <param name="snap">是否立即对齐到目标位置。</param>
    public void SetFollowTarget(Transform target, bool snap) // 跟随目标设置入口
    {
        _followTarget = target; // 写入跟随目标
        _followVelocity = Vector3.zero; // 清空平滑速度缓存
        if (snap)
        {
            SnapToTarget(); // 立即对齐到目标位置
        }
    }

    /// <summary>
    /// 清理相机跟随目标。
    /// </summary>
    public void ClearFollowTarget() // 跟随目标清理入口
    {
        _followTarget = null; // 清空跟随目标
        _followVelocity = Vector3.zero; // 清空平滑速度缓存
    }

    /// <summary>
    /// 缓存相机引用（仅在需要时调用）。
    /// </summary>
    private void CacheCameras() // 相机缓存入口
    {
        if (_worldCamera == null)
        {
            _worldCamera = Camera.main; // 尝试获取主相机
            if (_worldCamera == null)
            {
                _worldCamera = FindObjectOfType<Camera>(); // 回退查找任意相机（低频）
            }
        }

        if (_worldCamera == null && !_warnedWorldCamera)
        {
            CY.LogWarning("[CameraManager] 未找到世界相机，请在 Inspector 指定。"); // 输出缺失提示
            _warnedWorldCamera = true; // 标记已提示
        }

        if (_uiCamera == null && !_warnedUiCamera)
        {
            CY.LogWarning("[CameraManager] 未找到 UI 相机（Overlay 可为空）。"); // 输出缺失提示
            _warnedUiCamera = true; // 标记已提示
        }
    }

    /// <summary>
    /// 执行相机跟随逻辑。
    /// </summary>
    /// <param name="deltaTime">帧间隔时间。</param>
    private void UpdateFollow(float deltaTime) // 跟随更新入口
    {
        if (!_initialized || _disposed)
        {
            return; // 未初始化或已释放时退出
        }

        if (_paused)
        {
            return; // 暂停时退出
        }

        if (_followTarget == null)
        {
            return; // 无跟随目标时退出
        }

        if (_worldCamera == null)
        {
            CacheCameras(); // 重新尝试缓存相机
            if (_worldCamera == null)
            {
                return; // 相机仍为空时退出
            }
        }

        var cameraTransform = _worldCamera.transform; // 获取相机 Transform
        var targetPosition = _followTarget.position; // 获取目标位置
        var desiredPosition = new Vector3( // 计算期望相机位置
            targetPosition.x + _followOffset.x, // 应用 X 偏移
            targetPosition.y + _followOffset.y, // 应用 Y 偏移
            cameraTransform.position.z); // 保持相机 Z 不变

        if (_followSmoothTime <= 0f)
        {
            cameraTransform.position = desiredPosition; // 无平滑时直接移动
            _followVelocity = Vector3.zero; // 清空平滑速度缓存
            return; // 直接移动后退出
        }

        cameraTransform.position = Vector3.SmoothDamp( // 平滑移动相机
            cameraTransform.position, // 当前相机位置
            desiredPosition, // 目标位置
            ref _followVelocity, // 平滑速度缓存
            _followSmoothTime, // 平滑时间
            Mathf.Infinity, // 最大速度不限制
            deltaTime); // 当前帧间隔
    }

    /// <summary>
    /// 立即对齐相机到目标位置。
    /// </summary>
    private void SnapToTarget() // 立即对齐入口
    {
        if (_followTarget == null)
        {
            return; // 无跟随目标时退出
        }

        if (_worldCamera == null)
        {
            CacheCameras(); // 重新尝试缓存相机
            if (_worldCamera == null)
            {
                return; // 相机仍为空时退出
            }
        }

        var cameraTransform = _worldCamera.transform; // 获取相机 Transform
        var targetPosition = _followTarget.position; // 获取目标位置
        cameraTransform.position = new Vector3( // 立即设置相机位置
            targetPosition.x + _followOffset.x, // 应用 X 偏移
            targetPosition.y + _followOffset.y, // 应用 Y 偏移
            cameraTransform.position.z); // 保持相机 Z 不变
        _followVelocity = Vector3.zero; // 清空平滑速度缓存
    }
}
