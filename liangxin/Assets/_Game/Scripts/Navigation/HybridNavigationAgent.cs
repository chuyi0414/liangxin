// 引用 CYFramework 命名空间，使用 CY.Log 等入口
using CYFramework; // CYFramework 入口引用
// 引用泛型集合命名空间，使用 List 容器
using System.Collections.Generic; // 泛型集合引用
// 引用 UnityEngine，使用 MonoBehaviour/Vector/Time 等类型
using UnityEngine; // Unity 引擎基础类型引用
// 引用 UnityEngine.AI，使用 NavMeshPath 等类型
using UnityEngine.AI; // NavMesh 相关类型引用

/// <summary>
/// 导航模式：默认自动，优先 NavMesh，失败时回退 A*。
/// </summary>
public enum NavigationMode // 导航模式枚举
{
    Auto = 0, // 自动选择导航模式
    NavMesh = 1, // 强制使用 NavMesh
    GridAStar = 2 // 强制使用网格 A*
}

/// <summary>
/// NavMesh 坐标平面（用于 2D 项目坐标映射）。
/// </summary>
public enum NavMeshPlane // NavMesh 平面枚举
{
    XZ = 0, // 使用 XZ 平面
    XY = 1 // 使用 XY 平面
}

/// <summary>
/// 混合导航代理：先尝试 NavMesh，失败后回退到 A* 网格寻路，实现临时点与动态障碍兼容。
/// 用法：挂到需要自动寻路的单位上，调用 SetDestination 即可。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))] // 强制挂载 Rigidbody2D，确保可移动
public sealed class HybridNavigationAgent : MonoBehaviour // 混合导航代理组件
{
    
    [Header("移动配置")] // Inspector 分组：移动配置
    [SerializeField] private float _moveSpeed = 5f; // 移动速度
    [SerializeField] private float _stoppingDistance = 0.1f; // 停止距离阈值

    [Header("寻路配置")] // Inspector 分组：寻路配置
    [SerializeField] private NavigationMode _defaultMode = NavigationMode.Auto; // 默认导航模式
    [SerializeField] private NavMeshPlane _navMeshPlane = NavMeshPlane.XY; // NavMesh 坐标平面
    [SerializeField] private float _agentRadius = 0.4f; // 寻路半径（用于网格障碍检测）
    [SerializeField] private LayerMask _dynamicObstacleMask = 0; // 动态障碍层
    [SerializeField] private float _repathInterval = 0.5f; // NavMesh 重算间隔

    private readonly List<Vector2> _pathBuffer = new List<Vector2>(32); // 路径点缓存
    private int _currentWaypointIndex = -1; // 当前路径点索引
    private Vector2 _currentVelocity; // 当前速度缓存
    private Vector2 _currentDestination; // 当前目标点缓存
    private float _repathTimer; // 重算计时器
    private NavigationMode _currentMode = NavigationMode.Auto; // 当前导航模式
    private bool _hasPath; // 是否存在有效路径

    private Rigidbody2D _rigidbody2D; // 刚体缓存

    public bool HasPath => _hasPath; // 是否有路径的外部只读访问
    public NavigationMode CurrentMode => _currentMode; // 当前导航模式的外部只读访问

    /// <summary>
    /// 组件初始化：缓存刚体并禁用旋转/重力。
    /// </summary>
    private void Awake() // 生命周期：Awake
    {
        _rigidbody2D = GetComponent<Rigidbody2D>(); // 缓存刚体组件
        _rigidbody2D.gravityScale = 0f; // 禁用重力
        _rigidbody2D.freezeRotation = true; // 冻结旋转
    }

    /// <summary>
    /// 组件物理更新：沿路径移动并处理重算。
    /// </summary>
    private void FixedUpdate() // 生命周期：FixedUpdate（物理步进）
    {
        if (!_hasPath)
        {
            return; // 没有路径时直接退出
        }

        if (_currentWaypointIndex < 0 || _currentWaypointIndex >= _pathBuffer.Count)
        {
            _hasPath = false; // 索引无效时清空路径
            return; // 索引无效时退出
        }

        var target = _pathBuffer[_currentWaypointIndex]; // 获取当前路径点
        var currentPos = (Vector2)transform.position; // 获取当前位置
        var diff = target - currentPos; // 计算方向向量

        if (diff.sqrMagnitude <= _stoppingDistance * _stoppingDistance)
        {
            _currentWaypointIndex++; // 进入下一个路径点
            if (_currentWaypointIndex >= _pathBuffer.Count)
            {
                _hasPath = false; // 已到达终点
                return; // 到达终点时退出
            }
            target = _pathBuffer[_currentWaypointIndex]; // 更新当前目标点
            diff = target - currentPos; // 重新计算方向向量
        }

        var direction = diff.normalized; // 归一化方向
        _currentVelocity = direction * _moveSpeed; // 计算当前速度
        _rigidbody2D.MovePosition(currentPos + _currentVelocity * Time.fixedDeltaTime); // 刚体移动

        if (_currentMode == NavigationMode.NavMesh && _repathInterval > 0f)
        {
            _repathTimer -= Time.fixedDeltaTime; // 递减重算计时器
            if (_repathTimer <= 0f)
            {
                _repathTimer = _repathInterval; // 重置重算计时器
                SetDestination(_currentDestination, _currentMode, true); // 重算当前目标路径
            }
        }
    }

    /// <summary>
    /// 设置目标位置。
    /// </summary>
    /// <param name="destination">目标世界坐标。</param>
    /// <param name="mode">导航模式（Auto = 先 NavMesh 后 A*）。</param>
    /// <param name="isRepath">内部重算标记，外部调用保持 false。</param>
    public bool SetDestination(Vector2 destination, NavigationMode mode = NavigationMode.Auto, bool isRepath = false) // 设置目标入口
    {
        _currentDestination = destination; // 记录目标坐标
        _currentMode = mode == NavigationMode.Auto ? _defaultMode : mode; // 解析最终导航模式

        if (TryBuildNavMeshPath(destination))
        {
            return true; // NavMesh 路径成功时返回
        }

        if (TryBuildGridPath(destination))
        {
            return true; // A* 路径成功时返回
        }

        if (!isRepath)
        {
            CY.LogWarning($"[HybridNavigationAgent] 无法到达目标：{destination}。"); // 外部调用失败时提示警告
        }

        _hasPath = false; // 标记无路径
        return false; // 无法寻路返回失败
    }

    /// <summary>
    /// 尝试使用 NavMesh 生成路径。
    /// </summary>
    private bool TryBuildNavMeshPath(Vector2 destination) // NavMesh 寻路入口
    {
        if (_currentMode == NavigationMode.GridAStar)
        {
            return false; // 强制 A* 时不走 NavMesh
        }

        var path = new NavMeshPath(); // 创建 NavMeshPath
        var sourcePosition = ToNavMeshPosition(transform.position); // 转换起点坐标
        var destinationPosition = ToNavMeshPosition(destination); // 转换终点坐标
        if (!NavMesh.SamplePosition(sourcePosition, out var startHit, 1f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(destinationPosition, out var endHit, 1f, NavMesh.AllAreas))
        {
            return false; // 起点/终点采样失败
        }

        if (!NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path))
        {
            return false; // 路径计算失败
        }

        if (path.status != NavMeshPathStatus.PathComplete)
        {
            return false; // 路径不完整
        }

        _pathBuffer.Clear(); // 清空路径缓存
        for (int i = 0; i < path.corners.Length; i++)
        {
            var corner = path.corners[i]; // 获取路径拐点
            _pathBuffer.Add(FromNavMeshPosition(corner)); // 转换回 2D 坐标并写入
        }

        _currentWaypointIndex = 0; // 重置路径索引
        _hasPath = _pathBuffer.Count > 0; // 更新路径标记
        _currentMode = NavigationMode.NavMesh; // 标记为 NavMesh 模式
        return _hasPath; // 返回是否成功
    }

    /// <summary>
    /// 尝试使用网格 A* 生成路径。
    /// </summary>
    private bool TryBuildGridPath(Vector2 destination) // A* 寻路入口
    {
        if (!NavGridArea.TryGetByPositionOrNearest((Vector2)transform.position, out var area))
        {
            return false; // 无可用区域时失败
        }

        if (!GridPathfinder.FindPath(
                area,
                transform.position,
                destination,
                _agentRadius,
                _dynamicObstacleMask,
                area.StaticObstacleMask,
                _pathBuffer))
        {
            return false; // A* 寻路失败
        }

        _currentWaypointIndex = 0; // 重置路径索引
        _hasPath = _pathBuffer.Count > 0; // 更新路径标记
        _currentMode = NavigationMode.GridAStar; // 标记为 A* 模式
        return _hasPath; // 返回是否成功
    }

    /// <summary>
    /// 将 2D 世界坐标映射到 NavMesh 坐标。
    /// </summary>
    private Vector3 ToNavMeshPosition(Vector2 worldPos) // 2D 到 NavMesh 坐标转换
    {
        if (_navMeshPlane == NavMeshPlane.XY)
        {
            return new Vector3(worldPos.x, worldPos.y, 0f); // XY 平面映射
        }

        return new Vector3(worldPos.x, 0f, worldPos.y); // XZ 平面映射
    }

    /// <summary>
    /// 将 3D 坐标映射到 NavMesh 坐标（以 XY 或 XZ 平面为基准）。
    /// </summary>
    private Vector3 ToNavMeshPosition(Vector3 worldPos) // 3D 到 NavMesh 坐标转换
    {
        if (_navMeshPlane == NavMeshPlane.XY)
        {
            return new Vector3(worldPos.x, worldPos.y, 0f); // XY 平面映射
        }

        return new Vector3(worldPos.x, 0f, worldPos.y); // XZ 平面映射
    }

    /// <summary>
    /// 将 NavMesh 坐标映射回 2D 世界坐标。
    /// </summary>
    private Vector2 FromNavMeshPosition(Vector3 navPos) // NavMesh 到 2D 坐标转换
    {
        return _navMeshPlane == NavMeshPlane.XY
            ? new Vector2(navPos.x, navPos.y) // XY 平面回映射
            : new Vector2(navPos.x, navPos.z); // XZ 平面回映射
    }
}
