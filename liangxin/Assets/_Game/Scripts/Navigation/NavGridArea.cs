// 引用泛型集合命名空间，使用 List 容器
using System.Collections.Generic; // 泛型集合引用
// 引用 UnityEngine 命名空间，使用 MonoBehaviour/Vector/Mathf 等类型
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// A* 网格区域配置：定义网格边界、格子大小与静态障碍层。
/// 用于 GridPathfinder 和 HybridNavigationAgent 的区域查询。
/// </summary>
[DefaultExecutionOrder(-300)] // 确保比常规组件更早执行
public sealed class NavGridArea : MonoBehaviour // 网格区域组件
{
    /// <summary>全局区域表。</summary>
    private static readonly List<NavGridArea> ActiveAreas = new List<NavGridArea>(8); // 活动区域缓存

    /// <summary>区域编号（调试用）。</summary>
    [SerializeField] private int _areaId = 1; // 区域编号
    /// <summary>是否使用碰撞体边界作为区域大小。</summary>
    [SerializeField] private bool _useColliderBounds = true; // 是否读取 Collider2D 边界
    /// <summary>手动设置区域大小（当不使用碰撞体时生效）。</summary>
    [SerializeField] private Vector2 _manualSize = new Vector2(40f, 40f); // 手动区域尺寸
    /// <summary>格子大小（世界单位）。</summary>
    [SerializeField] private float _cellSize = 1f; // 网格格子大小
    /// <summary>静态障碍层遮罩。</summary>
    [SerializeField] private LayerMask _staticObstacleMask = ~0; // 静态障碍层

    /// <summary>边界碰撞体缓存。</summary>
    private Collider2D _boundsCollider; // 2D 边界碰撞体
    /// <summary>区域中心缓存。</summary>
    private Vector2 _cachedCenter; // 缓存区域中心
    /// <summary>区域半尺寸缓存。</summary>
    private Vector2 _cachedExtents; // 缓存区域半尺寸
    /// <summary>上一次世界位置缓存。</summary>
    private Vector3 _lastPosition; // 上一次位置
    /// <summary>上一次缩放缓存。</summary>
    private Vector3 _lastScale; // 上一次缩放

    /// <summary>区域编号（调试用）。</summary>
    public int AreaId => _areaId; // 读取区域编号
    /// <summary>格子大小（世界单位）。</summary>
    public float CellSize => _cellSize <= 0.01f ? 0.01f : _cellSize; // 读取格子大小并确保最小值
    /// <summary>区域中心。</summary>
    public Vector2 AreaCenter // 区域中心属性
    {
        get
        {
            CacheBounds(false); // 确保缓存有效
            return _cachedCenter; // 返回缓存中心
        }
    }
    /// <summary>区域半尺寸。</summary>
    public Vector2 AreaExtents // 区域半尺寸属性
    {
        get
        {
            CacheBounds(false); // 确保缓存有效
            return _cachedExtents; // 返回缓存半尺寸
        }
    }

    /// <summary>静态障碍层。</summary>
    public LayerMask StaticObstacleMask => _staticObstacleMask; // 读取静态障碍层

    /// <summary>
    /// 组件初始化：缓存边界碰撞体并刷新边界。
    /// </summary>
    private void Awake() // 生命周期：Awake
    {
        _boundsCollider = GetComponentInChildren<Collider2D>(); // 获取子层级碰撞体
        CacheBounds(true); // 强制刷新边界缓存
    }

    /// <summary>
    /// 启用时注册到全局区域表。
    /// </summary>
    private void OnEnable() // 生命周期：OnEnable
    {
        RegisterArea(); // 注册到全局区域
        CacheBounds(true); // 刷新边界缓存
    }

    /// <summary>
    /// 禁用时从全局区域表移除。
    /// </summary>
    private void OnDisable() // 生命周期：OnDisable
    {
        UnregisterArea(); // 注销区域
    }

    /// <summary>
    /// 编辑器属性变更时刷新缓存。
    /// </summary>
    private void OnValidate() // 生命周期：OnValidate
    {
        CacheBounds(true); // 强制刷新边界
    }

    /// <summary>
    /// 判断点是否在区域内。
    /// </summary>
    /// <param name="position">待检测坐标。</param>
    public bool Contains(Vector2 position) // 区域包含判断
    {
        return SqrDistanceTo(position) <= 0f; // 距离为 0 表示在区域内
    }

    /// <summary>
    /// 点到区域矩形的平方距离（区域内为 0）。
    /// </summary>
    /// <param name="position">待检测坐标。</param>
    public float SqrDistanceTo(Vector2 position) // 区域距离计算
    {
        CacheBounds(false); // 确保缓存有效
        var dx = Mathf.Abs(position.x - _cachedCenter.x) - _cachedExtents.x; // 计算 X 方向超出量
        var dy = Mathf.Abs(position.y - _cachedCenter.y) - _cachedExtents.y; // 计算 Y 方向超出量
        if (dx < 0f) dx = 0f;
        if (dy < 0f) dy = 0f;
        return dx * dx + dy * dy; // 返回矩形外距离平方
    }

    /// <summary>
    /// 根据位置获取所在区域（优先包含区域）。
    /// </summary>
    /// <param name="position">查询坐标。</param>
    /// <param name="area">输出区域。</param>
    public static bool TryGetByPosition(Vector2 position, out NavGridArea area) // 位置查询入口
    {
        for (int i = 0; i < ActiveAreas.Count; i++)
        {
            var current = ActiveAreas[i]; // 取当前区域
            if (current != null && current.Contains(position))
            {
                area = current; // 命中区域
                return true; // 返回成功
            }
        }

        area = null; // 未命中时清空输出
        return false; // 返回失败
    }

    /// <summary>
    /// 根据位置获取包含或最近区域。
    /// </summary>
    /// <param name="position">查询坐标。</param>
    /// <param name="area">输出区域。</param>
    public static bool TryGetByPositionOrNearest(Vector2 position, out NavGridArea area) // 最近区域查询入口
    {
        area = null; // 初始化输出
        var bestDist = float.MaxValue; // 最小距离缓存

        for (int i = 0; i < ActiveAreas.Count; i++)
        {
            var current = ActiveAreas[i]; // 取当前区域
            if (current == null)
            {
                continue; // 过滤空引用
            }

            var dist = current.SqrDistanceTo(position); // 计算距离平方
            if (dist <= 0f)
            {
                area = current; // 直接命中包含区域
                return true; // 直接返回成功
            }

            if (dist < bestDist)
            {
                bestDist = dist; // 更新最优距离
                area = current; // 更新最近区域
            }
        }

        return area != null; // 有候选区域则返回成功
    }

    /// <summary>
    /// 缓存区域边界。
    /// </summary>
    /// <param name="force">是否强制刷新。</param>
    private void CacheBounds(bool force) // 边界缓存入口
    {
        var position = transform.position; // 获取当前位置
        var scale = transform.lossyScale; // 获取当前缩放
        if (!force && position == _lastPosition && scale == _lastScale)
        {
            return; // 位置与缩放未变时不更新
        }

        _lastPosition = position; // 记录位置
        _lastScale = scale; // 记录缩放

        if (_useColliderBounds && _boundsCollider != null)
        {
            var bounds = _boundsCollider.bounds; // 读取碰撞体边界
            _cachedCenter = bounds.center; // 更新中心缓存
            _cachedExtents = bounds.extents; // 更新半尺寸缓存
        }
        else
        {
            _cachedCenter = position; // 使用 Transform 位置作为中心
            _cachedExtents = _manualSize * 0.5f; // 使用手动尺寸作为半尺寸
        }
    }

    /// <summary>
    /// 注册区域到全局列表。
    /// </summary>
    private void RegisterArea() // 注册入口
    {
        for (int i = 0; i < ActiveAreas.Count; i++)
        {
            if (ActiveAreas[i] == this)
            {
                return; // 已注册时直接返回
            }
        }

        ActiveAreas.Add(this); // 加入全局列表
    }

    /// <summary>
    /// 从全局列表注销区域。
    /// </summary>
    private void UnregisterArea() // 注销入口
    {
        for (int i = 0; i < ActiveAreas.Count; i++)
        {
            if (ActiveAreas[i] == this)
            {
                ActiveAreas.RemoveAt(i); // 移除区域
                return; // 移除后退出
            }
        }
    }
}