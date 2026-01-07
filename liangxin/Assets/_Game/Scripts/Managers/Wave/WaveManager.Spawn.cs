// 引用泛型集合命名空间，使用 List
using System.Collections.Generic; // 集合类型引用
// 引用 CYFramework 入口，使用 CY.Event/CY.Unit
using CYFramework; // 框架统一入口
// 引用 UnityEngine，使用 Vector2/Random
using UnityEngine; // Unity 引擎类型引用

public sealed partial class WaveManager // 波次管理器分部定义
{
    /// <summary>
    /// 更新所有活动波次。
    /// </summary>
    /// <param name="deltaTime">帧间隔时间。</param>
    private void UpdateActiveWaves(float deltaTime) // 活动波次更新入口
    {
        for (int i = _activeWaves.Count - 1; i >= 0; i--)
        {
            var runtime = _activeWaves[i]; // 取出运行时波次
            if (runtime == null)
            {
                _activeWaves.RemoveAt(i); // 移除空引用
                continue; // 跳过空对象
            }

            var hasRunningSpawnType = false; // 是否仍有未结束的生成类型
            var hasActiveSpawnType = false; // 是否存在可刷怪的生成类型
            for (int s = 0; s < runtime.SpawnTypeRuntimes.Count; s++)
            {
                var spawnRuntime = runtime.SpawnTypeRuntimes[s]; // 取出生成类型运行时
                if (spawnRuntime == null)
                {
                    continue; // 跳过空对象
                }

                if (spawnRuntime.SpawnRemaining <= 0f)
                {
                    continue; // 已结束则跳过
                }

                hasRunningSpawnType = true; // 标记仍有运行中的生成类型
                var remainingDelta = deltaTime; // 当前帧剩余时间
                if (spawnRuntime.PrepareRemaining > 0f)
                {
                    spawnRuntime.PrepareRemaining -= deltaTime; // 递减准备计时
                    if (spawnRuntime.PrepareRemaining > 0f)
                    {
                        continue; // 准备阶段未结束
                    }

                    remainingDelta = -spawnRuntime.PrepareRemaining; // 计算准备阶段耗尽后的剩余时间
                    spawnRuntime.PrepareRemaining = 0f; // 纠正为 0
                }

                if (spawnRuntime.SpawnRemaining > 0f)
                {
                    spawnRuntime.SpawnRemaining -= remainingDelta; // 递减刷怪计时
                    if (spawnRuntime.SpawnRemaining < 0f)
                    {
                        spawnRuntime.SpawnRemaining = 0f; // 纠正为 0
                    }
                }

                if (spawnRuntime.SpawnRemaining > 0f)
                {
                    hasActiveSpawnType = true; // 标记存在可刷怪类型
                }
            }

            if (!hasRunningSpawnType)
            {
                FinishWave(runtime); // 结束波次
                _activeWaves.RemoveAt(i); // 移除活动波次
                if (runtime.IsAssault)
                {
                    _activeAssaultWaveMap.Remove(runtime.WaveId); // 移除奇袭映射
                }
                else
                {
                    _activeWaveMap.Remove(runtime.WaveId); // 移除主线映射
                }
                if (!runtime.IsAssault && _currentWaveId == runtime.WaveId)
                {
                    _currentWaveId = 0; // 主线结束后清理显示
                }
                continue; // 继续下一个波次
            }

            if (hasActiveSpawnType && !runtime.HasSpawnStarted)
            {
                runtime.HasSpawnStarted = true; // 标记已进入刷怪阶段
                PostSpawnStarted(runtime); // 派发刷怪开始事件
            }

            if (!hasActiveSpawnType)
            {
                runtime.NextRefreshTimer = 0f; // 无可刷怪类型时保持立即刷新
                continue; // 暂停刷怪逻辑
            }

            UpdateWaveSpawns(runtime, deltaTime); // 刷怪逻辑更新
        }
    }

    /// <summary>
    /// 刷怪阶段刷新逻辑。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    /// <param name="deltaTime">帧间隔时间。</param>
    private void UpdateWaveSpawns(WaveRuntime runtime, float deltaTime) // 刷怪更新入口
    {
        if (runtime.SpawnTypeWeightTotal <= 0)
        {
            return; // 无可用生成类型时退出
        }

        runtime.NextRefreshTimer -= deltaTime; // 递减刷新计时器
        var guard = 0; // 刷新次数保护
        while (runtime.NextRefreshTimer <= 0f && guard < _maxRefreshPerUpdate)
        {
            var nextInterval = SpawnBatch(runtime); // 执行一次刷新
            runtime.NextRefreshTimer += nextInterval; // 累加下一次刷新间隔
            guard++; // 递增刷新次数
        }
    }

    /// <summary>
    /// 执行一次刷新批次，返回下一次刷新间隔。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    private float SpawnBatch(WaveRuntime runtime) // 刷新批次入口
    {
        if (!TryPickSpawnType(runtime, out var spawnTypeId))
        {
            return _minRefreshInterval; // 无生成类型时返回兜底间隔
        }

        if (runtime.IsAssault)
        {
            if (!_assaultSpawnTypeMap.TryGetValue(spawnTypeId, out var assaultType))
            {
                CY.LogWarning($"[WaveManager] 奇袭生成类型不存在，Id={spawnTypeId}"); // 输出生成类型警告
                return _minRefreshInterval; // 生成类型不存在时兜底
            }

            if (!_assaultSpawnTypeEnemyPoolMap.TryGetValue(spawnTypeId, out var assaultPool))
            {
                CY.LogWarning($"[WaveManager] 奇袭生成类型敌人池未准备，SpawnTypeId={spawnTypeId}"); // 输出敌人池警告
                return GetInterval(assaultType.IntervalMin, assaultType.IntervalMax); // 返回刷新间隔
            }

            var spawnCount = GetRandomCount(assaultType.SpawnCountMin, assaultType.SpawnCountMax); // 计算本次刷新数量
            if (spawnCount <= 0)
            {
                return GetInterval(assaultType.IntervalMin, assaultType.IntervalMax); // 无生成数量时直接返回
            }

            for (int i = 0; i < spawnCount; i++)
            {
                if (!TryPickEnemy(assaultPool, out var enemyId))
                {
                    continue; // 无可用敌人时跳过
                }

                if (!TryComputeSpawnPosition(spawnTypeId, true, out var position))
                {
                    continue; // 刷新点无效时跳过本次生成
                }

                SpawnEnemy(runtime, spawnTypeId, enemyId, position); // 生成敌人
            }

            return GetInterval(assaultType.IntervalMin, assaultType.IntervalMax); // 返回下一次刷新间隔
        }

        if (!_spawnTypeMap.TryGetValue(spawnTypeId, out var spawnType))
        {
            CY.LogWarning($"[WaveManager] 生成类型不存在，Id={spawnTypeId}"); // 输出生成类型警告
            return _minRefreshInterval; // 生成类型不存在时兜底
        }

        if (!_spawnTypeEnemyPoolMap.TryGetValue(spawnTypeId, out var enemyPool))
        {
            CY.LogWarning($"[WaveManager] 生成类型敌人池未准备，SpawnTypeId={spawnTypeId}"); // 输出敌人池警告
            return GetInterval(spawnType.IntervalMin, spawnType.IntervalMax); // 返回刷新间隔
        }

        var mainSpawnCount = GetRandomCount(spawnType.SpawnCountMin, spawnType.SpawnCountMax); // 计算本次刷新数量
        if (mainSpawnCount <= 0)
        {
            return GetInterval(spawnType.IntervalMin, spawnType.IntervalMax); // 无生成数量时直接返回
        }

        for (int i = 0; i < mainSpawnCount; i++)
        {
            if (!TryPickEnemy(enemyPool, out var enemyId))
            {
                continue; // 无可用敌人时跳过
            }

            if (!TryComputeSpawnPosition(spawnTypeId, false, out var position))
            {
                continue; // 刷新点无效时跳过本次生成
            }

            SpawnEnemy(runtime, spawnTypeId, enemyId, position); // 生成敌人
        }

        return GetInterval(spawnType.IntervalMin, spawnType.IntervalMax); // 返回下一次刷新间隔
    }

    /// <summary>
    /// 生成敌人实体并派发事件。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    /// <param name="spawnTypeId">生成类型 Id。</param>
    /// <param name="enemyId">敌人 Id。</param>
    /// <param name="position">生成位置。</param>
    private void SpawnEnemy(WaveRuntime runtime, int spawnTypeId, int enemyId, Vector2 position) // 敌人生成入口
    {
        var unitManager = CY.Unit; // 获取单位管理器
        if (unitManager == null)
        {
            CY.LogWarning("[WaveManager] UnitManager 未就绪，无法生成敌人。"); // 输出管理器警告
            return; // 无管理器时退出
        }

        if (!unitManager.TryCreateEnemy(enemyId, position, out _))
        {
            CY.LogWarning($"[WaveManager] 生成敌人失败，EnemyId={enemyId}"); // 输出生成失败日志
            return; // 生成失败时退出
        }

        var evt = new WaveSpawnedEvent
        {
            WaveId = runtime.WaveId, // 写入波次 Id
            IsAssault = runtime.IsAssault, // 写入奇袭标记
            SpawnTypeId = spawnTypeId, // 写入生成类型 Id
            EnemyId = enemyId, // 写入敌人 Id
            Position = position // 写入位置
        };
        CY.Event.Post(ref evt); // 派发刷怪事件
    }

    /// <summary>
    /// 结束波次并派发事件。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    private void FinishWave(WaveRuntime runtime) // 波次结束入口
    {
        if (runtime == null)
        {
            return; // 空对象直接退出
        }

        PostWaveFinished(runtime); // 派发结束事件
    }

    /// <summary>
    /// 是否存在无需准备即可刷怪的生成类型。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    private bool HasImmediateSpawn(WaveRuntime runtime) // 立即刷怪判定入口
    {
        if (runtime == null)
        {
            return false; // 空对象直接退出
        }

        for (int i = 0; i < runtime.SpawnTypeRuntimes.Count; i++)
        {
            var spawnRuntime = runtime.SpawnTypeRuntimes[i]; // 取出运行时
            if (spawnRuntime == null)
            {
                continue; // 跳过空对象
            }

            if (spawnRuntime.PrepareRemaining <= 0f && spawnRuntime.SpawnRemaining > 0f)
            {
                return true; // 存在无需准备的生成类型
            }
        }

        return false; // 未命中时返回 false
    }

    /// <summary>
    /// 构建运行时生成类型池（按解锁/最大波次过滤，并过滤无敌人/无刷怪时长的配置）。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    private void BuildRuntimeSpawnTypes(WaveRuntime runtime) // 运行时生成池构建入口
    {
        runtime.SpawnTypes.Clear(); // 清理旧池
        runtime.SpawnTypeWeightTotal = 0; // 重置权重总和
        runtime.SpawnTypeRuntimes.Clear(); // 清理运行时列表

        if (_spawnTypeMap.Count == 0)
        {
            return; // 无生成类型池时直接退出
        }

        foreach (var pair in _spawnTypeMap)
        {
            var spawnType = pair.Value; // 取出生成类型配置
            if (spawnType == null)
            {
                continue; // 空配置时跳过
            }

            if (spawnType.Weight <= 0)
            {
                continue; // 权重无效时跳过
            }

            if (spawnType.UnlockWave > runtime.WaveId)
            {
                continue; // 未解锁时跳过
            }

            if (spawnType.MaxWave > 0 && runtime.WaveId > spawnType.MaxWave)
            {
                continue; // 超过最大波次时跳过
            }

            if (!_spawnTypeEnemyPoolMap.ContainsKey(spawnType.Id))
            {
                CY.LogWarning($"[WaveManager] 生成类型未配置敌人列表，Id={spawnType.Id}"); // 输出敌人列表警告
                continue; // 无敌人列表时跳过
            }

            if (spawnType.SpawnDuration <= 0f)
            {
                continue; // 刷怪时长无效时跳过
            }

            runtime.SpawnTypes.Add(new WeightedId(spawnType.Id, spawnType.Weight)); // 添加到运行时池
            runtime.SpawnTypeWeightTotal += spawnType.Weight; // 累加权重
            runtime.SpawnTypeRuntimes.Add(new SpawnTypeRuntime(spawnType.Id, spawnType.PrepareDuration, spawnType.SpawnDuration)); // 添加运行时
        }
    }

    /// <summary>
    /// 构建奇袭运行时生成类型池（按波次 Id 过滤）。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    /// <param name="assaultWaveId">奇袭波次 Id。</param>
    private void BuildRuntimeAssaultSpawnTypes(WaveRuntime runtime, int assaultWaveId) // 奇袭生成池构建入口
    {
        runtime.SpawnTypes.Clear(); // 清理旧池
        runtime.SpawnTypeWeightTotal = 0; // 重置权重总和
        runtime.SpawnTypeRuntimes.Clear(); // 清理运行时列表

        if (_assaultSpawnTypeMap.Count == 0)
        {
            return; // 无奇袭生成类型时直接退出
        }

        foreach (var pair in _assaultSpawnTypeMap)
        {
            var spawnType = pair.Value; // 取出奇袭生成类型配置
            if (spawnType == null)
            {
                continue; // 空配置时跳过
            }

            if (spawnType.WaveId != assaultWaveId)
            {
                continue; // 非目标奇袭波次时跳过
            }

            if (spawnType.Weight <= 0)
            {
                continue; // 权重无效时跳过
            }

            if (!_assaultSpawnTypeEnemyPoolMap.ContainsKey(spawnType.Id))
            {
                CY.LogWarning($"[WaveManager] 奇袭生成类型未配置敌人列表，Id={spawnType.Id}"); // 输出敌人列表警告
                continue; // 无敌人列表时跳过
            }

            if (spawnType.SpawnDuration <= 0f)
            {
                continue; // 刷怪时长无效时跳过
            }

            runtime.SpawnTypes.Add(new WeightedId(spawnType.Id, spawnType.Weight)); // 添加到运行时池
            runtime.SpawnTypeWeightTotal += spawnType.Weight; // 累加权重
            runtime.SpawnTypeRuntimes.Add(new SpawnTypeRuntime(spawnType.Id, spawnType.PrepareDuration, spawnType.SpawnDuration)); // 添加运行时
        }
    }

    /// <summary>
    /// 尝试从运行时生成类型池中随机选取一个生成类型。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    /// <param name="spawnTypeId">输出生成类型 Id。</param>
    private bool TryPickSpawnType(WaveRuntime runtime, out int spawnTypeId) // 生成类型抽取入口
    {
        spawnTypeId = 0; // 默认输出
        if (runtime.SpawnTypeWeightTotal <= 0 || runtime.SpawnTypes.Count == 0)
        {
            return false; // 无可用生成类型时失败
        }

        var totalWeight = 0; // 活动权重总和
        for (int i = 0; i < runtime.SpawnTypes.Count; i++)
        {
            var entry = runtime.SpawnTypes[i]; // 取出条目
            var spawnRuntime = runtime.SpawnTypeRuntimes[i]; // 取出运行时
            if (spawnRuntime == null)
            {
                continue; // 跳过空对象
            }

            if (spawnRuntime.PrepareRemaining > 0f || spawnRuntime.SpawnRemaining <= 0f)
            {
                continue; // 未进入刷怪或已结束时跳过
            }

            totalWeight += entry.Weight; // 累加可用权重
        }

        if (totalWeight <= 0)
        {
            return false; // 无可用生成类型时失败
        }

        var roll = Random.Range(1, totalWeight + 1); // 随机权重点
        var cumulative = 0; // 权重累加
        for (int i = 0; i < runtime.SpawnTypes.Count; i++)
        {
            var entry = runtime.SpawnTypes[i]; // 取出条目
            var spawnRuntime = runtime.SpawnTypeRuntimes[i]; // 取出运行时
            if (spawnRuntime == null)
            {
                continue; // 跳过空对象
            }

            if (spawnRuntime.PrepareRemaining > 0f || spawnRuntime.SpawnRemaining <= 0f)
            {
                continue; // 未进入刷怪或已结束时跳过
            }

            cumulative += entry.Weight; // 累加权重
            if (roll <= cumulative)
            {
                spawnTypeId = entry.Id; // 命中生成类型
                return true; // 返回成功
            }
        }

        return false; // 未命中时失败
    }

    /// <summary>
    /// 尝试从敌人池中按权重抽取敌人。
    /// </summary>
    /// <param name="pool">敌人池。</param>
    /// <param name="enemyId">输出敌人 Id。</param>
    private bool TryPickEnemy(SpawnTypeEnemyPool pool, out int enemyId) // 敌人抽取入口
    {
        enemyId = 0; // 默认输出
        if (pool == null || pool.TotalWeight <= 0 || pool.Entries.Count == 0)
        {
            return false; // 敌人池无效时失败
        }

        var roll = Random.Range(1, pool.TotalWeight + 1); // 随机权重点
        var cumulative = 0; // 权重累加
        for (int i = 0; i < pool.Entries.Count; i++)
        {
            var entry = pool.Entries[i]; // 取出敌人条目
            cumulative += entry.Weight; // 累加权重
            if (roll <= cumulative)
            {
                enemyId = entry.Id; // 命中敌人 Id
                return true; // 返回成功
            }
        }

        return false; // 未命中时失败
    }

    /// <summary>
    /// 尝试计算生成位置（仅允许命名点，失败时返回 false）。
    /// </summary>
    /// <param name="spawnTypeId">生成类型 Id。</param>
    /// <param name="isAssault">是否奇袭生成类型。</param>
    /// <param name="position">输出位置。</param>
    private bool TryComputeSpawnPosition(int spawnTypeId, bool isAssault, out Vector2 position) // 生成位置计算入口
    {
        position = Vector2.zero; // 默认输出
        if (spawnTypeId <= 0)
        {
            CY.LogError("[WaveManager] 生成类型 Id 非法，无法计算刷新点。"); // 输出生成类型错误
            return false; // 生成类型非法时失败
        }

        if (isAssault)
        {
            if (!_assaultSpawnTypePointPoolMap.TryGetValue(spawnTypeId, out var assaultPool) || assaultPool.PointIds.Count == 0)
            {
                CY.LogError($"[WaveManager] 奇袭生成类型未配置刷新点，SpawnTypeId={spawnTypeId}"); // 输出刷新点缺失错误
                return false; // 未配置刷新点时失败
            }

            var index = Random.Range(0, assaultPool.PointIds.Count); // 随机索引
            var pointId = assaultPool.PointIds[index]; // 取出点 Id
            if (!WaveSpawnPoint.TryGetRandomPoint(pointId, out position))
            {
                CY.LogError($"[WaveManager] 刷新点未注册或已失效，PointId={pointId}, SpawnTypeId={spawnTypeId}"); // 输出刷新点无效错误
                return false; // 刷新点无效时失败
            }

            return true; // 命中有效刷新点时返回成功
        }

        if (!_spawnTypePointPoolMap.TryGetValue(spawnTypeId, out var pool) || pool.PointIds.Count == 0)
        {
            CY.LogError($"[WaveManager] 生成类型未配置刷新点，SpawnTypeId={spawnTypeId}"); // 输出刷新点缺失错误
            return false; // 未配置刷新点时失败
        }

        var normalIndex = Random.Range(0, pool.PointIds.Count); // 随机索引
        var normalPointId = pool.PointIds[normalIndex]; // 取出点 Id
        if (!WaveSpawnPoint.TryGetRandomPoint(normalPointId, out position))
        {
            CY.LogError($"[WaveManager] 刷新点未注册或已失效，PointId={normalPointId}, SpawnTypeId={spawnTypeId}"); // 输出刷新点无效错误
            return false; // 刷新点无效时失败
        }

        return true; // 命中有效刷新点时返回成功
    }

    /// <summary>
    /// 获取刷新间隔（带兜底最小值）。
    /// </summary>
    /// <param name="spawnType">生成类型。</param>
    private float GetInterval(float min, float max) // 间隔获取入口
    {
        var interval = GetRandomInterval(min, max); // 获取间隔
        if (interval <= 0f)
        {
            interval = _minRefreshInterval; // 使用兜底间隔
        }

        return interval; // 返回间隔
    }

    /// <summary>
    /// 获取随机间隔。
    /// </summary>
    /// <param name="min">最小间隔。</param>
    /// <param name="max">最大间隔。</param>
    private float GetRandomInterval(float min, float max) // 随机间隔入口
    {
        if (max < min)
        {
            max = min; // 保证最大值不小于最小值
        }

        return Random.Range(min, max); // 返回随机间隔
    }

    /// <summary>
    /// 获取随机数量（包含上下限）。
    /// </summary>
    /// <param name="min">最小数量。</param>
    /// <param name="max">最大数量。</param>
    private int GetRandomCount(int min, int max) // 随机数量入口
    {
        if (max < min)
        {
            max = min; // 保证最大值不小于最小值
        }

        if (max <= 0)
        {
            return 0; // 数量无效时返回 0
        }

        return Random.Range(min, max + 1); // 返回随机数量
    }

    /// <summary>
    /// 派发准备阶段开始事件。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    private void PostPrepareStarted(WaveRuntime runtime) // 事件派发入口
    {
        var evt = new WavePrepareStartedEvent
        {
            WaveId = runtime.WaveId, // 写入波次 Id
            IsAssault = runtime.IsAssault // 写入奇袭标记
        };
        CY.Event.Post(ref evt); // 派发事件
    }

    /// <summary>
    /// 派发刷怪阶段开始事件。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    private void PostSpawnStarted(WaveRuntime runtime) // 事件派发入口
    {
        var evt = new WaveSpawnStartedEvent
        {
            WaveId = runtime.WaveId, // 写入波次 Id
            IsAssault = runtime.IsAssault // 写入奇袭标记
        };
        CY.Event.Post(ref evt); // 派发事件
    }

    /// <summary>
    /// 派发波次结束事件。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    private void PostWaveFinished(WaveRuntime runtime) // 事件派发入口
    {
        var evt = new WaveFinishedEvent
        {
            WaveId = runtime.WaveId, // 写入波次 Id
            IsAssault = runtime.IsAssault // 写入奇袭标记
        };
        CY.Event.Post(ref evt); // 派发事件
    }

    /// <summary>
    /// 派发暂停事件。
    /// </summary>
    /// <param name="paused">是否暂停。</param>
    private void PostPauseEvent(bool paused) // 事件派发入口
    {
        var evt = new WavePauseEvent
        {
            IsPaused = paused // 写入暂停状态
        };
        CY.Event.Post(ref evt); // 派发事件
    }
}
