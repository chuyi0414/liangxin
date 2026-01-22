using GameFramework.Camera;
using UnityEngine;
using UnityGameFramework.Runtime;
using GFGameEntry = UnityGameFramework.Runtime.GameEntry;

/// <summary>
/// 相机驱动基础类（负责注册与统一时间处理）。
/// </summary>
public abstract class CameraDriverBase : MonoBehaviour, ICameraDriver
{
    /// <summary>
    /// 驱动优先级（值越大越先更新）。
    /// </summary>
    [SerializeField]
    private int _priority = 0;

    /// <summary>
    /// 是否使用真实时间（不受 Time.timeScale 影响）。
    /// </summary>
    [SerializeField]
    private bool _useUnscaledTime = false;

    /// <summary>
    /// 手动指定的相机模块组件（为空则自动查找）。
    /// </summary>
    [SerializeField]
    private CameraComponent _cameraComponent;

    /// <summary>
    /// 驱动控制的相机引用（为空则尝试从当前物体获取）。
    /// </summary>
    [SerializeField]
    private Camera _camera;

    /// <summary>
    /// 是否已完成驱动注册。
    /// </summary>
    private bool _isRegistered;

    /// <summary>
    /// 驱动优先级（值越大越先更新）。
    /// </summary>
    public int Priority => _priority;

    /// <summary>
    /// 是否处于可更新状态（一般与组件启用状态一致）。
    /// </summary>
    public bool IsActive => isActiveAndEnabled;

    /// <summary>
    /// 是否使用真实时间（不受 Time.timeScale 影响）。
    /// </summary>
    public bool UseUnscaledTime => _useUnscaledTime;

    /// <summary>
    /// 获取当前控制的相机引用。
    /// </summary>
    protected Camera TargetCamera => _camera;

    /// <summary>
    /// 获取当前控制的相机 Transform（相机为空时回退到自身）。
    /// </summary>
    protected Transform TargetTransform => _camera != null ? _camera.transform : transform;

    /// <summary>
    /// Unity 启用回调（负责注册驱动）。
    /// </summary>
    private void OnEnable()
    {
        EnsureCamera();
        TryRegister();
        OnDriverEnable();
    }

    /// <summary>
    /// Unity 禁用回调（负责注销驱动）。
    /// </summary>
    private void OnDisable()
    {
        OnDriverDisable();
        TryUnregister();
    }

    /// <summary>
    /// Unity 销毁回调（确保驱动已注销）。
    /// </summary>
    private void OnDestroy()
    {
        OnDriverDestroy();
        TryUnregister();
    }

    /// <summary>
    /// Unity Reset 回调（自动绑定同物体的相机）。
    /// </summary>
    private void Reset()
    {
        _camera = GetComponent<Camera>();
    }

    /// <summary>
    /// 相机模块更新回调（由相机模块统一轮询调用）。
    /// </summary>
    /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
    /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (!IsActive)
        {
            return;
        }

        float deltaTime = _useUnscaledTime ? realElapseSeconds : elapseSeconds;
        OnDriverUpdate(deltaTime, elapseSeconds, realElapseSeconds);
    }

    /// <summary>
    /// 派生类驱动更新逻辑入口。
    /// </summary>
    /// <param name="deltaTime">按驱动时间模式计算后的时间增量（秒）。</param>
    /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
    /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
    protected abstract void OnDriverUpdate(float deltaTime, float elapseSeconds, float realElapseSeconds);

    /// <summary>
    /// 派生类启用钩子（在注册后调用）。
    /// </summary>
    protected virtual void OnDriverEnable()
    {
    }

    /// <summary>
    /// 派生类禁用钩子（在注销前调用）。
    /// </summary>
    protected virtual void OnDriverDisable()
    {
    }

    /// <summary>
    /// 派生类销毁钩子（在注销前调用）。
    /// </summary>
    protected virtual void OnDriverDestroy()
    {
    }

    /// <summary>
    /// 确保相机引用有效（为空则尝试从当前物体获取）。
    /// </summary>
    protected void EnsureCamera()
    {
        if (_camera == null)
        {
            _camera = GetComponent<Camera>();
        }
    }

    /// <summary>
    /// 尝试注册驱动到相机模块。
    /// </summary>
    private void TryRegister()
    {
        if (_isRegistered)
        {
            return;
        }

        if (_cameraComponent == null)
        {
            _cameraComponent = GFGameEntry.GetComponent<CameraComponent>();
        }

        if (_cameraComponent == null)
        {
            Log.Warning("CameraDriverBase: 未找到 CameraComponent，驱动无法注册。");
            return;
        }

        _cameraComponent.RegisterDriver(this);
        _isRegistered = true;
    }

    /// <summary>
    /// 尝试从相机模块注销驱动。
    /// </summary>
    private void TryUnregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        if (_cameraComponent != null)
        {
            _cameraComponent.UnregisterDriver(this);
        }

        _isRegistered = false;
    }
}