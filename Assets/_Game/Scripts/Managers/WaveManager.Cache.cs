// 引用泛型集合命名空间，使用 List
using System.Collections.Generic; // 集合类型引用
// 引用 CYFramework 入口，使用 CY.Data/CY.Log
using CYFramework; // 框架统一入口
// 引用数据表命名空间，使用 DataTable
using CYFramework.Core.DataTable; // 数据表类型引用

public sealed partial class WaveManager // 波次管理器分部定义
{
    /// <summary>
    /// 构建波次缓存（依赖数据表已加载）。
    /// </summary>
    private bool TryBuildWaveCache() // 缓存构建入口
    {
        ClearAll(); // 清空旧缓存

        if (!CY.Data.HasDataTable(WaveTableName) ||
            !CY.Data.HasDataTable(WaveSpawnPoolTableName) ||
            !CY.Data.HasDataTable(SpawnTypeTableName) ||
            !CY.Data.HasDataTable(EnemyPoolTableName) ||
            !CY.Data.HasDataTable(RefreshTypeTableName))
        {
            CY.LogWarning("[WaveManager] 波次相关数据表未加载。"); // 输出未加载提示
            return false; // 未加载时失败
        }

        var waveTable = CY.Data.GetDataTable<WaveRow>(WaveTableName); // 获取波次表
        var waveSpawnPoolTable = CY.Data.GetDataTable<WaveSpawnPoolRow>(WaveSpawnPoolTableName); // 获取波次池表
        var spawnTypeTable = CY.Data.GetDataTable<SpawnTypeRow>(SpawnTypeTableName); // 获取生成类型表
        var enemyPoolTable = CY.Data.GetDataTable<EnemyPoolRow>(EnemyPoolTableName); // 获取敌人池表
        var refreshTypeTable = CY.Data.GetDataTable<RefreshTypeRow>(RefreshTypeTableName); // 获取刷新类型表

        if (waveTable == null || waveTable.Count == 0)
        {
            CY.LogWarning("[WaveManager] 波次表为空。"); // 输出空表提示
            return false; // 空表时失败
        }

        if (spawnTypeTable == null || spawnTypeTable.Count == 0)
        {
            CY.LogWarning("[WaveManager] 生成类型表为空。"); // 输出空表提示
            return false; // 空表时失败
        }

        if (enemyPoolTable == null || enemyPoolTable.Count == 0)
        {
            CY.LogWarning("[WaveManager] 敌人池表为空。"); // 输出空表提示
            return false; // 空表时失败
        }

        if (refreshTypeTable == null || refreshTypeTable.Count == 0)
        {
            CY.LogWarning("[WaveManager] 刷新类型表为空。"); // 输出空表提示
            return false; // 空表时失败
        }

        CacheWaveRows(waveTable); // 缓存波次表
        CacheSpawnTypes(spawnTypeTable); // 缓存生成类型表
        CacheEnemyPools(enemyPoolTable); // 缓存敌人池表
        CacheRefreshTypes(refreshTypeTable); // 缓存刷新类型表
        CacheWaveSpawnPool(waveSpawnPoolTable); // 缓存波次生成类型池

        BuildTriggerLists(); // 构建触发列表
        return true; // 构建成功
    }

    /// <summary>
    /// 缓存波次表行。
    /// </summary>
    /// <param name="table">波次表。</param>
    private void CacheWaveRows(DataTable<WaveRow> table) // 波次表缓存入口
    {
        var rows = table.GetAllRows(); // 获取所有行
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; // 取出行
            if (row == null)
            {
                continue; // 跳过空行
            }

            _waveMap[row.Id] = row; // 写入缓存
        }
    }

    /// <summary>
    /// 缓存生成类型表行。
    /// </summary>
    /// <param name="table">生成类型表。</param>
    private void CacheSpawnTypes(DataTable<SpawnTypeRow> table) // 生成类型缓存入口
    {
        var rows = table.GetAllRows(); // 获取所有行
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; // 取出行
            if (row == null)
            {
                continue; // 跳过空行
            }

            _spawnTypeMap[row.Id] = row; // 写入缓存
        }
    }

    /// <summary>
    /// 缓存敌人池表行。
    /// </summary>
    /// <param name="table">敌人池表。</param>
    private void CacheEnemyPools(DataTable<EnemyPoolRow> table) // 敌人池缓存入口
    {
        var rows = table.GetAllRows(); // 获取所有行
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; // 取出行
            if (row == null)
            {
                continue; // 跳过空行
            }

            if (!_enemyPoolMap.TryGetValue(row.PoolId, out var group))
            {
                group = new EnemyPoolGroup(row.PoolId); // 创建敌人池组
                _enemyPoolMap.Add(row.PoolId, group); // 写入缓存
            }

            if (row.Weight <= 0)
            {
                continue; // 权重无效时跳过
            }

            group.Entries.Add(new WeightedId(row.EnemyId, row.Weight)); // 添加敌人条目
            group.TotalWeight += row.Weight; // 累加权重
        }
    }

    /// <summary>
    /// 缓存刷新类型表行。
    /// </summary>
    /// <param name="table">刷新类型表。</param>
    private void CacheRefreshTypes(DataTable<RefreshTypeRow> table) // 刷新类型缓存入口
    {
        var rows = table.GetAllRows(); // 获取所有行
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; // 取出行
            if (row == null)
            {
                continue; // 跳过空行
            }

            _refreshTypeMap[row.Id] = row; // 写入缓存
        }
    }

    /// <summary>
    /// 缓存波次生成类型池表行。
    /// </summary>
    /// <param name="table">波次生成类型池表。</param>
    private void CacheWaveSpawnPool(DataTable<WaveSpawnPoolRow> table) // 生成类型池缓存入口
    {
        var rows = table.GetAllRows(); // 获取所有行
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; // 取出行
            if (row == null)
            {
                continue; // 跳过空行
            }

            if (!_waveSpawnPoolMap.TryGetValue(row.WaveId, out var list))
            {
                list = new List<WeightedId>(8); // 创建列表
                _waveSpawnPoolMap.Add(row.WaveId, list); // 写入缓存
            }

            if (row.Weight <= 0)
            {
                continue; // 权重无效时跳过
            }

            list.Add(new WeightedId(row.SpawnTypeId, row.Weight)); // 添加生成类型条目
        }
    }

    /// <summary>
    /// 构建时间/清空触发列表。
    /// </summary>
    private void BuildTriggerLists() // 触发列表构建入口
    {
        foreach (var pair in _waveMap)
        {
            var wave = pair.Value; // 取出波次
            if (wave == null)
            {
                continue; // 跳过空波次
            }

            if (wave.TriggerType == WaveTriggerType.Time)
            {
                _timeTriggeredWaves.Add(wave); // 加入时间触发列表
            }
            else if (wave.TriggerType == WaveTriggerType.Clear)
            {
                if (!_clearTriggeredWaves.TryGetValue(wave.TriggerWaveId, out var list))
                {
                    list = new List<WaveRow>(4); // 创建触发列表
                    _clearTriggeredWaves.Add(wave.TriggerWaveId, list); // 写入映射
                }

                list.Add(wave); // 添加清空触发波次
            }
        }
    }

    /// <summary>
    /// 清理全部缓存。
    /// </summary>
    private void ClearAll() // 清理入口
    {
        _elapsedTime = 0f; // 重置运行时间
        _activeMainWaveId = 0; // 清理主线波次
        _waveMap.Clear(); // 清理波次缓存
        _waveSpawnPoolMap.Clear(); // 清理生成池缓存
        _spawnTypeMap.Clear(); // 清理生成类型缓存
        _enemyPoolMap.Clear(); // 清理敌人池缓存
        _refreshTypeMap.Clear(); // 清理刷新类型缓存
        _timeTriggeredWaves.Clear(); // 清理时间触发列表
        _clearTriggeredWaves.Clear(); // 清理清空触发映射
        _activeWaves.Clear(); // 清理活动波次
        _activeWaveMap.Clear(); // 清理活动映射
        _completedWaveIds.Clear(); // 清理完成集合
    }
}
