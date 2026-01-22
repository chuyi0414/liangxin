using UnityEngine;

/// <summary>
/// 相机跟随驱动（将相机位置跟随目标）。
/// </summary>
public sealed class CameraFollowDriver : CameraDriverBase
{
    /// <summary>
    /// 跟随目标 Transform。
    /// </summary>
    [SerializeField]
    private Transform _target;

    /// <summary>
    /// 目标位置偏移量（可用于拉开距离）。
    /// </summary>
    [SerializeField]
    private Vector3 _offset = new Vector3(0f, 3f, -6f);

    /// <summary>
    /// 是否使用目标旋转来旋转偏移量。
    /// </summary>
    [SerializeField]
    private bool _useTargetRotation = false;

    /// <summary>
    /// 位置平滑时间（秒，<=0 表示立即跟随）。
    /// </summary>
    [SerializeField]
    private float _smoothTime = 0.1f;

    /// <summary>
    /// 平滑移动最大速度（单位/秒）。
    /// </summary>
    [SerializeField]
    private float _maxSpeed = 999f;

    /// <summary>
    /// SmoothDamp 速度缓存。
    /// </summary>
    private Vector3 _velocity;

    /// <summary>
    /// 驱动更新逻辑入口。
    /// </summary>
    /// <param name="deltaTime">按驱动时间模式计算后的时间增量（秒）。</param>
    /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
    /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
    protected override void OnDriverUpdate(float deltaTime, float elapseSeconds, float realElapseSeconds)
    {
        if (_target == null)
        {
            return;
        }

        Transform cameraTransform = TargetTransform;
        if (cameraTransform == null)
        {
            return;
        }

        Vector3 offset = _useTargetRotation ? _target.rotation * _offset : _offset;
        Vector3 desiredPosition = _target.position + offset;

        if (_smoothTime <= 0f || deltaTime <= 0f)
        {
            cameraTransform.position = desiredPosition;
            _velocity = Vector3.zero;
            return;
        }

        cameraTransform.position = Vector3.SmoothDamp(
            cameraTransform.position,
            desiredPosition,
            ref _velocity,
            _smoothTime,
            _maxSpeed,
            deltaTime);
    }
}