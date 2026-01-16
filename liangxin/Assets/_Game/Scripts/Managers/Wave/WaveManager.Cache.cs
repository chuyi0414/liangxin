// 引用泛型集合命名空间，使用 List
using System.Collections.Generic; // 集合类型引用
// 引用 CYFramework 入口，使用 CY.Data/CY.Log
using CYFramework; // 框架统一入口
// 引用数据表命名空间，使用 DataTable
using CYFramework.Core.DataTable; // 数据表类型引用

public sealed partial class WaveManager // 波次管理器分部定义
{
    /// <summary>
    /// 构建波次编排缓存（依赖数据表已加载）。
    /// </summary>
    private bool TryBuildWaveCache() // 缓存构建入口
    {
        ClearAll(); // 清空旧缓存

        if (!CY.Data.HasDataTable(WavePlanTableName)) // 波次计划表存在判定
        {
            CY.LogWarning("[WaveManager] 波次计划表未加载。"); // 输出未加载提示
            return false; // 未加载时失败
        }

        if (!CY.Data.HasDataTable(WaveTrackTableName)) // 波次轨道表存在判定
        {
            CY.LogWarning("[WaveManager] 波次轨道表未加载。"); // 输出未加载提示
            return false; // 未加载时失败
        }

        if (!CY.Data.HasDataTable(WaveSpawnGroupTableName)) // 刷怪组表存在判定
        {
            CY.LogWarning("[WaveManager] 刷怪组表未加载。"); // 输出未加载提示
            return false; // 未加载时失败
        }

        var planTable = CY.Data.GetDataTable<WavePlanRow>(WavePlanTableName); // 获取波次计划表
        if (planTable == null || planTable.Count == 0) // 空表判定
        {
            CY.LogWarning("[WaveManager] 波次计划表为空。"); // 输出空表提示
            return false; // 空表时失败
        }

        var trackTable = CY.Data.GetDataTable<WaveTrackRow>(WaveTrackTableName); // 获取波次轨道表
        if (trackTable == null || trackTable.Count == 0) // 空表判定
        {
            CY.LogWarning("[WaveManager] 波次轨道表为空。"); // 输出空表提示
            return false; // 空表时失败
        }

        var groupTable = CY.Data.GetDataTable<WaveSpawnGroupRow>(WaveSpawnGroupTableName); // 获取刷怪组表
        if (groupTable == null || groupTable.Count == 0) // 空表判定
        {
            CY.LogWarning("[WaveManager] 刷怪组表为空。"); // 输出空表提示
            return false; // 空表时失败
        }

        CacheWavePlans(planTable); // 缓存波次计划
        CacheWaveTracks(trackTable); // 缓存波次轨道
        CacheSpawnGroups(groupTable); // 缓存刷怪组
        return true; // 构建成功
    }

    /// <summary>
    /// 缓存波次计划表行。
    /// </summary>
    /// <param name="table">波次计划表。</param>
    private void CacheWavePlans(DataTable<WavePlanRow> table) // 波次计划缓存入口
    {
        var rows = table.GetAllRows(); // 获取全部行
        for (int i = 0; i < rows.Count; i++) // 遍历行
        {
            var row = rows[i]; // 取出行
            if (row == null) // 空行判定
            {
                continue; // 跳过空行
            }

            _wavePlanMap[row.WaveId] = row; // 写入缓存
        }
    }

    /// <summary>
    /// 缓存波次轨道表行。
    /// </summary>
    /// <param name="table">波次轨道表。</param>
    private void CacheWaveTracks(DataTable<WaveTrackRow> table) // 波次轨道缓存入口
    {
        var rows = table.GetAllRows(); // 获取全部行
        if (rows == null || rows.Count == 0) // 行列表判定
        {
            return; // 无行时直接退出
        }

        var trackMap = new Dictionary<int, WaveTrackRow>(rows.Count); // 轨道临时缓存
        for (int i = 0; i < rows.Count; i++) // 遍历行
        {
            var row = rows[i]; // 取出行
            if (row == null) // 空行判定
            {
                continue; // 跳过空行
            }

            trackMap[row.TrackId] = row; // 写入轨道缓存
        }

        BuildWaveTracksFromPlan(trackMap); // 按 WavePlan 组装轨道
    }

    /// <summary>
    /// 按 WavePlan.TrackIds 构建波次轨道映射。
    /// </summary>
    /// <param name="trackMap">轨道缓存（TrackId -> Row）。</param>
    private void BuildWaveTracksFromPlan(Dictionary<int, WaveTrackRow> trackMap) // 轨道组装入口
    {
        if (trackMap == null || trackMap.Count == 0) // 轨道缓存判定
        {
            return; // 无轨道时直接退出
        }

        foreach (var pair in _wavePlanMap) // 遍历波次计划
        {
            var plan = pair.Value; // 获取计划行
            if (plan == null) // 空计划判定
            {
                continue; // 跳过空计划
            }

            if (string.IsNullOrEmpty(plan.TrackIds)) // 轨道列表为空判定
            {
                CY.LogWarning($"[WaveManager] 波次未配置轨道列表，WaveId={plan.WaveId}"); // 输出警告
                continue; // 空列表时跳过
            }

            var tracks = new List<WaveTrackRow>(4); // 创建轨道列表
            ParseTrackIdList(plan.TrackIds, trackMap, tracks); // 解析轨道列表
            if (tracks.Count == 0) // 解析结果判定
            {
                CY.LogWarning($"[WaveManager] 波次轨道列表无有效轨道，WaveId={plan.WaveId}"); // 输出警告
                continue; // 无有效轨道时跳过
            }

            _waveTrackMap[plan.WaveId] = tracks; // 写入波次轨道映射
        }
    }

    /// <summary>
    /// 解析轨道 Id 列表（格式：TrackId|TrackId）。
    /// </summary>
    /// <param name="value">轨道列表字符串。</param>
    /// <param name="trackMap">轨道缓存。</param>
    /// <param name="output">输出列表。</param>
    private void ParseTrackIdList(string value, Dictionary<int, WaveTrackRow> trackMap, List<WaveTrackRow> output) // 轨道列表解析入口
    {
        if (string.IsNullOrEmpty(value) || trackMap == null || output == null) // 参数判定
        {
            return; // 参数无效时退出
        }

        var number = 0; // 当前数字
        var hasNumber = false; // 数字标记
        for (int i = 0; i <= value.Length; i++) // 遍历字符串
        {
            var c = i < value.Length ? value[i] : '|'; // 末尾补分隔符
            if (c >= '0' && c <= '9') // 数字判定
            {
                number = number * 10 + (c - '0'); // 累加数字
                hasNumber = true; // 标记有数字
                continue; // 继续读取
            }

            if (c == '|' || c == ',' || i == value.Length) // 分隔符判定
            {
                if (!hasNumber) // 无数字判定
                {
                    continue; // 无数字时跳过
                }

                var trackId = number; // 记录轨道 Id
                if (trackId > 0 && trackMap.TryGetValue(trackId, out var row) && row != null) // 轨道存在判定
                {
                    output.Add(row); // 添加轨道
                }
                else
                {
                    CY.LogWarning($"[WaveManager] 轨道不存在或无效，TrackId={trackId}"); // 输出警告
                }

                number = 0; // 重置数字
                hasNumber = false; // 清空标记
                continue; // 继续解析
            }
        }
    }

    /// <summary>
    /// 缓存刷怪组表行。
    /// </summary>
    /// <param name="table">刷怪组表。</param>
    private void CacheSpawnGroups(DataTable<WaveSpawnGroupRow> table) // 刷怪组缓存入口
    {
        var rows = table.GetAllRows(); // 获取全部行
        for (int i = 0; i < rows.Count; i++) // 遍历行
        {
            var row = rows[i]; // 取出行
            if (row == null) // 空行判定
            {
                continue; // 跳过空行
            }

            _spawnGroupMap[row.GroupId] = row; // 写入刷怪组配置缓存
            CacheSpawnGroupRuntime(row); // 构建刷怪组运行时
        }
    }

    /// <summary>
    /// 缓存刷怪组运行时数据。
    /// </summary>
    /// <param name="row">刷怪组配置。</param>
    private void CacheSpawnGroupRuntime(WaveSpawnGroupRow row) // 刷怪组运行时缓存入口
    {
        if (row == null) // 空行判定
        {
            return; // 空行直接退出
        }

        if (_spawnGroupRuntimeMap.ContainsKey(row.GroupId)) // 已缓存判定
        {
            return; // 已缓存时退出
        }

        var runtime = new SpawnGroupRuntime(); // 创建运行时
        runtime.Row = row; // 写入配置
        runtime.EnemyPool = new SpawnGroupEnemyPool(); // 创建敌人池
        ParseEnemyList(row.EnemyList, runtime.EnemyPool); // 解析敌人列表
        if (runtime.EnemyPool.TotalWeight <= 0 || runtime.EnemyPool.Entries.Count == 0) // 敌人池有效性判定
        {
            CY.LogWarning($"[WaveManager] 刷怪组敌人列表无效，GroupId={row.GroupId}"); // 输出警告
        }

        var pointMode = (WavePointMode)row.PointMode; // 解析刷新点模式
        if (pointMode == WavePointMode.PointId) // 命名点模式判定
        {
            if (string.IsNullOrEmpty(row.PointId)) // 刷新点为空判定
            {
                CY.LogWarning($"[WaveManager] 刷怪组未配置刷新点，GroupId={row.GroupId}"); // 输出警告
            }
            else
            {
                ParsePointIdList(row.PointId, runtime.PointIds); // 解析刷新点列表
            }
        }
        else if (pointMode == WavePointMode.AreaId) // 区域模式判定
        {
            if (string.IsNullOrEmpty(row.AreaId)) // 区域 Id 为空判定
            {
                CY.LogWarning($"[WaveManager] 刷怪组未配置区域 Id，GroupId={row.GroupId}"); // 输出警告
            }
        }

        _spawnGroupRuntimeMap.Add(row.GroupId, runtime); // 写入运行时缓存
    }

    /// <summary>
    /// 解析刷新点列表（格式：a|b|c）。
    /// </summary>
    /// <param name="value">刷新点列表字符串。</param>
    /// <param name="output">输出列表。</param>
    private void ParsePointIdList(string value, List<string> output) // 刷新点列表解析入口
    {
        if (string.IsNullOrEmpty(value) || output == null) // 参数判定
        {
            return; // 无效输入直接退出
        }

        var start = 0; // 片段起点
        for (int i = 0; i <= value.Length; i++) // 遍历字符串
        {
            var isEnd = i == value.Length || value[i] == '|'; // 判断分隔符
            if (!isEnd) // 非分隔符判定
            {
                continue; // 非分隔符时继续
            }

            var length = i - start; // 计算片段长度
            if (length > 0) // 有效长度判定
            {
                var id = value.Substring(start, length).Trim(); // 提取 Id
                if (!string.IsNullOrEmpty(id)) // 非空判定
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
    private void ParseEnemyList(string enemyList, SpawnGroupEnemyPool pool) // 敌人列表解析入口
    {
        if (string.IsNullOrEmpty(enemyList) || pool == null) // 参数判定
        {
            return; // 无效输入直接退出
        }

        var length = enemyList.Length; // 字符串长度
        var number = 0; // 当前数字
        var hasNumber = false; // 数字标记
        var parsingWeight = false; // 是否解析权重
        var enemyId = 0; // 当前敌人 Id

        for (int i = 0; i <= length; i++) // 遍历字符串
        {
            var c = i < length ? enemyList[i] : '|'; // 末尾补分隔符
            if (c >= '0' && c <= '9') // 数字判定
            {
                number = number * 10 + (c - '0'); // 累积数字
                hasNumber = true; // 标记有数字
                continue; // 继续读取
            }

            if (c == ':') // 权重分隔符判定
            {
                if (!hasNumber) // 无数字判定
                {
                    number = 0; // 重置数字
                    continue; // 跳过
                }

                enemyId = number; // 写入敌人 Id
                number = 0; // 重置数字
                hasNumber = false; // 清空标记
                parsingWeight = true; // 开始解析权重
                continue; // 继续解析
            }

            if (c == '|' || c == ',' || i == length) // 分隔符判定
            {
                if (!hasNumber) // 无数字判定
                {
                    parsingWeight = false; // 重置权重解析
                    continue; // 没有数字时跳过
                }

                if (!parsingWeight) // 未解析权重判定
                {
                    enemyId = number; // 使用当前数字作为敌人 Id
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
    private void AddEnemyEntry(SpawnGroupEnemyPool pool, int enemyId, int weight) // 敌人条目添加入口
    {
        if (pool == null) // 空池判定
        {
            return; // 空池直接退出
        }

        if (enemyId <= 0 || weight <= 0) // 参数合法性判定
        {
            return; // 无效数据直接忽略
        }

        pool.Entries.Add(new WeightedId(enemyId, weight)); // 添加条目
        pool.TotalWeight += weight; // 累加权重
    }

    /// <summary>
    /// 清理全部缓存与运行时。
    /// </summary>
    private void ClearAll() // 清理入口
    {
        _wavePlanMap.Clear(); // 清理波次计划缓存
        _waveTrackMap.Clear(); // 清理波次轨道缓存
        _spawnGroupMap.Clear(); // 清理刷怪组缓存
        _spawnGroupRuntimeMap.Clear(); // 清理刷怪组运行时缓存
        ResetRuntimeInternal(); // 清理运行时状态
        _lastWaveId = 0; // 重置最近波次 Id
        _completedWaveCount = 0; // 重置已完成波次数
    }
}
