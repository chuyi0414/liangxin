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
    [SerializeField] private int _navMeshAgentTypeId = 0; // NavMesh 代理类型 ID（用于选择哪一套 NavMesh）
    [SerializeField] private float _agentRadius = 0.4f; // 寻路半径（用于网格障碍检测）
    [SerializeField] private LayerMask _dynamicObstacleMask = 0; // 动态障碍层
    [SerializeField] private float _repathInterval = 0.5f; // NavMesh 重算间隔

    [Header("局部避让配置")] // Inspector 分组：局部避让配置
    [SerializeField] private bool _enableLocalAvoidance = false; // 是否启用局部避让（用于单位之间自动绕开）
    [SerializeField] private bool _useColliderBoundsForAvoidanceRadius = true; // 是否使用自身碰撞体 Bounds 自动计算避让半径
    [SerializeField] private float _avoidanceRadius = 0.6f; // 局部避让半径（当不使用碰撞体自动计算时生效）
    [SerializeField] private float _avoidanceStrength = 1f; // 局部避让强度（越大越倾向远离邻居）
    [SerializeField] private LayerMask _avoidanceMask = 0; // 局部避让检测层（例如 Employee 层）
    [SerializeField] private int _avoidanceMaxHits = 16; // 局部避让最多检测碰撞体数量（避免数组扩容）

    private readonly List<Vector2> _pathBuffer = new List<Vector2>(32); // 路径点缓存
    private int _currentWaypointIndex = -1; // 当前路径点索引
    private Vector2 _currentVelocity; // 当前速度缓存
    private Vector2 _currentDestination; // 当前目标点缓存
    private float _repathTimer; // 重算计时器
    private NavigationMode _currentMode = NavigationMode.Auto; // 当前导航模式
    private bool _hasPath; // 是否存在有效路径

    private Rigidbody2D _rigidbody2D; // 刚体缓存
    private Collider2D _collider2D; // 碰撞体缓存（用于计算避让半径与邻居关系）
    private Collider2D[] _avoidanceHits; // 局部避让命中缓存数组（NonAlloc）
    private float _cachedAvoidanceRadius; // 缓存的避让半径（使用碰撞体自动计算时）

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
        _collider2D = GetComponent<Collider2D>(); // 缓存碰撞体组件（用于局部避让）
        PrepareLocalAvoidanceCache(); // 初始化局部避让缓存（避免运行时分配）
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
        direction = ApplyLocalAvoidance(currentPos, direction); // 应用局部避让（在不改变目标点的前提下轻度修正移动方向）
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
    /// 初始化局部避让缓存：准备命中数组与避让半径缓存，避免运行时 GC。
    /// </summary>
    private void PrepareLocalAvoidanceCache() // 局部避让缓存准备入口
    {
        if (_avoidanceMaxHits <= 0) // 最大命中数量非法判定
        {
            _avoidanceMaxHits = 1; // 兜底至少为 1
        }

        _avoidanceHits = new Collider2D[_avoidanceMaxHits]; // 分配命中缓存数组（一次性分配）

        if (!_useColliderBoundsForAvoidanceRadius) // 不使用碰撞体自动计算判定
        {
            _cachedAvoidanceRadius = 0f; // 不使用时清空缓存半径
            return; // 直接结束
        }

        if (_collider2D == null) // 碰撞体缺失判定
        {
            _cachedAvoidanceRadius = 0f; // 碰撞体缺失时清空缓存半径
            return; // 直接结束
        }

        var bounds = _collider2D.bounds; // 获取碰撞体世界 Bounds
        var extents = bounds.extents; // 获取 Bounds 半尺寸
        _cachedAvoidanceRadius = Mathf.Max(extents.x, extents.y); // 使用较大半轴作为避让半径（与碰撞体大小一致的量级）
    }

    /// <summary>
    /// 应用局部避让：检测附近同层单位并叠加一个轻度“远离邻居”的修正方向。
    /// </summary>
    /// <param name="currentPos">当前世界坐标（2D）。</param>
    /// <param name="direction">原始移动方向（已归一化）。</param>
    /// <returns>修正后的移动方向（归一化）。</returns>
    private Vector2 ApplyLocalAvoidance(Vector2 currentPos, Vector2 direction) // 局部避让应用入口
    {
        if (!_enableLocalAvoidance) // 未启用判定
        {
            return direction; // 未启用时直接返回原方向
        }

        if (_avoidanceMask.value == 0) // 避让层为空判定
        {
            return direction; // 未配置避让层时不处理
        }

        if (_avoidanceStrength <= 0f) // 强度非法判定
        {
            return direction; // 强度为 0 时不处理
        }

        var radius = _useColliderBoundsForAvoidanceRadius ? _cachedAvoidanceRadius : _avoidanceRadius; // 计算实际避让半径
        if (radius <= 0f) // 半径非法判定
        {
            return direction; // 半径为 0 时不处理
        }

        if (_avoidanceHits == null || _avoidanceHits.Length == 0) // 命中数组缺失判定
        {
            return direction; // 缺失时直接返回原方向
        }

        var hitCount = Physics2D.OverlapCircleNonAlloc(currentPos, radius, _avoidanceHits, _avoidanceMask); // 获取范围内碰撞体（NonAlloc）
        if (hitCount <= 0) // 未命中判定
        {
            return direction; // 未命中邻居时不处理
        }

        var avoidance = Vector2.zero; // 避让向量累积
        for (int i = 0; i < hitCount; i++) // 遍历命中碰撞体
        {
            var hit = _avoidanceHits[i]; // 获取命中碰撞体
            if (hit == null) // 碰撞体为空判定
            {
                continue; // 为空时跳过
            }

            var hitRigidbody = hit.attachedRigidbody; // 获取命中碰撞体附带刚体
            if (hitRigidbody == null) // 刚体缺失判定
            {
                continue; // 无刚体时跳过
            }

            if (hitRigidbody == _rigidbody2D) // 命中自身判定
            {
                continue; // 忽略自身
            }

            var otherPos = hitRigidbody.position; // 获取邻居刚体位置
            var diff = currentPos - otherPos; // 计算远离邻居的方向
            var distSqr = diff.sqrMagnitude; // 计算距离平方
            if (distSqr <= 0.0001f) // 距离过小判定
            {
                continue; // 距离过小不处理
            }

            var dist = Mathf.Sqrt(distSqr); // 计算距离
            if (dist >= radius) // 超出半径判定
            {
                continue; // 超出半径时跳过
            }

            var weight = (radius - dist) / radius; // 根据距离计算权重（越近权重越大）
            avoidance += (diff / dist) * weight; // 按权重叠加单位化避让方向
        }

        if (avoidance.sqrMagnitude <= 0.0001f) // 避让向量为空判定
        {
            return direction; // 无有效避让时返回原方向
        }

        var finalDirection = direction + avoidance.normalized * _avoidanceStrength; // 叠加避让修正方向
        if (finalDirection.sqrMagnitude <= 0.0001f) // 方向退化判定
        {
            return direction; // 退化时回退原方向
        }

        return finalDirection.normalized; // 返回归一化后的最终方向
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
        var filter = new NavMeshQueryFilter(); // 创建 NavMesh 查询过滤器
        filter.agentTypeID = _navMeshAgentTypeId; // 指定本代理使用的 NavMesh 代理类型（选择对应 NavMesh）
        filter.areaMask = NavMesh.AllAreas; // 使用所有区域（区域限制由烘焙与 Modifier 控制）
        var originalStartWorld2D = (Vector2)transform.position; // 读取原始起点世界坐标（2D）
        var startWorld2D = originalStartWorld2D; // 默认使用原始起点作为寻路起点
        if (_navMeshAgentTypeId != 0) // 非默认代理（例如员工）判定
        {
            var diffToDestination = destination - originalStartWorld2D; // 计算起点到目标点的方向向量
            if (diffToDestination.sqrMagnitude > 0.0001f) // 方向有效判定
            {
                var offsetDistance = Mathf.Max(_agentRadius, 0.05f); // 计算偏移距离（使用单位半径，避免采样落在自身动态障碍洞内）
                startWorld2D = originalStartWorld2D + diffToDestination.normalized * offsetDistance; // 将起点沿目标方向前推一点，减少“先去边缘再折返”的路径
            }
        }

        var sourcePosition = ToNavMeshPosition(startWorld2D); // 转换起点坐标（用于 NavMesh 采样）
        var destinationPosition = ToNavMeshPosition(destination); // 转换终点坐标
        if (!NavMesh.SamplePosition(sourcePosition, out var startHit, 0.2f, filter) && // 先用较小半径采样起点（仅在本代理类型 NavMesh 上采样）
            !NavMesh.SamplePosition(sourcePosition, out startHit, 2f, filter)) // 小半径失败时再用大半径兜底
        {
            sourcePosition = ToNavMeshPosition(originalStartWorld2D); // 回退使用原始起点（避免偏移起点恰好落在不可达区域）
            if (!NavMesh.SamplePosition(sourcePosition, out startHit, 0.2f, filter) && // 再次尝试采样起点
                !NavMesh.SamplePosition(sourcePosition, out startHit, 2f, filter)) // 小半径失败时再用大半径兜底
            {
                return false; // 起点采样失败时返回失败
            }
        }

        if (!NavMesh.SamplePosition(destinationPosition, out var endHit, 0.2f, filter) && // 先用较小半径采样终点（仅在本代理类型 NavMesh 上采样）
            !NavMesh.SamplePosition(destinationPosition, out endHit, 2f, filter)) // 小半径失败时再用大半径兜底
        {
            return false; // 终点采样失败时返回失败
        }

        if (!NavMesh.CalculatePath(startHit.position, endHit.position, filter, path)) // 使用采样点计算路径（避免起点/终点落在别的 NavMesh 上导致大范围绕路）
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

        if (_navMeshAgentTypeId != 0 && _pathBuffer.Count > 0) // 员工等非默认 NavMesh 代理时执行轻量目标点校正
        {
            var lastIndex = _pathBuffer.Count - 1; // 获取最后一个路径点索引
            var lastCorner = _pathBuffer[lastIndex]; // 获取最后一个路径点
            var diffToClick = destination - lastCorner; // 计算点击点与终点差值
            if (diffToClick.sqrMagnitude <= 0.25f) // 仅在偏差较小（0.5 以内）时才校正，避免越过不可达区域
            {
                var sameLayerMask = 1 << gameObject.layer; // 使用自身所在 Layer 作为“同类单位占用”检测层
                var overlap = Physics2D.OverlapPoint(destination, sameLayerMask); // 检测点击点是否被同层单位占用
                if (overlap == null) // 未被同层单位占用判定
                {
                    _pathBuffer[lastIndex] = destination; // 用点击点替换终点，保证落点更贴合鼠标
                }
            }
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
