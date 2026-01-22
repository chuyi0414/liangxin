using UnityEngine;

/// <summary>
/// 相机缩放驱动（自动适配正交/透视模式）。
/// </summary>
public sealed class CameraZoomDriver : CameraDriverBase
{
    /// <summary>
    /// 目标缩放值（正交时为 orthographicSize，透视时为 fieldOfView）。
    /// </summary>
    [SerializeField]
    private float _targetZoom = -1f;

    /// <summary>
    /// 缩放最小值。
    /// </summary>
    [SerializeField]
    private float _minZoom = 1f;

    /// <summary>
    /// 缩放最大值。
    /// </summary>
    [SerializeField]
    private float _maxZoom = 60f;

    /// <summary>
    /// 缩放平滑时间（秒，<=0 表示立即生效）。
    /// </summary>
    [SerializeField]
    private float _smoothTime = 0.2f;

    /// <summary>
    /// SmoothDamp 缩放速度缓存。
    /// </summary>
    private float _zoomVelocity;

    /// <summary>
    /// 是否已初始化目标缩放值。
    /// </summary>
    private bool _initialized;

    /// <summary>
    /// 设置目标缩放值。
    /// </summary>
    /// <param name="zoom">目标缩放值。</param>
    /// <param name="instant">是否立即生效。</param>
    public void SetTargetZoom(float zoom, bool instant = false)
    {
        _targetZoom = zoom;

        if (instant)
        {
            ApplyZoom(zoom, true);
        }
    }

    /// <summary>
    /// 驱动更新逻辑入口。
    /// </summary>
    /// <param name="deltaTime">按驱动时间模式计算后的时间增量（秒）。</param>
    /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
    /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
    protected override void OnDriverUpdate(float deltaTime, float elapseSeconds, float realElapseSeconds)
    {
        Camera camera = TargetCamera;
        if (camera == null)
        {
            return;
        }

        float currentZoom = GetCameraZoom(camera);
        if (!_initialized)
        {
            if (_targetZoom <= 0f)
            {
                _targetZoom = currentZoom;
            }

            _initialized = true;
        }

        float targetZoom = ClampZoom(_targetZoom);
        if (_smoothTime <= 0f || deltaTime <= 0f)
        {
            SetCameraZoom(camera, targetZoom);
            _zoomVelocity = 0f;
            return;
        }

        float newZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref _zoomVelocity, _smoothTime, Mathf.Infinity, deltaTime);
        SetCameraZoom(camera, newZoom);
    }

    /// <summary>
    /// 立即应用缩放并同步目标值。
    /// </summary>
    /// <param name="zoom">目标缩放值。</param>
    /// <param name="syncTarget">是否同步目标值。</param>
    private void ApplyZoom(float zoom, bool syncTarget)
    {
        Camera camera = TargetCamera;
        if (camera == null)
        {
            return;
        }

        float value = ClampZoom(zoom);
        SetCameraZoom(camera, value);
        _zoomVelocity = 0f;

        if (syncTarget)
        {
            _targetZoom = value;
        }
    }

    /// <summary>
    /// 获取相机当前缩放值。
    /// </summary>
    /// <param name="camera">相机对象。</param>
    /// <returns>缩放值。</returns>
    private static float GetCameraZoom(Camera camera)
    {
        return camera.orthographic ? camera.orthographicSize : camera.fieldOfView;
    }

    /// <summary>
    /// 设置相机缩放值。
    /// </summary>
    /// <param name="camera">相机对象。</param>
    /// <param name="value">缩放值。</param>
    private static void SetCameraZoom(Camera camera, float value)
    {
        if (camera.orthographic)
        {
            camera.orthographicSize = value;
        }
        else
        {
            camera.fieldOfView = value;
        }
    }

    /// <summary>
    /// 对缩放值进行范围裁剪。
    /// </summary>
    /// <param name="zoom">缩放值。</param>
    /// <returns>裁剪后的缩放值。</returns>
    private float ClampZoom(float zoom)
    {
        if (_minZoom > _maxZoom)
        {
            return zoom;
        }

        return Mathf.Clamp(zoom, _minZoom, _maxZoom);
    }
}