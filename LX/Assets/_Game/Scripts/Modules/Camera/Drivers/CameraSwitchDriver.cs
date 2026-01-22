#if PRIME_TWEEN_INSTALLED
using PrimeTween;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 相机切换驱动（基于 PrimeTween 进行平滑过渡）。
/// 建议使用独立的过渡相机作为本组件所在相机。
/// </summary>
public sealed class CameraSwitchDriver : CameraDriverBase
{
    /// <summary>
    /// 当前激活相机引用（为空则尝试自动检测）。
    /// </summary>
    [SerializeField]
    private Camera _currentCamera;

    /// <summary>
    /// 切换过渡时长（秒）。
    /// </summary>
    [SerializeField]
    private float _duration = 0.5f;

    /// <summary>
    /// 切换过渡缓动类型。
    /// </summary>
    [SerializeField]
    private Ease _ease = Ease.InOutCubic;

    /// <summary>
    /// 是否自动检测当前激活相机。
    /// </summary>
    [SerializeField]
    private bool _autoDetectCurrentCamera = true;

    /// <summary>
    /// 是否在过渡期间禁用目标相机，避免双相机同时渲染。
    /// </summary>
    [SerializeField]
    private bool _disableTargetCameraDuringTransition = true;

    /// <summary>
    /// 当前切换序列。
    /// </summary>
    private Sequence _transitionSequence;

    /// <summary>
    /// 是否正在切换。
    /// </summary>
    private bool _isSwitching;

    /// <summary>
    /// 是否正在切换。
    /// </summary>
    public bool IsSwitching => _isSwitching;

    /// <summary>
    /// 获取当前激活相机。
    /// </summary>
    public Camera CurrentCamera => _currentCamera;

    /// <summary>
    /// 手动设置当前激活相机。
    /// </summary>
    /// <param name="camera">当前相机。</param>
    public void SetCurrentCamera(Camera camera)
    {
        _currentCamera = camera;
    }

    /// <summary>
    /// 切换到目标相机（使用默认时长和缓动）。
    /// </summary>
    /// <param name="targetCamera">目标相机。</param>
    public void SwitchTo(Camera targetCamera)
    {
        SwitchTo(targetCamera, _duration, _ease);
    }

    /// <summary>
    /// 切换到目标相机。
    /// </summary>
    /// <param name="targetCamera">目标相机。</param>
    /// <param name="duration">过渡时长（秒）。</param>
    /// <param name="ease">缓动类型。</param>
    public void SwitchTo(Camera targetCamera, float duration, Ease ease)
    {
        if (targetCamera == null)
        {
            return;
        }

        EnsureCamera();
        Camera transitionCamera = TargetCamera;
        if (transitionCamera == null)
        {
            Log.Warning("CameraSwitchDriver: 过渡相机为空，无法切换。");
            return;
        }

        Camera fromCamera = ResolveCurrentCamera(transitionCamera);
        if (fromCamera == null)
        {
            targetCamera.enabled = true;
            if (transitionCamera != targetCamera)
            {
                transitionCamera.enabled = false;
            }

            _currentCamera = targetCamera;
            return;
        }

        if (fromCamera == targetCamera)
        {
            _currentCamera = targetCamera;
            return;
        }

        StopTransition(false);

        if (_disableTargetCameraDuringTransition && targetCamera.enabled)
        {
            targetCamera.enabled = false;
        }

        CopyCameraState(fromCamera, transitionCamera);

        if (transitionCamera != fromCamera)
        {
            transitionCamera.enabled = true;
        }

        if (fromCamera != transitionCamera)
        {
            fromCamera.enabled = false;
        }

        bool sameProjection = fromCamera.orthographic == targetCamera.orthographic;
        if (!sameProjection)
        {
            transitionCamera.orthographic = targetCamera.orthographic;
            if (transitionCamera.orthographic)
            {
                transitionCamera.orthographicSize = targetCamera.orthographicSize;
            }
            else
            {
                transitionCamera.fieldOfView = targetCamera.fieldOfView;
            }
        }

        float time = Mathf.Max(0f, duration);
        if (time <= 0f || transitionCamera == targetCamera)
        {
            CompleteSwitch(targetCamera, transitionCamera);
            return;
        }

        _isSwitching = true;

        Sequence sequence = Sequence.Create(useUnscaledTime: UseUnscaledTime);
        sequence = sequence.Group(Tween.Position(
            transitionCamera.transform,
            targetCamera.transform.position,
            time,
            ease,
            useUnscaledTime: UseUnscaledTime));
        sequence = sequence.Group(Tween.Rotation(
            transitionCamera.transform,
            targetCamera.transform.rotation,
            time,
            ease,
            useUnscaledTime: UseUnscaledTime));

        if (sameProjection)
        {
            if (targetCamera.orthographic)
            {
                sequence = sequence.Group(Tween.CameraOrthographicSize(
                    transitionCamera,
                    targetCamera.orthographicSize,
                    time,
                    ease,
                    useUnscaledTime: UseUnscaledTime));
            }
            else
            {
                sequence = sequence.Group(Tween.CameraFieldOfView(
                    transitionCamera,
                    targetCamera.fieldOfView,
                    time,
                    ease,
                    useUnscaledTime: UseUnscaledTime));
            }
        }

        sequence = sequence.ChainCallback(() => CompleteSwitch(targetCamera, transitionCamera));
        _transitionSequence = sequence;
    }

    /// <summary>
    /// 驱动更新逻辑入口（切换驱动由 PrimeTween 自行推进）。
    /// </summary>
    /// <param name="deltaTime">按驱动时间模式计算后的时间增量（秒）。</param>
    /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
    /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
    protected override void OnDriverUpdate(float deltaTime, float elapseSeconds, float realElapseSeconds)
    {
    }

    /// <summary>
    /// 派生类禁用钩子（停止切换并清理状态）。
    /// </summary>
    protected override void OnDriverDisable()
    {
        StopTransition(true);
    }

    /// <summary>
    /// 根据设置解析当前激活相机。
    /// </summary>
    /// <param name="transitionCamera">过渡相机。</param>
    /// <returns>当前相机。</returns>
    private Camera ResolveCurrentCamera(Camera transitionCamera)
    {
        if (_currentCamera != null)
        {
            return _currentCamera;
        }

        if (!_autoDetectCurrentCamera)
        {
            return null;
        }

        Camera main = Camera.main;
        if (main != null)
        {
            return main;
        }

        if (transitionCamera != null && transitionCamera.enabled)
        {
            return transitionCamera;
        }

        int count = Camera.allCamerasCount;
        if (count <= 0)
        {
            return null;
        }

        Camera[] cameras = new Camera[count];
        Camera.GetAllCameras(cameras);
        return cameras.Length > 0 ? cameras[0] : null;
    }

    /// <summary>
    /// 复制相机核心状态到过渡相机。
    /// </summary>
    /// <param name="source">来源相机。</param>
    /// <param name="destination">目标相机。</param>
    private static void CopyCameraState(Camera source, Camera destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        Transform sourceTransform = source.transform;
        Transform destinationTransform = destination.transform;
        destinationTransform.position = sourceTransform.position;
        destinationTransform.rotation = sourceTransform.rotation;

        destination.orthographic = source.orthographic;
        destination.fieldOfView = source.fieldOfView;
        destination.orthographicSize = source.orthographicSize;
        destination.nearClipPlane = source.nearClipPlane;
        destination.farClipPlane = source.farClipPlane;
    }

    /// <summary>
    /// 完成切换并切换相机启用状态。
    /// </summary>
    /// <param name="targetCamera">目标相机。</param>
    /// <param name="transitionCamera">过渡相机。</param>
    private void CompleteSwitch(Camera targetCamera, Camera transitionCamera)
    {
        if (transitionCamera != null && transitionCamera != targetCamera)
        {
            transitionCamera.enabled = false;
        }

        if (targetCamera != null)
        {
            targetCamera.enabled = true;
        }

        _currentCamera = targetCamera;
        _isSwitching = false;
    }

    /// <summary>
    /// 停止当前切换序列。
    /// </summary>
    /// <param name="disableTransitionCamera">是否关闭过渡相机。</param>
    private void StopTransition(bool disableTransitionCamera)
    {
        if (_transitionSequence.isAlive)
        {
            _transitionSequence.Stop();
        }

        _transitionSequence = default;
        _isSwitching = false;

        if (disableTransitionCamera)
        {
            Camera transitionCamera = TargetCamera;
            if (transitionCamera != null && transitionCamera != _currentCamera)
            {
                transitionCamera.enabled = false;
            }
        }
    }
}
#else
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 相机切换驱动占位实现（未安装 PrimeTween 时使用）。
/// </summary>
public sealed class CameraSwitchDriver : CameraDriverBase
{
    /// <summary>
    /// 切换过渡时长（秒）。
    /// </summary>
    [SerializeField]
    private float _duration = 0.5f;

    /// <summary>
    /// 切换到目标相机（PrimeTween 未安装时仅提示）。
    /// </summary>
    /// <param name="targetCamera">目标相机。</param>
    public void SwitchTo(Camera targetCamera)
    {
        Log.Warning("CameraSwitchDriver: 未安装 PrimeTween，无法进行平滑切换。");
        if (targetCamera != null)
        {
            targetCamera.enabled = true;
        }
    }

    /// <summary>
    /// 切换到目标相机（PrimeTween 未安装时仅提示）。
    /// </summary>
    /// <param name="targetCamera">目标相机。</param>
    /// <param name="duration">过渡时长（秒）。</param>
    public void SwitchTo(Camera targetCamera, float duration)
    {
        _duration = duration;
        SwitchTo(targetCamera);
    }

    /// <summary>
    /// 驱动更新逻辑入口（占位实现不执行更新）。
    /// </summary>
    /// <param name="deltaTime">按驱动时间模式计算后的时间增量（秒）。</param>
    /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
    /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
    protected override void OnDriverUpdate(float deltaTime, float elapseSeconds, float realElapseSeconds)
    {
    }
}
#endif