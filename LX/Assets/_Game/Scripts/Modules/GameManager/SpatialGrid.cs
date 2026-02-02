using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 空间网格分区系统：用于加速附近单位查询，减少物理触发器开销。
/// </summary>
public class SpatialGrid
{
    /// <summary>
    /// 网格字典：键是网格坐标，值是该网格内的单位列表。
    /// </summary>
    private Dictionary<Vector2Int, List<UnitBaseEntity>> _grid;

    /// <summary>
    /// 单个网格单元的大小（世界坐标单位）。
    /// </summary>
    private float _cellSize;

    /// <summary>
    /// 构造函数：初始化空间网格系统。
    /// </summary>
    /// <param name="cellSize">网格单元大小，建议设为视觉范围的 1~2 倍。</param>
    public SpatialGrid(float cellSize)
    {
        _cellSize = cellSize;
        _grid = new Dictionary<Vector2Int, List<UnitBaseEntity>>();
    }

    /// <summary>
    /// 将世界坐标转换为网格坐标。
    /// </summary>
    /// <param name="worldPos">世界坐标。</param>
    /// <returns>网格坐标。</returns>
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / _cellSize);
        int y = Mathf.FloorToInt(worldPos.y / _cellSize);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// 添加单位到网格中。
    /// </summary>
    /// <param name="entity">需要添加的单位。</param>
    public void Add(UnitBaseEntity entity)
    {
        if (entity == null) return;

        Vector2Int gridPos = WorldToGrid(entity.transform.position);
        if (!_grid.ContainsKey(gridPos))
        {
            _grid[gridPos] = new List<UnitBaseEntity>();
        }

        if (!_grid[gridPos].Contains(entity))
        {
            _grid[gridPos].Add(entity);
        }
    }

    /// <summary>
    /// 从网格中移除单位。
    /// </summary>
    /// <param name="entity">需要移除的单位。</param>
    public void Remove(UnitBaseEntity entity)
    {
        if (entity == null) return;

        Vector2Int gridPos = WorldToGrid(entity.transform.position);
        if (_grid.ContainsKey(gridPos))
        {
            _grid[gridPos].Remove(entity);
        }
    }

    /// <summary>
    /// 更新单位在网格中的位置（当单位移动时调用）。
    /// </summary>
    /// <param name="entity">需要更新的单位。</param>
    /// <param name="oldPos">单位旧位置。</param>
    /// <param name="newPos">单位新位置。</param>
    public void UpdatePosition(UnitBaseEntity entity, Vector3 oldPos, Vector3 newPos)
    {
        if (entity == null) return;

        Vector2Int oldGrid = WorldToGrid(oldPos);
        Vector2Int newGrid = WorldToGrid(newPos);

        if (oldGrid == newGrid) return;

        if (_grid.ContainsKey(oldGrid))
        {
            _grid[oldGrid].Remove(entity);
        }

        Add(entity);
    }

    /// <summary>
    /// 获取指定位置半径范围内的所有单位。
    /// </summary>
    /// <param name="worldPos">查询中心点。</param>
    /// <param name="radius">查询半径。</param>
    /// <returns>范围内单位列表。</returns>
    public List<UnitBaseEntity> GetNearby(Vector3 worldPos, float radius)
    {
        List<UnitBaseEntity> result = new List<UnitBaseEntity>();
        Vector2Int center = WorldToGrid(worldPos);

        int cellRange = Mathf.CeilToInt(radius / _cellSize);

        for (int x = -cellRange; x <= cellRange; x++)
        {
            for (int y = -cellRange; y <= cellRange; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                if (_grid.ContainsKey(checkPos))
                {
                    List<UnitBaseEntity> list = _grid[checkPos];
                    for (int i = 0; i < list.Count; i++)
                    {
                        UnitBaseEntity unit = list[i];
                        if (unit == null) continue;

                        float sqr = (unit.transform.position - worldPos).sqrMagnitude;
                        if (sqr <= radius * radius)
                        {
                            result.Add(unit);
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 获取可能在范围内的候选单位（不做距离过滤，适合配合盒体最近点判断）。
    /// </summary>
    /// <param name="worldPos">查询中心点。</param>
    /// <param name="radius">查询半径。</param>
    /// <returns>候选单位列表。</returns>
    public List<UnitBaseEntity> GetNearbyCandidates(Vector3 worldPos, float radius)
    {
        List<UnitBaseEntity> result = new List<UnitBaseEntity>();
        Vector2Int center = WorldToGrid(worldPos);

        int cellRange = Mathf.CeilToInt(radius / _cellSize);

        for (int x = -cellRange; x <= cellRange; x++)
        {
            for (int y = -cellRange; y <= cellRange; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                if (_grid.ContainsKey(checkPos))
                {
                    List<UnitBaseEntity> list = _grid[checkPos];
                    for (int i = 0; i < list.Count; i++)
                    {
                        UnitBaseEntity unit = list[i];
                        if (unit == null) continue;
                        result.Add(unit);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 清空所有网格数据。
    /// </summary>
    public void Clear()
    {
        _grid.Clear();
    }
}