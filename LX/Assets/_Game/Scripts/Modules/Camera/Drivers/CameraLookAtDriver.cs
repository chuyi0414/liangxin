using UnityEngine;

/// <summary>
/// 相机看向驱动（将相机朝向目标）。
/// </summary>
public sealed class CameraLookAtDriver : CameraDriverBase
{
    /// <summary>
    /// 看向目标 Transform。
    /// </summary>
    [SerializeField]
    private Transform _target;

    /// <summary>
    /// 看向点的偏移量。
    /// </summary>
    [SerializeField]
    private Vector3 _offset = Vector3.zero;

    /// <summary>
    /// LookRotation 使用的上方向。
    /// </summary>
    [SerializeField]
    private Vector3 _up = Vector3.up;

    /// <summary>
    /// 旋转插值速度（每秒插值比例）。
    /// </summary>
    [SerializeField]
    private float _rotationLerpSpeed = 10f;

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

        Vector3 lookPosition = _target.position + _offset;
        Vector3 direction = lookPosition - cameraTransform.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction, _up);
        if (_rotationLerpSpeed <= 0f || deltaTime <= 0f)
        {
            cameraTransform.rotation = targetRotation;
            return;
        }

        float t = Mathf.Clamp01(_rotationLerpSpeed * deltaTime);
        cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, t);
    }
}