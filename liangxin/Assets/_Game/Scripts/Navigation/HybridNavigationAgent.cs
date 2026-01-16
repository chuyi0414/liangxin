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
    [SerializeField] private float _fallbackSampleRadius = 8f; // 目标不可达时的 NavMesh 采样半径
    [SerializeField] private float _gridFallbackSearchRadius = 6f; // 目标不可达时的 A* 最近可走点搜索半径（世界单位）

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

        if (!isRepath && TryResolveDestination(destination, out var resolvedDestination)) // 外部调用失败后尝试修正目标
        {
            _currentDestination = resolvedDestination; // 记录修正后的目标点
            if (TryBuildNavMeshPath(resolvedDestination))
            {
                return true; // 修正后 NavMesh 成功时返回
            }

            if (TryBuildGridPath(resolvedDestination))
            {
                return true; // 修正后 A* 成功时返回
            }
        }

        if (!isRepath)
        {
            CY.LogWarning($"[HybridNavigationAgent] 无法到达目标：{destination}。"); // 外部调用失败时提示警告
        }

        _hasPath = false; // 标记无路径
        return false; // 无法寻路返回失败
    }

    /// <summary>
    /// 将“剩余路径点（从当前路径点开始）”复制到外部缓存数组（NonAlloc）。
    /// 说明：用于在运行时可视化路径（例如右键移动显示路径线/路径点），避免直接暴露内部 List 造成误修改与 GC。
    /// </summary>
    /// <param name="buffer">外部缓存数组（由调用者复用，避免每次分配）。</param>
    /// <param name="includeCurrentPosition">是否在数组第 0 个位置写入“当前脚下位置”。</param>
    /// <returns>实际写入的点数量。</returns>
    public int CopyRemainingPathPointsNonAlloc(Vector2[] buffer, bool includeCurrentPosition = true) // 剩余路径点拷贝入口（NonAlloc）
    {
        if (buffer == null) // 缓存数组为空判定
        {
            return 0; // 缓存为空时返回 0
        }

        if (buffer.Length <= 0) // 缓存长度为 0 判定
        {
            return 0; // 无容量时返回 0
        }

        if (!_hasPath) // 无路径判定
        {
            return 0; // 无路径时返回 0（调用方可据此隐藏可视化）
        }

        if (_pathBuffer == null || _pathBuffer.Count <= 0) // 内部路径缓存为空判定
        {
            return 0; // 无有效路径点时返回 0
        }

        var startIndex = _currentWaypointIndex; // 读取当前路径点索引
        if (startIndex < 0) // 下限保护判定
        {
            startIndex = 0; // 回退为 0
        }

        if (startIndex >= _pathBuffer.Count) // 上限保护判定
        {
            startIndex = _pathBuffer.Count; // 回退为 Count（后续循环不会写入）
        }

        var writeIndex = 0; // 记录写入索引
        if (includeCurrentPosition) // 需要写入脚下点判定
        {
            buffer[writeIndex] = (Vector2)transform.position; // 写入当前脚下世界坐标（2D）
            writeIndex++; // 推进写入索引
            if (writeIndex >= buffer.Length) // 缓存已满判定
            {
                return writeIndex; // 已写满时直接返回写入数量
            }
        }

        for (int i = startIndex; i < _pathBuffer.Count; i++) // 从当前路径点开始遍历剩余路径点
        {
            if (writeIndex >= buffer.Length) // 缓存容量不足判定
            {
                break; // 容量不足时提前结束
            }

            buffer[writeIndex] = _pathBuffer[i]; // 将剩余路径点写入外部缓存
            writeIndex++; // 推进写入索引
        }

        return writeIndex; // 返回实际写入数量
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
    /// 尝试将不可达目标修正为最近可走点。
    /// </summary>
    /// <param name="destination">原目标坐标。</param>
    /// <param name="resolvedDestination">输出修正后的目标坐标。</param>
    private bool TryResolveDestination(Vector2 destination, out Vector2 resolvedDestination) // 目标修正入口
    {
        resolvedDestination = destination; // 默认返回原目标
        var resolved = false; // 记录是否找到修正点

        if (_currentMode != NavigationMode.GridAStar) // 非强制 A* 模式时优先尝试 NavMesh
        {
            if (TrySampleNavMeshDestination(destination, out var navMeshDestination))
            {
                resolvedDestination = navMeshDestination; // 记录 NavMesh 采样点
                resolved = true; // 标记已修正
            }
        }

        if (!resolved) // NavMesh 未修正时尝试 A* 网格
        {
            if (TrySampleGridDestination(destination, out var gridDestination))
            {
                resolvedDestination = gridDestination; // 记录网格采样点
                resolved = true; // 标记已修正
            }
        }

        return resolved; // 返回是否修正成功
    }

    /// <summary>
    /// 尝试在 NavMesh 上采样最近可走点。
    /// </summary>
    /// <param name="destination">原目标坐标。</param>
    /// <param name="resolvedDestination">输出修正后的目标坐标。</param>
    private bool TrySampleNavMeshDestination(Vector2 destination, out Vector2 resolvedDestination) // NavMesh 采样入口
    {
        resolvedDestination = destination; // 默认返回原目标
        if (_fallbackSampleRadius <= 0f) // 采样半径禁用判定
        {
            return false; // 禁用时直接失败
        }

        var filter = new NavMeshQueryFilter(); // 创建查询过滤器
        filter.agentTypeID = _navMeshAgentTypeId; // 指定代理类型
        filter.areaMask = NavMesh.AllAreas; // 使用所有区域
        var destinationPosition = ToNavMeshPosition(destination); // 转换目标坐标到 NavMesh 坐标
        if (!NavMesh.SamplePosition(destinationPosition, out var endHit, _fallbackSampleRadius, filter)) // 采样最近点
        {
            return false; // 采样失败时返回
        }

        resolvedDestination = FromNavMeshPosition(endHit.position); // 回写采样点
        return true; // 返回采样成功
    }

    /// <summary>
    /// 尝试在 A* 网格上采样最近可走点。
    /// </summary>
    /// <param name="destination">原目标坐标。</param>
    /// <param name="resolvedDestination">输出修正后的目标坐标。</param>
    private bool TrySampleGridDestination(Vector2 destination, out Vector2 resolvedDestination) // 网格采样入口
    {
        resolvedDestination = destination; // 默认返回原目标
        if (_gridFallbackSearchRadius <= 0f) // 搜索半径禁用判定
        {
            return false; // 禁用时直接失败
        }

        if (!NavGridArea.TryGetByPositionOrNearest(destination, out var area)) // 获取最近网格区域
        {
            return false; // 无区域时失败
        }

        var cellSize = area.CellSize; // 读取格子大小
        if (cellSize <= 0.0001f) // 格子大小异常判定
        {
            return false; // 格子异常时失败
        }

        var areaCenter = area.AreaCenter; // 读取区域中心
        var areaExtents = area.AreaExtents; // 读取区域半尺寸
        var width = Mathf.Max(1, Mathf.CeilToInt((areaExtents.x * 2f) / cellSize)); // 计算网格宽度
        var height = Mathf.Max(1, Mathf.CeilToInt((areaExtents.y * 2f) / cellSize)); // 计算网格高度
        var origin = new Vector2(areaCenter.x - areaExtents.x, areaCenter.y - areaExtents.y); // 计算网格原点

        var cellX = Mathf.FloorToInt((destination.x - origin.x) / cellSize); // 计算目标格子 X
        var cellY = Mathf.FloorToInt((destination.y - origin.y) / cellSize); // 计算目标格子 Y
        cellX = Mathf.Clamp(cellX, 0, width - 1); // 夹取格子 X 到范围内
        cellY = Mathf.Clamp(cellY, 0, height - 1); // 夹取格子 Y 到范围内

        var maxCellRadius = Mathf.Max(0, Mathf.CeilToInt(_gridFallbackSearchRadius / cellSize)); // 计算最大搜索格数
        var bestDist = float.MaxValue; // 最近距离缓存
        var bestPos = destination; // 最近位置缓存
        var found = false; // 是否找到可走点

        for (int r = 0; r <= maxCellRadius; r++) // 按环半径逐圈搜索
        {
            var foundInRing = false; // 当前环是否找到候选
            for (int dx = -r; dx <= r; dx++) // 遍历环的 X 偏移
            {
                for (int dy = -r; dy <= r; dy++) // 遍历环的 Y 偏移
                {
                    if (r > 0 && Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) // 仅检查环边界
                    {
                        continue; // 非环边界点跳过
                    }

                    var x = cellX + dx; // 计算候选格子 X
                    var y = cellY + dy; // 计算候选格子 Y
                    if (x < 0 || x >= width || y < 0 || y >= height) // 越界判定
                    {
                        continue; // 越界时跳过
                    }

                    if (IsGridCellBlocked(origin, cellSize, x, y, area.StaticObstacleMask)) // 障碍格子判定
                    {
                        continue; // 被障碍阻挡时跳过
                    }

                    var worldPos = GridCellToWorld(origin, cellSize, x, y); // 转换为世界坐标
                    var dist = (worldPos - destination).sqrMagnitude; // 计算距离平方
                    if (dist < bestDist) // 更近判定
                    {
                        bestDist = dist; // 更新最优距离
                        bestPos = worldPos; // 更新最优位置
                        found = true; // 标记找到
                    }

                    foundInRing = true; // 标记本环存在候选
                }
            }

            if (foundInRing && found) // 本环已找到可走点
            {
                break; // 最近环找到即可退出
            }
        }

        if (!found) // 未找到可走点判定
        {
            return false; // 未找到时失败
        }

        resolvedDestination = bestPos; // 回写最优位置
        return true; // 返回成功
    }

    /// <summary>
    /// 网格格子坐标转换为世界坐标。
    /// </summary>
    /// <param name="origin">网格原点。</param>
    /// <param name="cellSize">格子大小。</param>
    /// <param name="x">格子 X。</param>
    /// <param name="y">格子 Y。</param>
    private Vector2 GridCellToWorld(Vector2 origin, float cellSize, int x, int y) // 网格坐标转换入口
    {
        return new Vector2( // 返回世界坐标
            origin.x + (x + 0.5f) * cellSize, // 计算 X 坐标
            origin.y + (y + 0.5f) * cellSize); // 计算 Y 坐标
    }

    /// <summary>
    /// 判断网格格子是否被障碍阻挡。
    /// </summary>
    /// <param name="origin">网格原点。</param>
    /// <param name="cellSize">格子大小。</param>
    /// <param name="x">格子 X。</param>
    /// <param name="y">格子 Y。</param>
    /// <param name="staticObstacleMask">静态障碍层。</param>
    private bool IsGridCellBlocked(Vector2 origin, float cellSize, int x, int y, LayerMask staticObstacleMask) // 网格障碍检测入口
    {
        var center = GridCellToWorld(origin, cellSize, x, y); // 计算格子中心点
        var extents = new Vector2(_agentRadius * 2f, _agentRadius * 2f); // 计算单位占用尺寸
        var size = new Vector2(Mathf.Min(cellSize, extents.x), Mathf.Min(cellSize, extents.y)); // 计算检测盒大小
        return Physics2D.OverlapBox(center, size, 0f, _dynamicObstacleMask | staticObstacleMask); // 重叠检测障碍
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
