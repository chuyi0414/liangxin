// 引用泛型集合命名空间，使用 List
using System.Collections.Generic; // 集合类型引用
// 引用 CYFramework 入口，使用 CY.Data/CY.Log
using CYFramework; // 框架统一入口
// 引用数据表命名空间，使用 DataTable
using CYFramework.Core.DataTable; // 数据表类型引用

public sealed partial class WaveManager // 波次管理器分部定义
{
    /// <summary>
    /// 构建生成类型缓存（依赖数据表已加载）。
    /// </summary>
    private bool TryBuildWaveCache() // 缓存构建入口
    {
        ClearAll(); // 清空旧缓存

        if (!CY.Data.HasDataTable(SpawnTypeTableName))
        {
            CY.LogWarning("[WaveManager] 生成类型表未加载。"); // 输出未加载提示
            return false; // 未加载时失败
        }

        var spawnTypeTable = CY.Data.GetDataTable<SpawnTypeRow>(SpawnTypeTableName); // 获取生成类型表
        if (spawnTypeTable == null || spawnTypeTable.Count == 0)
        {
            CY.LogWarning("[WaveManager] 生成类型表为空。"); // 输出空表提示
            return false; // 空表时失败
        }

        CacheSpawnTypes(spawnTypeTable); // 缓存生成类型表
        TryCacheAssaultSpawnTypes(); // 尝试缓存奇袭生成类型
        return true; // 构建成功
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
            CacheSpawnTypeEnemyPool(row); // 缓存生成类型敌人池
            CacheSpawnTypePointPool(row); // 缓存生成类型刷新点池
        }
    }

    /// <summary>
    /// 尝试缓存奇袭生成类型表。
    /// </summary>
    private void TryCacheAssaultSpawnTypes() // 奇袭生成类型缓存入口
    {
        if (!CY.Data.HasDataTable(AssaultSpawnTypeTableName))
        {
            CY.LogWarning("[WaveManager] 未加载奇袭生成类型表，将跳过奇袭刷怪。"); // 输出提示
            return; // 允许缺失
        }

        var assaultTable = CY.Data.GetDataTable<AssaultSpawnTypeRow>(AssaultSpawnTypeTableName); // 获取奇袭生成类型表
        if (assaultTable == null || assaultTable.Count == 0)
        {
            CY.LogWarning("[WaveManager] 奇袭生成类型表为空，将跳过奇袭刷怪。"); // 输出提示
            return; // 允许空表
        }

        CacheAssaultSpawnTypes(assaultTable); // 缓存奇袭生成类型表
    }

    /// <summary>
    /// 缓存奇袭生成类型表行。
    /// </summary>
    /// <param name="table">奇袭生成类型表。</param>
    private void CacheAssaultSpawnTypes(DataTable<AssaultSpawnTypeRow> table) // 奇袭生成类型缓存入口
    {
        _assaultWaveIdList.Clear(); // 清理旧奇袭波次列表
        _assaultWaveIdSet.Clear(); // 清理旧去重集合
        var rows = table.GetAllRows(); // 获取所有行
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; // 取出行
            if (row == null)
            {
                continue; // 跳过空行
            }

            _assaultSpawnTypeMap[row.Id] = row; // 写入缓存
            CacheAssaultSpawnTypeEnemyPool(row); // 缓存奇袭敌人池
            CacheAssaultSpawnTypePointPool(row); // 缓存奇袭刷新点池
            if (row.WaveId > 0 && _assaultWaveIdSet.Add(row.WaveId))
            {
                _assaultWaveIdList.Add(row.WaveId); // 收集奇袭波次 Id
            }
        }
    }

    /// <summary>
    /// 缓存奇袭生成类型的敌人池。
    /// </summary>
    /// <param name="row">奇袭生成类型数据。</param>
    private void CacheAssaultSpawnTypeEnemyPool(AssaultSpawnTypeRow row) // 奇袭敌人池缓存入口
    {
        if (row == null)
        {
            return; // 空行直接退出
        }

        if (_assaultSpawnTypeEnemyPoolMap.ContainsKey(row.Id))
        {
            return; // 已缓存时直接退出
        }

        if (string.IsNullOrEmpty(row.EnemyList))
        {
            CY.LogWarning($"[WaveManager] 奇袭生成类型未配置敌人列表，Id={row.Id}"); // 输出警告
            return; // 无敌人列表时退出
        }

        var pool = new SpawnTypeEnemyPool(); // 创建敌人池
        ParseEnemyList(row.EnemyList, pool); // 解析敌人列表
        if (pool.TotalWeight <= 0 || pool.Entries.Count == 0)
        {
            CY.LogWarning($"[WaveManager] 奇袭生成类型敌人列表无效，Id={row.Id}"); // 输出警告
            return; // 列表无效时退出
        }

        _assaultSpawnTypeEnemyPoolMap.Add(row.Id, pool); // 写入缓存
    }

    /// <summary>
    /// 缓存奇袭生成类型的刷新点池。
    /// </summary>
    /// <param name="row">奇袭生成类型数据。</param>
    private void CacheAssaultSpawnTypePointPool(AssaultSpawnTypeRow row) // 奇袭刷新点池缓存入口
    {
        if (row == null)
        {
            return; // 空行直接退出
        }

        if (_assaultSpawnTypePointPoolMap.ContainsKey(row.Id))
        {
            return; // 已缓存时直接退出
        }

        if (string.IsNullOrEmpty(row.PointId))
        {
            return; // 未配置刷新点时跳过
        }

        var pool = new SpawnTypePointPool(); // 创建刷新点池
        ParsePointIdList(row.PointId, pool.PointIds); // 解析刷新点列表
        if (pool.PointIds.Count == 0)
        {
            return; // 列表为空时退出
        }

        _assaultSpawnTypePointPoolMap.Add(row.Id, pool); // 写入缓存
    }
    /// <summary>
    /// 缓存生成类型的敌人池。
    /// </summary>
    /// <param name="row">生成类型数据。</param>
    private void CacheSpawnTypeEnemyPool(SpawnTypeRow row) // 生成类型敌人池缓存入口
    {
        if (row == null)
        {
            return; // 空行直接退出
        }

        if (_spawnTypeEnemyPoolMap.ContainsKey(row.Id))
        {
            return; // 已缓存时直接退出
        }

        if (string.IsNullOrEmpty(row.EnemyList))
        {
            CY.LogWarning($"[WaveManager] 生成类型未配置敌人列表，Id={row.Id}"); // 输出警告
            return; // 无敌人列表时退出
        }

        var pool = new SpawnTypeEnemyPool(); // 创建敌人池
        ParseEnemyList(row.EnemyList, pool); // 解析敌人列表
        if (pool.TotalWeight <= 0 || pool.Entries.Count == 0)
        {
            CY.LogWarning($"[WaveManager] 生成类型敌人列表无效，Id={row.Id}"); // 输出警告
            return; // 列表无效时退出
        }

        _spawnTypeEnemyPoolMap.Add(row.Id, pool); // 写入缓存
    }

    /// <summary>
    /// 缓存生成类型的刷新点池。
    /// </summary>
    /// <param name="row">生成类型数据。</param>
    private void CacheSpawnTypePointPool(SpawnTypeRow row) // 生成类型刷新点池缓存入口
    {
        if (row == null)
        {
            return; // 空行直接退出
        }

        if (_spawnTypePointPoolMap.ContainsKey(row.Id))
        {
            return; // 已缓存时直接退出
        }

        if (string.IsNullOrEmpty(row.PointId))
        {
            return; // 未配置刷新点时跳过
        }

        var pool = new SpawnTypePointPool(); // 创建刷新点池
        ParsePointIdList(row.PointId, pool.PointIds); // 解析刷新点列表
        if (pool.PointIds.Count == 0)
        {
            return; // 列表为空时退出
        }

        _spawnTypePointPoolMap.Add(row.Id, pool); // 写入缓存
    }

    /// <summary>
    /// 解析刷新点列表（格式：a|b|c）。
    /// </summary>
    /// <param name="value">刷新点列表字符串。</param>
    /// <param name="output">输出列表。</param>
    private void ParsePointIdList(string value, List<string> output) // 刷新点列表解析入口
    {
        if (string.IsNullOrEmpty(value) || output == null)
        {
            return; // 无效输入直接退出
        }

        var start = 0; // 片段起点
        for (int i = 0; i <= value.Length; i++)
        {
            var isEnd = i == value.Length || value[i] == '|'; // 判断分隔符
            if (!isEnd)
            {
                continue; // 非分隔符时继续
            }

            var length = i - start; // 计算片段长度
            if (length > 0)
            {
                var id = value.Substring(start, length).Trim(); // 提取 Id
                if (!string.IsNullOrEmpty(id))
                {
                    output.Add(id); // 添加刷新点 Id
                }
            }

            start = i + 1; // 更新起点
        }
    }

    /// <summary>
    /// 解析敌人列表字符串（格式：EnemyId:Weight|EnemyId:Weight）。
    /// </summary>
    /// <param name="enemyList">敌人列表字符串。</param>
    /// <param name="pool">输出敌人池。</param>
    private void ParseEnemyList(string enemyList, SpawnTypeEnemyPool pool) // 敌人列表解析入口
    {
        if (string.IsNullOrEmpty(enemyList) || pool == null)
        {
            return; // 无效输入直接退出
        }

        var length = enemyList.Length; // 字符串长度
        var number = 0; // 当前数字
        var hasNumber = false; // 数字标记
        var parsingWeight = false; // 是否解析权重
        var enemyId = 0; // 当前敌人 Id

        for (int i = 0; i <= length; i++)
        {
            var c = i < length ? enemyList[i] : '|'; // 末尾补一个分隔符
            if (c >= '0' && c <= '9')
            {
                number = number * 10 + (c - '0'); // 累积数字
                hasNumber = true; // 标记有数字
                continue; // 继续读取
            }

            if (c == ':')
            {
                if (!hasNumber)
                {
                    number = 0; // 无数字时重置
                    continue; // 跳过
                }

                enemyId = number; // 写入敌人 Id
                number = 0; // 重置数字
                hasNumber = false; // 清空标记
                parsingWeight = true; // 开始解析权重
                continue; // 继续解析
            }

            if (c == '|' || c == ',' || i == length)
            {
                if (!hasNumber)
                {
                    parsingWeight = false; // 重置权重解析
                    continue; // 没有数字时跳过
                }

                if (!parsingWeight)
                {
                    enemyId = number; // 未解析权重时用当前数字作为敌人 Id
                    AddEnemyEntry(pool, enemyId, 1); // 默认权重 1
                }
                else
                {
                    AddEnemyEntry(pool, enemyId, number); // 使用解析的权重
                }

                number = 0; // 重置数字
                hasNumber = false; // 清空标记
                parsingWeight = false; // 重置权重解析
                continue; // 继续解析
            }
        }
    }

    /// <summary>
    /// 添加敌人条目（权重<=0 自动忽略）。
    /// </summary>
    /// <param name="pool">敌人池。</param>
    /// <param name="enemyId">敌人 Id。</param>
    /// <param name="weight">权重。</param>
    private void AddEnemyEntry(SpawnTypeEnemyPool pool, int enemyId, int weight) // 敌人条目添加入口
    {
        if (pool == null)
        {
            return; // 空池直接退出
        }

        if (enemyId <= 0 || weight <= 0)
        {
            return; // 无效数据直接忽略
        }

        pool.Entries.Add(new WeightedId(enemyId, weight)); // 添加条目
        pool.TotalWeight += weight; // 累加权重
    }

    /// <summary>
    /// 清理全部缓存。
    /// </summary>
    private void ClearAll() // 清理入口
    {
        _currentWaveId = 0; // 重置当前波次
        _lastWaveId = 0; // 重置最近波次
        _spawnTypeMap.Clear(); // 清理生成类型缓存
        _spawnTypeEnemyPoolMap.Clear(); // 清理敌人池缓存
        _spawnTypePointPoolMap.Clear(); // 清理刷新点池缓存
        _assaultSpawnTypeMap.Clear(); // 清理奇袭生成类型缓存
        _assaultSpawnTypeEnemyPoolMap.Clear(); // 清理奇袭敌人池缓存
        _assaultSpawnTypePointPoolMap.Clear(); // 清理奇袭刷新点池缓存
        _assaultWaveIdList.Clear(); // 清理奇袭波次列表
        _assaultWaveIdSet.Clear(); // 清理奇袭波次去重集合
        _activeWaves.Clear(); // 清理活动波次
        _activeWaveMap.Clear(); // 清理活动映射
        _activeAssaultWaveMap.Clear(); // 清理奇袭映射
    }
}
