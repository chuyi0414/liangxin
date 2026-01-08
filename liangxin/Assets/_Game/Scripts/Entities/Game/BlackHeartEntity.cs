// 引用 CYFramework 命名空间，使用 CY 入口
using CYFramework; // CY 入口引用
// 引用实体系统命名空间，使用 EntityBase
using CYFramework.Core.Entity; // 实体系统类型引用
// 引用 System.Collections.Generic 命名空间，使用 Dictionary
using System.Collections.Generic; // 泛型字典类型引用
// 引用 PrimeTween 命名空间，使用 Tween/Ease
using PrimeTween; // PrimeTween 类型引用
// 引用 UnityEngine 命名空间，使用 SerializeField/Collider2D
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 黑心实体
/// </summary>
[RequireComponent(typeof(Collider2D))] // 约束必须挂载 2D 碰撞体
[EntityPrefab("Prefabs/Entities/Game/BlackHeartEntity", "BlackHeartEntity", "Items")] // 绑定实体预制体信息
public sealed class BlackHeartEntity : EntityBase // 黑心实体定义
{
    /// <summary>碰撞体到黑心实体的映射表。</summary>
    private static readonly Dictionary<Collider2D, BlackHeartEntity> ColliderEntityMap = new Dictionary<Collider2D, BlackHeartEntity>(64); // 碰撞体到实体映射
    /// <summary>黑心数量（固定为 1）。</summary>
    [SerializeField] private int _amount = 1; // 黑心数量
    /// <summary>是否已被拾取。</summary>
    private bool _picked; // 拾取标记
    /// <summary>待结算的拾取数量（用于在回收时才入账）。</summary>
    private int _pendingPickupAmount; // 待结算拾取数量
    /// <summary>是否需要在回收时结算拾取（用于控制“到达后再加黑心”）。</summary>
    private bool _commitPickupOnRecycle; // 回收结算标记
    /// <summary>拾取移动时长（秒）。</summary>
    [SerializeField] private float _pickupMoveDuration = 0.75f; // 拾取移动时长
    /// <summary>拾取移动“到达阈值”（归一化进度的保底余量），用于保证在动画完成前绝不抵达目标点。</summary>
    [SerializeField] private float _pickupArriveEpsilon = 0.02f; // 到达阈值（0.02 表示最多只走到 98%）
    /// <summary>拾取移动“先慢后快”加速指数（>=1；2 表示二次加速）。</summary>
    [SerializeField] private float _pickupEaseInExponent = 2f; // 拾取加速指数（默认 2）
    /// <summary>拾取移动 Tween 句柄。</summary>
    private Tween _pickupTween; // 拾取 Tween 句柄
    /// <summary>掉落抛物线 Tween 句柄。</summary>
    private Tween _dropTween; // 掉落 Tween 句柄
    /// <summary>拾取目标 Transform。</summary>
    private Transform _pickupTarget; // 拾取目标 Transform
    /// <summary>拾取起点位置（世界坐标）。</summary>
    private Vector3 _pickupStartPosition; // 拾取起点位置
    /// <summary>拾取目标位置缓存（世界坐标）。</summary>
    private Vector3 _pickupTargetPosition; // 拾取目标位置缓存
    /// <summary>拾取“完成前最大进度”（严格小于 1）。</summary>
    private float _pickupMaxProgressBeforeComplete; // 拾取完成前最大进度缓存
    /// <summary>拾取“先慢后快”加速指数运行时值（已做上下限保护）。</summary>
    private float _pickupEaseInExponentRuntime; // 拾取加速指数运行时缓存
    /// <summary>掉落抛物线起点位置（世界坐标）。</summary>
    private Vector3 _dropStartPosition; // 掉落起点位置
    /// <summary>掉落抛物线终点位置（世界坐标）。</summary>
    private Vector3 _dropEndPosition; // 掉落终点位置
    /// <summary>掉落抛物线高度（Y 轴峰值高度）。</summary>
    private float _dropArcHeight; // 掉落抛物线高度缓存
    /// <summary>掉落抛物线运动时长（秒）。</summary>
    private float _dropDuration; // 掉落运动时长缓存
    /// <summary>是否存在待启动的掉落抛物线动画（用于对象尚未激活时延迟启动）。</summary>
    private bool _hasPendingDrop; // 待启动掉落动画标记
    /// <summary>2D 碰撞体缓存（用于点击拾取映射）。</summary>
    private Collider2D _cachedCollider2D; // 2D 碰撞体缓存

    /// <summary>黑心数量（只读）。</summary>
    public int Amount => _amount; // 对外只读数量

    /// <summary>
    /// 根据碰撞体尝试获取黑心实体。
    /// </summary>
    /// <param name="collider">碰撞体。</param>
    /// <param name="entity">输出黑心实体。</param>
    /// <returns>是否获取成功。</returns>
    public static bool TryGetEntityByCollider(Collider2D collider, out BlackHeartEntity entity) // 碰撞体查询入口
    {
        if (collider == null)
        {
            entity = null; // 碰撞体为空时输出空引用
            return false; // 碰撞体为空时返回失败
        }

        return ColliderEntityMap.TryGetValue(collider, out entity); // 从映射表查询实体
    }

    /// <summary>
    /// 实体初始化：缓存必要组件。
    /// </summary>
    /// <param name="userData">初始化传入的数据。</param>
    protected override void OnEntityInit(object userData) // 实体初始化入口
    {
        base.OnEntityInit(userData); // 调用父类初始化
        if (_cachedCollider2D == null)
        {
            _cachedCollider2D = GetComponent<Collider2D>(); // 缓存 2D 碰撞体
        }

        if (_cachedCollider2D != null)
        {
            ColliderEntityMap[_cachedCollider2D] = this; // 注册碰撞体映射
        }
    }

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

        if (_dropTween.isAlive)
        {
            _dropTween.Stop(); // 停止残留掉落动画
        }

        base.OnEntityShow(userData); // 调用父类显示
        _picked = false; // 重置拾取标记
        _pendingPickupAmount = 0; // 重置待结算拾取数量
        _commitPickupOnRecycle = false; // 重置回收结算标记
        _pickupTarget = null; // 清理拾取目标
        _pickupStartPosition = transform.position; // 重置拾取起点
        _pickupTargetPosition = _pickupStartPosition; // 重置拾取目标位置
        _pickupMaxProgressBeforeComplete = 0.9999f; // 重置拾取最大进度缓存（默认接近 1）
        _pickupEaseInExponentRuntime = 2f; // 重置拾取加速指数运行时缓存（默认 2）
        if (!_hasPendingDrop)
        {
            _dropStartPosition = _pickupStartPosition; // 重置掉落起点缓存
            _dropEndPosition = _pickupStartPosition; // 重置掉落终点缓存
            _dropArcHeight = 0f; // 重置掉落高度缓存
            _dropDuration = 0f; // 重置掉落时长缓存
        }

        if (_amount <= 0)
        {
            _amount = 1; // 修正数量下限
        }

        if (_hasPendingDrop)
        {
            _hasPendingDrop = false; // 清除待启动标记，避免重复启动
            StartDropTweenFromCache(); // 在实体激活后启动掉落抛物线动画，避免 PrimeTween 对禁用对象报警
        }
    }

    /// <summary>
    /// 尝试拾取黑心。
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

        _pendingPickupAmount = finalAmount; // 写入待结算拾取数量（回收时才入账）
        _commitPickupOnRecycle = false; // 等待拾取动画开始后再允许回收结算，避免误回收触发入账
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

        if (_dropTween.isAlive)
        {
            _dropTween.Stop(); // 拾取开始时停止掉落动画，避免两个 Tween 同时驱动位置
        }

        if (_pendingPickupAmount > 0)
        {
            _commitPickupOnRecycle = true; // 标记在回收时结算拾取数量，实现“动画结束才入账”
        }

        _hasPendingDrop = false; // 拾取开始时取消待启动掉落动画，避免激活时误触发掉落 Tween
        _dropDuration = 0f; // 清空掉落时长缓存

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

        var arriveEpsilon = _pickupArriveEpsilon; // 读取“到达阈值”
        if (arriveEpsilon <= 0f) // 阈值非法时进行兜底
        {
            arriveEpsilon = 0.0001f; // 阈值非法时使用最小正值兜底，确保完成前不会到达目标点
        }
        else if (arriveEpsilon >= 1f) // 阈值过大时进行上限保护
        {
            arriveEpsilon = 0.999f; // 阈值过大时进行上限钳制，避免最大进度变为负数
        }

        _pickupMaxProgressBeforeComplete = 1f - arriveEpsilon; // 写入“完成前允许到达的最大进度”（严格小于 1）

        var exponent = _pickupEaseInExponent; // 读取拾取加速指数
        if (exponent < 1f) // 指数非法时进行下限保护
        {
            exponent = 1f; // 指数下限为 1（线性）
        }

        _pickupEaseInExponentRuntime = exponent; // 写入拾取加速指数运行时值（避免闭包捕获）
        _pickupTween = Tween.Custom<BlackHeartEntity>(this, 0f, 1f, duration, (self, t) => // 创建自定义拾取动画
        {
            var targetTransform = self._pickupTarget; // 获取当前拾取目标
            if (targetTransform != null)
            {
                var latestTargetPosition = targetTransform.position; // 读取最新目标位置
                self._pickupTargetPosition = new Vector3(latestTargetPosition.x, latestTargetPosition.y, self._pickupStartPosition.z); // 更新目标位置并保持 Z
            }

            var rawProgress = Mathf.Clamp01(t); // 获取线性归一化进度（0~1）
            var easedProgress = Mathf.Pow(rawProgress, self._pickupEaseInExponentRuntime) * self._pickupMaxProgressBeforeComplete; // 将线性进度映射为“先慢后快”并缩放到“完成前最大进度”，从而保证完成前绝不到达目标点

            var nextPosition = Vector3.Lerp(self._pickupStartPosition, self._pickupTargetPosition, easedProgress); // 计算插值位置（先慢后快，且完成前永不到达目标点）
            self.transform.position = nextPosition; // 更新黑心位置
        }, Ease.Linear) // 使用线性时间轴，确保持续时间固定且由指数控制速度曲线
            .OnComplete(this, self => // 动画结束回收实体（结束时才允许到达目标点）
            {
                var targetTransform = self._pickupTarget; // 获取当前拾取目标
                if (targetTransform != null)
                {
                    var latestTargetPosition = targetTransform.position; // 读取最新目标位置
                    self._pickupTargetPosition = new Vector3(latestTargetPosition.x, latestTargetPosition.y, self._pickupStartPosition.z); // 更新目标位置并保持 Z
                    self.transform.position = self._pickupTargetPosition; // 在完成回调中强制对齐到目标点，确保“到达”只发生在结束时刻
                }

                CY.Entity.RecycleEntity(self); // 回收实体
            });
    }

    /// <summary>
    /// 播放掉落抛物线动画（XY 平面，Y 轴为高度峰值）。
    /// </summary>
    /// <param name="startPosition">抛物线起点（世界坐标）。</param>
    /// <param name="endPosition">抛物线终点（世界坐标）。</param>
    /// <param name="duration">运动时长（秒）。</param>
    /// <param name="arcHeight">抛物线高度（Y 轴峰值高度）。</param>
    public void PlayDropParabola(Vector3 startPosition, Vector3 endPosition, float duration, float arcHeight) // 掉落抛物线入口
    {
        if (_dropTween.isAlive)
        {
            _dropTween.Stop(); // 停止已有掉落动画
        }

        if (_pickupTween.isAlive)
        {
            _pickupTween.Stop(); // 掉落开始时停止拾取动画，避免两个 Tween 同时驱动位置
        }

        if (duration <= 0f)
        {
            transform.position = endPosition; // 时长无效时直接设置到终点
            _hasPendingDrop = false; // 时长无效时取消待启动标记
            _dropDuration = 0f; // 清空掉落时长缓存
            return; // 直接退出
        }

        _dropStartPosition = startPosition; // 缓存掉落起点
        _dropEndPosition = endPosition; // 缓存掉落终点
        _dropArcHeight = arcHeight > 0f ? arcHeight : 0f; // 缓存掉落高度并做下限保护
        _dropDuration = duration; // 缓存掉落运动时长
        transform.position = startPosition; // 将位置设置到起点

        if (!gameObject.activeInHierarchy)
        {
            _hasPendingDrop = true; // 目标对象未激活时延迟启动，避免 PrimeTween 产生“禁用对象启动 Tween”的警告
            return; // 等待实体真正显示后再启动 Tween
        }

        _hasPendingDrop = false; // 目标对象已激活时直接启动掉落 Tween
        StartDropTweenFromCache(); // 启动掉落抛物线 Tween
    }

    /// <summary>
    /// 使用缓存参数启动掉落抛物线 Tween（要求对象已激活）。
    /// </summary>
    private void StartDropTweenFromCache() // 掉落 Tween 启动入口
    {
        if (_dropTween.isAlive)
        {
            _dropTween.Stop(); // 启动前停止已有掉落动画，避免重复驱动
        }

        var duration = _dropDuration; // 读取缓存掉落时长
        if (duration <= 0f)
        {
            transform.position = _dropEndPosition; // 时长无效时直接对齐终点
            return; // 直接退出
        }

        _dropTween = Tween.Custom<BlackHeartEntity>(this, 0f, 1f, duration, (self, t) => // 创建自定义掉落动画（避免闭包捕获）
        {
            var rawProgress = Mathf.Clamp01(t); // 获取线性归一化进度（0~1）
            var basePosition = Vector3.Lerp(self._dropStartPosition, self._dropEndPosition, rawProgress); // 计算基线插值位置
            var arc = Mathf.Sin(Mathf.PI * rawProgress) * self._dropArcHeight; // 计算抛物线高度（中点最高）
            basePosition.y += arc; // 将高度叠加到 Y 轴
            self.transform.position = basePosition; // 更新黑心位置
        }, Ease.Linear) // 使用线性时间轴保证持续时间固定
            .OnComplete(this, self => // 动画结束时对齐终点，避免浮点误差
            {
                self.transform.position = self._dropEndPosition; // 强制对齐终点位置
            });
    }

    /// <summary>
    /// 实体回收时停止拾取动画。
    /// </summary>
    protected override void OnEntityRecycle() // 实体回收入口
    {
        CommitPickupOnRecycleIfNeeded(); // 在回收阶段结算拾取数量并派发事件
        if (_pickupTween.isAlive)
        {
            _pickupTween.Stop(); // 回收时停止拾取动画
        }

        if (_dropTween.isAlive)
        {
            _dropTween.Stop(); // 回收时停止掉落动画
        }

        _hasPendingDrop = false; // 回收时清除待启动掉落标记
        _dropDuration = 0f; // 回收时清空掉落时长缓存
        if (_cachedCollider2D != null)
        {
            ColliderEntityMap.Remove(_cachedCollider2D); // 移除碰撞体映射
        }

        _pickupTarget = null; // 清理拾取目标
        base.OnEntityRecycle(); // 调用父类回收
    }

    /// <summary>
    /// 在回收阶段结算拾取数量（用于保证 UI 在动画回收时刷新）。
    /// </summary>
    private void CommitPickupOnRecycleIfNeeded() // 拾取回收结算入口
    {
        if (!_commitPickupOnRecycle)
        {
            return; // 未标记回收结算时直接退出
        }

        _commitPickupOnRecycle = false; // 先清除标记，避免重复回收导致二次入账
        var amount = _pendingPickupAmount; // 缓存待结算数量
        _pendingPickupAmount = 0; // 清空待结算数量
        if (amount <= 0)
        {
            return; // 数量无效时直接退出
        }

        var battleDataManager = CY.BattleDataManager; // 获取战斗数据管理器
        if (battleDataManager == null)
        {
            return; // 管理器为空时直接退出
        }

        battleDataManager.AddBlackHeart(amount); // 在回收阶段增加黑心并派发事件
    }
}
