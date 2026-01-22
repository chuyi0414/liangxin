using UnityEngine;

/// <summary>
/// 相机摇晃驱动（基于噪声的平滑抖动）。
/// </summary>
public sealed class CameraShakeDriver : CameraDriverBase
{
    /// <summary>
    /// 抖动作用的目标 Transform（为空则使用相机自身）。
    /// 建议使用相机的子物体以避免与跟随产生相互干扰。
    /// </summary>
    [SerializeField]
    private Transform _shakeTarget;

    /// <summary>
    /// 默认抖动持续时间（秒）。
    /// </summary>
    [SerializeField]
    private float _defaultDuration = 0.25f;

    /// <summary>
    /// 默认抖动强度（0~1）。
    /// </summary>
    [SerializeField]
    private float _defaultStrength = 0.3f;

    /// <summary>
    /// 抖动频率（噪声变化速度）。
    /// </summary>
    [SerializeField]
    private float _frequency = 25f;

    /// <summary>
    /// 位置抖动幅度（各轴权重）。
    /// </summary>
    [SerializeField]
    private Vector3 _positionAmplitude = Vector3.one;

    /// <summary>
    /// 旋转抖动幅度（角度，单位度）。
    /// </summary>
    [SerializeField]
    private Vector3 _rotationAmplitude = new Vector3(2f, 2f, 2f);

    /// <summary>
    /// 当前剩余抖动时间（秒）。
    /// </summary>
    private float _remainingTime;

    /// <summary>
    /// 当前抖动总时长（秒）。
    /// </summary>
    private float _duration;

    /// <summary>
    /// 当前抖动强度。
    /// </summary>
    private float _strength;

    /// <summary>
    /// 噪声时间累加器。
    /// </summary>
    private float _noiseTime;

    /// <summary>
    /// 抖动开始时的本地位置。
    /// </summary>
    private Vector3 _originLocalPosition;

    /// <summary>
    /// 抖动开始时的本地旋转。
    /// </summary>
    private Quaternion _originLocalRotation;

    /// <summary>
    /// 是否已缓存抖动起始状态。
    /// </summary>
    private bool _hasOrigin;

    /// <summary>
    /// 触发一次默认抖动。
    /// </summary>
    public void Shake()
    {
        Shake(_defaultDuration, _defaultStrength);
    }

    /// <summary>
    /// 触发一次抖动。
    /// </summary>
    /// <param name="duration">抖动持续时间（秒）。</param>
    /// <param name="strength">抖动强度（0~1）。</param>
    public void Shake(float duration, float strength)
    {
        if (duration <= 0f || strength <= 0f)
        {
            return;
        }

        _duration = duration;
        _remainingTime = duration;
        _strength = strength;
        _noiseTime = 0f;
        CacheOrigin();
    }

    /// <summary>
    /// 停止抖动并可选恢复原位。
    /// </summary>
    /// <param name="restore">是否恢复到抖动前状态。</param>
    public void StopShake(bool restore = true)
    {
        _remainingTime = 0f;
        if (restore)
        {
            RestoreOrigin();
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
        if (_remainingTime <= 0f)
        {
            if (_hasOrigin)
            {
                RestoreOrigin();
            }

            return;
        }

        Transform target = ResolveShakeTarget();
        if (target == null)
        {
            return;
        }

        if (!_hasOrigin)
        {
            CacheOrigin();
        }

        _remainingTime -= deltaTime;
        if (_remainingTime < 0f)
        {
            _remainingTime = 0f;
        }

        _noiseTime += deltaTime * _frequency;
        float attenuate = _duration > 0f ? Mathf.Clamp01(_remainingTime / _duration) : 0f;
        float strength = _strength * attenuate;

        Vector3 positionNoise = new Vector3(
            SampleNoise(_noiseTime, 0f),
            SampleNoise(0f, _noiseTime),
            SampleNoise(_noiseTime, _noiseTime));
        Vector3 rotationNoise = new Vector3(
            SampleNoise(_noiseTime + 10f, 1f),
            SampleNoise(_noiseTime + 20f, 2f),
            SampleNoise(_noiseTime + 30f, 3f));

        Vector3 positionOffset = Vector3.Scale(positionNoise, _positionAmplitude) * strength;
        Vector3 rotationOffset = Vector3.Scale(rotationNoise, _rotationAmplitude) * strength;

        target.localPosition = _originLocalPosition + positionOffset;
        target.localRotation = _originLocalRotation * Quaternion.Euler(rotationOffset);
    }

    /// <summary>
    /// 派生类禁用钩子（禁用时恢复原位）。
    /// </summary>
    protected override void OnDriverDisable()
    {
        RestoreOrigin();
    }

    /// <summary>
    /// 获取实际抖动目标。
    /// </summary>
    /// <returns>抖动目标 Transform。</returns>
    private Transform ResolveShakeTarget()
    {
        if (_shakeTarget != null)
        {
            return _shakeTarget;
        }

        return TargetTransform;
    }

    /// <summary>
    /// 缓存抖动起始状态。
    /// </summary>
    private void CacheOrigin()
    {
        Transform target = ResolveShakeTarget();
        if (target == null)
        {
            return;
        }

        _originLocalPosition = target.localPosition;
        _originLocalRotation = target.localRotation;
        _hasOrigin = true;
    }

    /// <summary>
    /// 恢复抖动前状态。
    /// </summary>
    private void RestoreOrigin()
    {
        if (!_hasOrigin)
        {
            return;
        }

        Transform target = ResolveShakeTarget();
        if (target != null)
        {
            target.localPosition = _originLocalPosition;
            target.localRotation = _originLocalRotation;
        }

        _hasOrigin = false;
    }

    /// <summary>
    /// 采样 Perlin 噪声并映射到 -1~1。
    /// </summary>
    /// <param name="x">噪声 X 坐标。</param>
    /// <param name="y">噪声 Y 坐标。</param>
    /// <returns>归一化噪声值。</returns>
    private static float SampleNoise(float x, float y)
    {
        return Mathf.PerlinNoise(x, y) * 2f - 1f;
    }
}