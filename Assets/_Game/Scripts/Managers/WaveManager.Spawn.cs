// 引用 CYFramework 入口，使用 CY.Event/CY.Unit
using CYFramework; // 框架统一入口
// 引用 UnityEngine，使用 Vector2/Random
using UnityEngine; // Unity 引擎类型引用

public sealed partial class WaveManager // 波次管理器分部定义
{
    /// <summary>
    /// 更新时间触发波次列表。
    /// </summary>
    private void UpdateTimeTriggeredWaves() // 时间触发更新入口
    {
        for (int i = _timeTriggeredWaves.Count - 1; i >= 0; i--)
        {
            var wave = _timeTriggeredWaves[i]; // 取出波次
            if (wave == null)
            {
                _timeTriggeredWaves.RemoveAt(i); // 移除空引用
                continue; // 跳过空波次
            }

            if (_elapsedTime < wave.TriggerTime)
            {
                continue; // 未到触发时间时跳过
            }

            if (StartWaveInternal(wave))
            {
                _timeTriggeredWaves.RemoveAt(i); // 触发成功后移除
            }
        }
    }

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

            if (runtime.IsPreparePhase)
            {
                runtime.PrepareRemaining -= deltaTime; // 递减准备计时
                if (runtime.PrepareRemaining <= 0f)
                {
                    runtime.IsPreparePhase = false; // 切换到刷怪阶段
                    runtime.SpawnRemaining = runtime.Config.SpawnDuration; // 重置刷怪时长
                    runtime.NextRefreshTimer = 0f; // 首次刷新立即触发
                    PostSpawnStarted(runtime); // 派发刷怪开始事件
                }

                continue; // 准备阶段不执行刷怪
            }

            runtime.SpawnRemaining -= deltaTime; // 递减刷怪计时
            UpdateWaveSpawns(runtime, deltaTime); // 刷怪逻辑更新
            if (runtime.SpawnRemaining > 0f)
            {
                continue; // 刷怪阶段未结束
            }

            FinishWave(runtime); // 结束波次
            _activeWaves.RemoveAt(i); // 移除活动波次
            _activeWaveMap.Remove(runtime.WaveId); // 移除映射
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
        while (runtime.NextRefreshTimer <= 0f && runtime.SpawnRemaining > 0f && guard < _maxRefreshPerUpdate)
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

        if (!_spawnTypeMap.TryGetValue(spawnTypeId, out var spawnType))
        {
            CY.LogWarning($"[WaveManager] 生成类型不存在，Id={spawnTypeId}"); // 输出生成类型警告
            return _minRefreshInterval; // 生成类型不存在时兜底
        }

        if (!_enemyPoolMap.TryGetValue(spawnType.EnemyPoolId, out var enemyPool))
        {
            CY.LogWarning($"[WaveManager] 敌人池不存在，PoolId={spawnType.EnemyPoolId}"); // 输出敌人池警告
            return GetInterval(spawnType); // 返回刷新间隔
        }

        if (!_refreshTypeMap.TryGetValue(spawnType.RefreshTypeId, out var refreshType))
        {
            CY.LogWarning($"[WaveManager] 刷新类型不存在，Id={spawnType.RefreshTypeId}"); // 输出刷新类型警告
            return GetInterval(spawnType); // 返回刷新间隔
        }

        var spawnCount = GetRandomCount(spawnType.SpawnCountMin, spawnType.SpawnCountMax); // 计算本次刷新数量
        if (spawnCount <= 0)
        {
            return GetInterval(spawnType); // 无生成数量时直接返回
        }

        var context = BuildRefreshContext(refreshType); // 构建刷新上下文
        for (int i = 0; i < spawnCount; i++)
        {
            if (!TryPickEnemy(enemyPool, out var enemyId))
            {
                continue; // 无可用敌人时跳过
            }

            var position = ComputeSpawnPosition(refreshType, ref context, i); // 计算生成位置
            SpawnEnemy(runtime, spawnTypeId, enemyId, position); // 生成敌人
        }

        return GetInterval(spawnType); // 返回下一次刷新间隔
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
            SpawnTypeId = spawnTypeId, // 写入生成类型 Id
            EnemyId = enemyId, // 写入敌人 Id
            Position = position // 写入位置
        };
        CY.Event.Post(ref evt); // 派发刷怪事件
    }

    /// <summary>
    /// 启动波次（内部入口）。
    /// </summary>
    /// <param name="wave">波次配置。</param>
    private bool StartWaveInternal(WaveRow wave) // 波次启动入口
    {
        if (wave == null)
        {
            return false; // 空波次直接失败
        }

        if (_activeWaveMap.ContainsKey(wave.Id))
        {
            return false; // 已在运行时直接退出
        }

        if (_completedWaveIds.Contains(wave.Id))
        {
            return false; // 已完成时直接退出
        }

        if (wave.Channel == WaveChannel.Main && _activeMainWaveId != 0)
        {
            return false; // 主线波次已在运行时阻止并发
        }

        if (!_waveSpawnPoolMap.TryGetValue(wave.Id, out var spawnPool) || spawnPool == null)
        {
            CY.LogWarning($"[WaveManager] 波次未配置生成类型池，WaveId={wave.Id}"); // 输出生成池警告
        }

        var runtime = new WaveRuntime(wave); // 创建运行时波次
        runtime.PrepareRemaining = wave.PrepareDuration; // 设置准备阶段时长
        runtime.SpawnRemaining = wave.SpawnDuration; // 设置刷怪阶段时长
        runtime.IsPreparePhase = true; // 标记准备阶段
        BuildRuntimeSpawnTypes(runtime, spawnPool); // 构建运行时生成类型池

        _activeWaves.Add(runtime); // 添加到活动列表
        _activeWaveMap.Add(runtime.WaveId, runtime); // 添加到映射表
        if (wave.Channel == WaveChannel.Main)
        {
            _activeMainWaveId = wave.Id; // 记录主线波次 Id
        }

        PostPrepareStarted(runtime); // 派发准备阶段开始事件
        return true; // 启动成功
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

        _completedWaveIds.Add(runtime.WaveId); // 标记为完成
        PostWaveFinished(runtime); // 派发结束事件

        if (runtime.Channel == WaveChannel.Main)
        {
            _activeMainWaveId = 0; // 清理主线活动标记
            TryAutoAdvanceMain(runtime.WaveId + 1); // 自动推进下一波
        }
    }

    /// <summary>
    /// 自动推进下一主线波次。
    /// </summary>
    /// <param name="nextWaveId">下一波 Id。</param>
    private void TryAutoAdvanceMain(int nextWaveId) // 主线自动推进入口
    {
        if (!_waveMap.TryGetValue(nextWaveId, out var nextWave))
        {
            return; // 找不到下一波时退出
        }

        if (nextWave.Channel != WaveChannel.Main)
        {
            return; // 非主线波次时退出
        }

        StartWaveInternal(nextWave); // 启动下一主线波次
    }

    /// <summary>
    /// 构建运行时生成类型池（根据解锁波次过滤）。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    /// <param name="spawnPool">波次配置的生成类型池。</param>
    private void BuildRuntimeSpawnTypes(WaveRuntime runtime, List<WeightedId> spawnPool) // 运行时生成池构建入口
    {
        runtime.SpawnTypes.Clear(); // 清理旧池
        runtime.SpawnTypeWeightTotal = 0; // 重置权重总和

        if (spawnPool == null || spawnPool.Count == 0)
        {
            return; // 无生成类型池时直接退出
        }

        for (int i = 0; i < spawnPool.Count; i++)
        {
            var entry = spawnPool[i]; // 取出生成类型条目
            if (entry.Weight <= 0)
            {
                continue; // 权重无效时跳过
            }

            if (!_spawnTypeMap.TryGetValue(entry.Id, out var spawnType))
            {
                continue; // 生成类型缺失时跳过
            }

            if (spawnType.UnlockWave > runtime.WaveId)
            {
                continue; // 未解锁时跳过
            }

            runtime.SpawnTypes.Add(entry); // 添加到运行时池
            runtime.SpawnTypeWeightTotal += entry.Weight; // 累加权重
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

        var roll = Random.Range(1, runtime.SpawnTypeWeightTotal + 1); // 随机权重点
        var cumulative = 0; // 权重累加
        for (int i = 0; i < runtime.SpawnTypes.Count; i++)
        {
            var entry = runtime.SpawnTypes[i]; // 取出条目
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
    private bool TryPickEnemy(EnemyPoolGroup pool, out int enemyId) // 敌人抽取入口
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
    /// 构建刷新上下文（用于单次刷新）。
    /// </summary>
    /// <param name="refreshType">刷新类型配置。</param>
    private RefreshContext BuildRefreshContext(RefreshTypeRow refreshType) // 刷新上下文入口
    {
        var context = new RefreshContext(); // 创建刷新上下文
        context.Mode = refreshType.Mode; // 写入刷新模式
        context.Center = GetCompanyCenter(); // 记录中心点
        context.DirectionIndex = PickDirectionIndex(refreshType.DirectionMask); // 选择方向索引
        context.Around4Offset = Random.Range(0, 4); // 选择四周偏移
        context.SpecialPoint = GetSpecialPoint(refreshType.SpecialPointId, context.Center); // 记录特殊点
        context.Radius = GetRandomRadius(refreshType.RadiusMin, refreshType.RadiusMax); // 记录半径
        return context; // 返回上下文
    }

    /// <summary>
    /// 计算生成位置（根据刷新模式与上下文）。
    /// </summary>
    /// <param name="refreshType">刷新类型配置。</param>
    /// <param name="context">刷新上下文。</param>
    /// <param name="index">生成索引。</param>
    private Vector2 ComputeSpawnPosition(RefreshTypeRow refreshType, ref RefreshContext context, int index) // 生成位置计算入口
    {
        var position = context.Center; // 默认位置
        if (refreshType.Mode == WaveRefreshMode.Center)
        {
            position = context.Center; // 中心点生成
        }
        else if (refreshType.Mode == WaveRefreshMode.SpecialPoint)
        {
            position = context.SpecialPoint; // 命名点生成
        }
        else if (refreshType.Mode == WaveRefreshMode.RingRandom)
        {
            var dir = Random.insideUnitCircle; // 随机方向
            if (dir.sqrMagnitude <= 0.0001f)
            {
                dir = Vector2.right; // 兜底方向
            }

            dir.Normalize(); // 归一化方向
            position = context.Center + dir * context.Radius; // 环形位置
        }
        else if (refreshType.Mode == WaveRefreshMode.Around4)
        {
            var dirIndex = (index + context.Around4Offset) % 4; // 计算四周方向索引
            position = context.Center + Direction4[dirIndex] * context.Radius; // 四周位置
        }
        else if (refreshType.Mode == WaveRefreshMode.Direction8Random)
        {
            var dirIndex = PickDirectionIndex(refreshType.DirectionMask); // 随机方向索引
            position = context.Center + Direction8[dirIndex] * context.Radius; // 8 方向位置
        }
        else
        {
            position = context.Center + Direction8[context.DirectionIndex] * context.Radius; // 8 方向固定位置
        }

        if (refreshType.ScatterRadius > 0f)
        {
            var offset = Random.insideUnitCircle * refreshType.ScatterRadius; // 位置散射偏移
            position += offset; // 应用偏移
        }

        return position; // 返回位置
    }

    /// <summary>
    /// 获取公司中心点。
    /// </summary>
    private Vector2 GetCompanyCenter() // 公司中心获取入口
    {
        var company = CompanyEntity.Current; // 获取公司实体
        if (company == null)
        {
            return Vector2.zero; // 无公司时返回原点
        }

        return company.transform.position; // 返回公司位置
    }

    /// <summary>
    /// 获取特殊点位置（不存在时回退到中心点）。
    /// </summary>
    /// <param name="pointId">命名点 Id。</param>
    /// <param name="fallback">回退位置。</param>
    private Vector2 GetSpecialPoint(string pointId, Vector2 fallback) // 特殊点获取入口
    {
        if (WaveSpawnPoint.TryGetRandomPoint(pointId, out var position))
        {
            return position; // 命中命名点时返回
        }

        return fallback; // 未命中时回退
    }

    /// <summary>
    /// 选择方向索引（按掩码过滤）。
    /// </summary>
    /// <param name="mask">方向掩码。</param>
    private int PickDirectionIndex(int mask) // 方向选择入口
    {
        if (mask == 0)
        {
            return Random.Range(0, Direction8.Length); // 全方向随机
        }

        var count = 0; // 可用方向数量
        for (int i = 0; i < Direction8.Length; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                count++; // 统计可用方向
            }
        }

        if (count <= 0)
        {
            return Random.Range(0, Direction8.Length); // 掩码无效时全方向随机
        }

        var roll = Random.Range(0, count); // 选择索引
        for (int i = 0; i < Direction8.Length; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                if (roll == 0)
                {
                    return i; // 返回命中方向
                }

                roll--; // 递减随机索引
            }
        }

        return 0; // 兜底返回
    }

    /// <summary>
    /// 获取随机半径。
    /// </summary>
    /// <param name="min">最小半径。</param>
    /// <param name="max">最大半径。</param>
    private float GetRandomRadius(float min, float max) // 半径获取入口
    {
        if (max < min)
        {
            max = min; // 保证最大值不小于最小值
        }

        if (max <= 0f)
        {
            return 0f; // 半径无效时返回 0
        }

        return Random.Range(min, max); // 返回随机半径
    }

    /// <summary>
    /// 获取刷新间隔（带兜底最小值）。
    /// </summary>
    /// <param name="spawnType">生成类型。</param>
    private float GetInterval(SpawnTypeRow spawnType) // 间隔获取入口
    {
        var interval = GetRandomInterval(spawnType.IntervalMin, spawnType.IntervalMax); // 获取间隔
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
            Channel = runtime.Channel // 写入通道
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
            Channel = runtime.Channel // 写入通道
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
            Channel = runtime.Channel // 写入通道
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
