// 引用 System 命名空间，使用 Array 等基础类型
using System; // System 基础功能引用
// 引用泛型集合命名空间，使用 List/Stack 等容器
using System.Collections.Generic; // 泛型集合引用
// 引用 UnityEngine 命名空间，使用 Vector/Mathf/Physics2D 等类型
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 网格 A* 寻路工具（基于 NavGridArea 配置，适用于高动态临时目标）。
/// </summary>
public static class GridPathfinder // A* 网格寻路工具类
{
    /// <summary>
    /// 单个节点记录（包含代价与父节点信息）。
    /// </summary>
    private struct NodeRecord // A* 节点记录结构
    {
        public float GCost; // 起点到当前节点的累积代价
        public float HCost; // 当前节点到目标的启发式代价
        public int Parent; // 父节点索引
        public bool Closed; // 是否已加入关闭列表
    }

    private static readonly int[] NeighborOffsetX = { 1, -1, 0, 0, 1, 1, -1, -1 }; // 邻居 X 偏移表（含斜向）
    private static readonly int[] NeighborOffsetY = { 0, 0, 1, -1, 1, -1, 1, -1 }; // 邻居 Y 偏移表（含斜向）

    /// <summary>
    /// 尝试在指定区域内找到一条网格路径。
    /// </summary>
    /// <param name="area">参考的 NavGridArea（提供边界与配置）。</param>
    /// <param name="start">起点世界坐标。</param>
    /// <param name="goal">终点世界坐标。</param>
    /// <param name="agentRadius">单位半径（用于障碍检测）。</param>
    /// <param name="dynamicObstacleMask">动态障碍层。</param>
    /// <param name="staticObstacleMask">静态障碍层。</param>
    /// <param name="result">输出路径（顺序为起点到终点）。</param>
    public static bool FindPath( // 寻路入口
        NavGridArea area, // 导航区域
        Vector2 start, // 起点坐标
        Vector2 goal, // 终点坐标
        float agentRadius, // 单位半径
        LayerMask dynamicObstacleMask, // 动态障碍层
        LayerMask staticObstacleMask, // 静态障碍层
        List<Vector2> result) // 输出路径列表
    {
        if (area == null)
        {
            return false; // 区域为空时无法寻路
        }

        var cellSize = area.CellSize; // 读取格子大小
        var areaCenter = area.AreaCenter; // 读取区域中心
        var areaExtents = area.AreaExtents; // 读取区域半尺寸
        var width = Mathf.Max(1, Mathf.CeilToInt((areaExtents.x * 2f) / cellSize)); // 计算宽度格子数
        var height = Mathf.Max(1, Mathf.CeilToInt((areaExtents.y * 2f) / cellSize)); // 计算高度格子数
        var origin = new Vector2(areaCenter.x - areaExtents.x, areaCenter.y - areaExtents.y); // 计算左下角原点
        var nodeCount = width * height; // 计算节点总数

        var nodes = new NodeRecord[nodeCount]; // 创建节点记录数组
        var openList = new List<int>(128); // 创建开放列表
        var indexBuffer = new int[nodeCount]; // 创建索引缓存
        Array.Fill(indexBuffer, -1); // 初始化索引缓存为未加入状态

        if (!TryWorldToCell(start, origin, cellSize, width, height, out var startX, out var startY))
        {
            ClampToGrid(ref startX, ref startY, width, height); // 起点越界时夹到边界
        }

        if (!TryWorldToCell(goal, origin, cellSize, width, height, out var goalX, out var goalY))
        {
            ClampToGrid(ref goalX, ref goalY, width, height); // 终点越界时夹到边界
        }

        var startIndex = ToIndex(startX, startY, width); // 计算起点索引
        var goalIndex = ToIndex(goalX, goalY, width); // 计算终点索引

        if (IsBlocked(startX, startY, origin, cellSize, agentRadius, dynamicObstacleMask, staticObstacleMask) ||
            IsBlocked(goalX, goalY, origin, cellSize, agentRadius, dynamicObstacleMask, staticObstacleMask))
        {
            return false; // 起点或终点被阻挡时直接失败
        }

        nodes[startIndex].GCost = 0f; // 起点 G 代价为 0
        nodes[startIndex].HCost = Heuristic(startX, startY, goalX, goalY); // 计算起点启发式代价
        nodes[startIndex].Parent = -1; // 起点父节点为空
        AddToOpen(openList, indexBuffer, startIndex); // 将起点加入开放列表

        var iterations = 0; // 迭代计数
        var maxIterations = width * height; // 最大迭代次数

        while (openList.Count > 0 && iterations++ < maxIterations)
        {
            var currentIndex = PopBest(openList, indexBuffer, nodes); // 弹出当前最优节点
            if (currentIndex == goalIndex)
            {
                BuildResultPath(currentIndex, nodes, origin, cellSize, width, result); // 回溯构建路径
                return true; // 找到路径则返回成功
            }

            nodes[currentIndex].Closed = true; // 标记为关闭节点
            var currentX = currentIndex % width; // 计算当前 X 坐标
            var currentY = currentIndex / width; // 计算当前 Y 坐标

            for (int i = 0; i < NeighborOffsetX.Length; i++)
            {
                var nx = currentX + NeighborOffsetX[i]; // 计算邻居 X
                var ny = currentY + NeighborOffsetY[i]; // 计算邻居 Y
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                {
                    continue; // 越界邻居直接跳过
                }

                var neighborIndex = ToIndex(nx, ny, width); // 计算邻居索引
                if (nodes[neighborIndex].Closed)
                {
                    continue; // 已关闭节点跳过
                }

                if (IsBlocked(nx, ny, origin, cellSize, agentRadius, dynamicObstacleMask, staticObstacleMask))
                {
                    continue; // 障碍节点跳过
                }

                var gCost = nodes[currentIndex].GCost + CostBetween(currentX, currentY, nx, ny); // 计算从当前到邻居的 G 代价
                if (indexBuffer[neighborIndex] < 0 || gCost < nodes[neighborIndex].GCost)
                {
                    nodes[neighborIndex].GCost = gCost; // 更新邻居 G 代价
                    nodes[neighborIndex].HCost = Heuristic(nx, ny, goalX, goalY); // 更新邻居 H 代价
                    nodes[neighborIndex].Parent = currentIndex; // 记录父节点
                    AddToOpen(openList, indexBuffer, neighborIndex); // 加入开放列表
                }
            }
        }

        return false; // 未找到路径时返回失败
    }

    /// <summary>
    /// 回溯构建路径结果。
    /// </summary>
    /// <param name="goalIndex">终点索引。</param>
    /// <param name="nodes">节点记录数组。</param>
    /// <param name="origin">区域左下角。</param>
    /// <param name="cellSize">格子大小。</param>
    /// <param name="width">网格宽度。</param>
    /// <param name="result">输出路径列表。</param>
    private static void BuildResultPath(int goalIndex, NodeRecord[] nodes, Vector2 origin, float cellSize, int width, List<Vector2> result) // 路径回溯入口
    {
        if (result == null)
        {
            return; // 输出列表为空时直接返回
        }

        result.Clear(); // 清空旧路径
        var stack = new Stack<int>(); // 创建回溯栈
        var current = goalIndex; // 从终点开始回溯

        while (current >= 0)
        {
            stack.Push(current); // 压入当前节点
            current = nodes[current].Parent; // 继续回溯父节点
        }

        while (stack.Count > 0)
        {
            var index = stack.Pop(); // 弹出路径节点
            var x = index % width; // 计算节点 X
            var y = index / width; // 计算节点 Y
            var worldPos = CellToWorld(origin, cellSize, x, y); // 转换为世界坐标
            result.Add(worldPos); // 写入路径
        }
    }

    /// <summary>
    /// 网格坐标转世界坐标。
    /// </summary>
    private static Vector2 CellToWorld(Vector2 origin, float cellSize, int x, int y) // 坐标转换入口
    {
        return new Vector2( // 返回世界坐标
            origin.x + (x + 0.5f) * cellSize, // 计算 X 坐标
            origin.y + (y + 0.5f) * cellSize); // 计算 Y 坐标
    }

    /// <summary>
    /// 世界坐标转换为格子坐标。
    /// </summary>
    private static bool TryWorldToCell(Vector2 worldPos, Vector2 origin, float cellSize, int width, int height, out int cellX, out int cellY) // 世界到格子转换入口
    {
        cellX = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize); // 计算格子 X
        cellY = Mathf.FloorToInt((worldPos.y - origin.y) / cellSize); // 计算格子 Y
        return cellX >= 0 && cellX < width && cellY >= 0 && cellY < height; // 判断是否在网格内
    }

    /// <summary>
    /// 将格子坐标夹到网格范围内。
    /// </summary>
    private static void ClampToGrid(ref int x, ref int y, int width, int height) // 坐标夹取入口
    {
        x = Mathf.Clamp(x, 0, width - 1); // 夹取 X 到合法范围
        y = Mathf.Clamp(y, 0, height - 1); // 夹取 Y 到合法范围
    }

    /// <summary>
    /// 格子坐标转一维索引。
    /// </summary>
    private static int ToIndex(int x, int y, int width) // 索引转换入口
    {
        return x + y * width; // 计算一维索引
    }

    /// <summary>
    /// 启发式函数（曼哈顿距离）。
    /// </summary>
    private static float Heuristic(int x, int y, int goalX, int goalY) // 启发式计算入口
    {
        return Mathf.Abs(goalX - x) + Mathf.Abs(goalY - y); // 计算曼哈顿距离
    }

    /// <summary>
    /// 计算相邻格子代价（直线为 1，对角为根号 2）。
    /// </summary>
    private static float CostBetween(int x1, int y1, int x2, int y2) // 代价计算入口
    {
        return (x1 == x2 || y1 == y2) ? 1f : 1.4142f; // 直线/对角移动代价
    }

    /// <summary>
    /// 将节点加入开放列表。
    /// </summary>
    private static void AddToOpen(List<int> openList, int[] indexBuffer, int nodeIndex) // 开放列表入列
    {
        if (indexBuffer[nodeIndex] >= 0)
        {
            return; // 已在开放列表中则跳过
        }

        openList.Add(nodeIndex); // 加入开放列表
        indexBuffer[nodeIndex] = openList.Count - 1; // 记录索引位置
    }

    /// <summary>
    /// 弹出开放列表中的最优节点。
    /// </summary>
    private static int PopBest(List<int> openList, int[] indexBuffer, NodeRecord[] nodes) // 弹出最优节点
    {
        var bestIdx = 0; // 最优节点位置
        var bestValue = float.MaxValue; // 最优 F 代价

        for (int i = 0; i < openList.Count; i++)
        {
            var idx = openList[i]; // 当前节点索引
            var record = nodes[idx]; // 当前节点记录
            var f = record.GCost + record.HCost; // 计算 F 代价
            if (f < bestValue)
            {
                bestValue = f; // 更新最优值
                bestIdx = i; // 更新最优索引
            }
        }

        var bestNodeIndex = openList[bestIdx]; // 取出最优节点索引
        var last = openList.Count - 1; // 获取末尾索引
        openList[bestIdx] = openList[last]; // 用末尾节点覆盖
        indexBuffer[openList[bestIdx]] = bestIdx; // 更新被移动节点索引
        openList.RemoveAt(last); // 移除末尾节点
        indexBuffer[bestNodeIndex] = -1; // 标记最优节点已移除
        return bestNodeIndex; // 返回最优节点索引
    }

    /// <summary>
    /// 判断格子是否被障碍阻挡。
    /// </summary>
    private static bool IsBlocked(int cellX, int cellY, Vector2 origin, float cellSize, float agentRadius, LayerMask dynamicMask, LayerMask staticMask) // 障碍检测入口
    {
        var center = CellToWorld(origin, cellSize, cellX, cellY); // 计算格子中心
        var extents = new Vector2(agentRadius * 2f, agentRadius * 2f); // 计算检测半径范围
        var size = new Vector2(Mathf.Min(cellSize, extents.x), Mathf.Min(cellSize, extents.y)); // 计算检测盒大小
        return Physics2D.OverlapBox(center, size, 0f, dynamicMask | staticMask); // 使用重叠盒检测障碍
    }
}