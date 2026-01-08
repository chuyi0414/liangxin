// 引用 CYFramework 命名空间，使用 CY 入口
using CYFramework; // CY 入口引用
// 引用实体系统命名空间，使用 EntityBase
using CYFramework.Core.Entity; // 实体系统类型引用
// 引用 PrimeTween 命名空间，使用 Tween/Ease
using PrimeTween; // PrimeTween 类型引用
// 引用 UnityEngine 命名空间，使用 SerializeField
using UnityEngine; // Unity 引擎类型引用
/// <summary>
/// 金币实体
/// </summary>
[EntityPrefab("Prefabs/Entities/Game/MoneyEntity", "MoneyEntity", "Items")] // 绑定实体预制体信息
public sealed class MoneyEntity : EntityBase // 金币实体定义
{
    /// <summary>金币数量（固定为 1）。</summary>
    [SerializeField] private int _amount = 1; // 金币数量
    /// <summary>是否已被拾取。</summary>
    private bool _picked; // 拾取标记
    /// <summary>拾取移动时长（秒）。</summary>
    [SerializeField] private float _pickupMoveDuration = 0.75f; // 拾取移动时长
    /// <summary>拾取移动缓动类型。</summary>
    [SerializeField] private Ease _pickupMoveEase = Ease.OutElastic; // 拾取移动缓动
    /// <summary>拾取移动 Tween 句柄。</summary>
    private Tween _pickupTween; // 拾取 Tween 句柄
    /// <summary>拾取目标 Transform。</summary>
    private Transform _pickupTarget; // 拾取目标 Transform
    /// <summary>拾取起点位置（世界坐标）。</summary>
    private Vector3 _pickupStartPosition; // 拾取起点位置
    /// <summary>拾取目标位置缓存（世界坐标）。</summary>
    private Vector3 _pickupTargetPosition; // 拾取目标位置缓存

    /// <summary>金币数量（只读）。</summary>
    public int Amount => _amount; // 对外只读数量

    /// <summary>
    /// 实体显示时重置拾取状态。
    /// </summary>
    /// <param name="userData">显示时传入的数据。</param>
    protected override void OnEntityShow(object userData) // 实体显示入口
    {
        if (_pickupTween.isAlive)
        {
            _pickupTween.Stop(); // 停止残留拾取动画
        }

        base.OnEntityShow(userData); // 调用父类显示
        _picked = false; // 重置拾取标记
        _pickupTarget = null; // 清理拾取目标
        _pickupStartPosition = transform.position; // 重置拾取起点
        _pickupTargetPosition = _pickupStartPosition; // 重置拾取目标位置
        if (_amount <= 0)
        {
            _amount = 1; // 修正数量下限
        }
    }

    /// <summary>
    /// 尝试拾取金币。
    /// </summary>
    /// <param name="amount">输出拾取数量。</param>
    /// <returns>是否拾取成功。</returns>
    public bool TryPickup(out int amount) // 拾取入口
    {
        amount = 0; // 默认输出为 0
        if (_picked)
        {
            return false; // 已拾取时返回失败
        }

        _picked = true; // 标记已拾取
        var finalAmount = _amount; // 读取最终数量
        if (finalAmount <= 0)
        {
            finalAmount = 1; // 修正最小数量
        }

        amount = finalAmount; // 输出拾取数量
        return true; // 返回拾取成功
    }

    /// <summary>
    /// 播放拾取动画并在完成后回收实体。
    /// </summary>
    /// <param name="target">拾取目标 Transform（玩家）。</param>
    public void PlayPickupToTarget(Transform target) // 拾取动画入口
    {
        if (_pickupTween.isAlive)
        {
            _pickupTween.Stop(); // 停止已有拾取动画
        }

        if (target == null)
        {
            CY.Entity.RecycleEntity(this); // 目标为空时直接回收
            return; // 直接退出
        }

        _pickupTarget = target; // 缓存拾取目标
        _pickupStartPosition = transform.position; // 记录拾取起点
        var targetPosition = target.position; // 读取目标位置
        _pickupTargetPosition = new Vector3(targetPosition.x, targetPosition.y, _pickupStartPosition.z); // 缓存目标位置并保持 Z
        var duration = _pickupMoveDuration; // 缓存移动时长
        if (duration <= 0f)
        {
            CY.Entity.RecycleEntity(this); // 时长无效时直接回收
            return; // 直接退出
        }

        _pickupTween = Tween.Custom<MoneyEntity>(this, 0f, 1f, duration, (self, t) => // 创建自定义拾取动画
        {
            var targetTransform = self._pickupTarget; // 获取当前拾取目标
            if (targetTransform != null)
            {
                var latestTargetPosition = targetTransform.position; // 读取最新目标位置
                self._pickupTargetPosition = new Vector3(latestTargetPosition.x, latestTargetPosition.y, self._pickupStartPosition.z); // 更新目标位置并保持 Z
            }

            var nextPosition = Vector3.LerpUnclamped(self._pickupStartPosition, self._pickupTargetPosition, t); // 计算插值位置（支持超调）
            self.transform.position = nextPosition; // 更新金币位置
        }, _pickupMoveEase) // 指定缓动类型
            .OnComplete(this, self => CY.Entity.RecycleEntity(self)); // 动画结束回收实体
    }

    /// <summary>
    /// 实体回收时停止拾取动画。
    /// </summary>
    protected override void OnEntityRecycle() // 实体回收入口
    {
        if (_pickupTween.isAlive)
        {
            _pickupTween.Stop(); // 回收时停止拾取动画
        }

        _pickupTarget = null; // 清理拾取目标
        base.OnEntityRecycle(); // 调用父类回收
    }
}
