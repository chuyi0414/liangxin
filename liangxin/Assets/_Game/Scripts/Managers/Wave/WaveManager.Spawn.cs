// 引用 CYFramework 入口，使用 CY.Event/CY.Unit
using CYFramework; // 框架统一入口
// 引用 UnityEngine，使用 Vector2/Random/Mathf
using UnityEngine; // Unity 引擎类型引用

public sealed partial class WaveManager // 波次管理器分部定义
{
    /// <summary>
    /// 更新当前活动波次。
    /// </summary>
    /// <param name="deltaTime">帧间隔时间。</param>
    private void UpdateActiveWave(float deltaTime) // 活动波次更新入口
    {
        if (_currentWave == null) // 波次存在判定
        {
            return; // 无波次时直接退出
        }

        _currentWave.ElapsedTime += deltaTime; // 累加波次时间

        var allTracksFinished = true; // 全轨道完成标记
        var anyTrackStarted = false; // 任意轨道已开始标记
        for (int i = 0; i < _currentWave.Tracks.Count; i++) // 遍历轨道
        {
            var track = _currentWave.Tracks[i]; // 取出轨道
            if (track == null) // 空轨道判定
            {
                continue; // 跳过空轨道
            }

            UpdateTrackRuntime(track, deltaTime); // 更新轨道运行时
            if (track.IsStarted) // 已开始判定
            {
                anyTrackStarted = true; // 标记存在已开始轨道
            }
            if (!track.IsFinished) // 未结束判定
            {
                allTracksFinished = false; // 标记未全部完成
            }
        }

        if (anyTrackStarted && !_currentWave.HasSpawnStarted) // 刷怪阶段触发判定
        {
            _currentWave.HasSpawnStarted = true; // 标记已进入刷怪阶段
            PostSpawnStarted(); // 派发刷怪开始事件
        }

        if (CheckWaveEndCondition(_currentWave, allTracksFinished)) // 波次结束判定
        {
            FinishWave(); // 结束波次
        }
    }

    /// <summary>
    /// 更新单条轨道运行时。
    /// </summary>
    /// <param name="track">轨道运行时。</param>
    /// <param name="deltaTime">帧间隔时间。</param>
    private void UpdateTrackRuntime(TrackRuntime track, float deltaTime) // 轨道更新入口
    {
        if (track == null || track.Row == null) // 空轨道判定
        {
            return; // 空轨道直接退出
        }

        if (track.IsFinished) // 已结束判定
        {
            return; // 已结束时退出
        }

        if (!track.StartConditionMet) // 开始条件未满足判定
        {
            if (CheckTrackStartCondition(track)) // 开始条件满足判定
            {
                track.StartConditionMet = true; // 标记开始条件满足
                track.StartDelayRemaining = track.Row.StartDelay; // 初始化开始延迟
            }
            else
            {
                return; // 条件未满足时退出
            }
        }

        if (!track.IsStarted) // 未开始判定
        {
            if (track.StartDelayRemaining > 0f) // 延迟未结束判定
            {
                track.StartDelayRemaining -= deltaTime; // 递减延迟时间
                if (track.StartDelayRemaining > 0f) // 延迟仍未结束判定
                {
                    return; // 延迟未结束时退出
                }
            }

            track.StartDelayRemaining = 0f; // 确保延迟清零
            track.IsStarted = true; // 标记已开始
            track.NextSpawnTimer = 0f; // 刷新计时归零
        }

        track.ElapsedTime += deltaTime; // 累加轨道时间

        if (CheckTrackEndCondition(track)) // 轨道结束条件判定
        {
            track.IsFinished = true; // 标记轨道结束
            return; // 结束后退出
        }

        if (track.Row.MaxTotalSpawn > 0 && track.SpawnedCount >= track.Row.MaxTotalSpawn) // 最大刷怪数判定
        {
            track.IsFinished = true; // 达到上限时结束
            return; // 结束后退出
        }

        UpdateTrackSpawns(track, deltaTime); // 更新轨道刷怪逻辑
    }

    /// <summary>
    /// 更新轨道刷怪逻辑。
    /// </summary>
    /// <param name="track">轨道运行时。</param>
    /// <param name="deltaTime">帧间隔时间。</param>
    private void UpdateTrackSpawns(TrackRuntime track, float deltaTime) // 轨道刷怪更新入口
    {
        if (track.SpawnGroup == null || track.SpawnGroup.Row == null) // 刷怪组有效性判定
        {
            return; // 刷怪组无效时退出
        }

        track.NextSpawnTimer -= deltaTime; // 递减刷新计时器
        var guard = 0; // 刷新次数保护
        while (track.NextSpawnTimer <= 0f && guard < _maxRefreshPerUpdate) // 刷新循环
        {
            var nextInterval = SpawnBatch(track); // 执行一次刷新批次
            track.NextSpawnTimer += nextInterval; // 累加下一次刷新间隔
            guard++; // 递增刷新计数
        }
    }

    /// <summary>
    /// 执行一次刷新批次，返回下一次刷新间隔。
    /// </summary>
    /// <param name="track">轨道运行时。</param>
    private float SpawnBatch(TrackRuntime track) // 刷新批次入口
    {
        if (track == null || track.SpawnGroup == null || track.SpawnGroup.Row == null) // 参数判定
        {
            return _minRefreshInterval; // 无效参数时返回兜底间隔
        }

        var row = track.SpawnGroup.Row; // 获取刷怪组配置
        if (track.Row.MaxAlive > 0 && track.AliveCount >= track.Row.MaxAlive) // 同屏存活上限判定
        {
            return _minRefreshInterval; // 达到上限时返回兜底间隔
        }

        var spawnCount = GetRandomCount(row.SpawnCountMin, row.SpawnCountMax); // 计算本次刷新数量
        if (spawnCount <= 0) // 数量无效判定
        {
            return GetInterval(row.IntervalMin, row.IntervalMax); // 无生成数量时返回间隔
        }

        var formation = (WaveFormation)row.Formation; // 解析阵型类型
        var distribution = (WaveDistribution)row.Distribution; // 解析分布方式
        var useSharedAnchor = formation != WaveFormation.Point || distribution == WaveDistribution.Uniform; // 判断是否共享锚点
        var anchor = Vector2.zero; // 锚点初始化
        if (useSharedAnchor) // 共享锚点判定
        {
            if (!TryGetSpawnAnchor(track.SpawnGroup, out anchor)) // 获取锚点
            {
                return GetInterval(row.IntervalMin, row.IntervalMax); // 获取失败时返回间隔
            }
        }

        for (int i = 0; i < spawnCount; i++) // 遍历刷新数量
        {
            if (track.Row.MaxTotalSpawn > 0 && track.SpawnedCount >= track.Row.MaxTotalSpawn) // 总刷怪上限判定
            {
                break; // 达到上限时中止
            }

            if (track.Row.MaxAlive > 0 && track.AliveCount >= track.Row.MaxAlive) // 同屏存活上限判定
            {
                break; // 达到上限时中止
            }

            if (!useSharedAnchor) // 单点随机锚点判定
            {
                if (!TryGetSpawnAnchor(track.SpawnGroup, out anchor)) // 获取锚点
                {
                    continue; // 获取失败时跳过
                }
            }

            if (!TryPickEnemy(track.SpawnGroup.EnemyPool, out var enemyId)) // 抽取敌人 Id
            {
                continue; // 无可用敌人时跳过
            }

            var position = ComputeFormationPosition(row, anchor, i, spawnCount); // 计算阵型位置
            SpawnEnemy(track, enemyId, position); // 生成敌人
        }

        return GetInterval(row.IntervalMin, row.IntervalMax); // 返回下一次刷新间隔
    }

    /// <summary>
    /// 尝试从敌人池中按权重抽取敌人。
    /// </summary>
    /// <param name="pool">敌人池。</param>
    /// <param name="enemyId">输出敌人 Id。</param>
    private bool TryPickEnemy(SpawnGroupEnemyPool pool, out int enemyId) // 敌人抽取入口
    {
        enemyId = 0; // 默认输出
        if (pool == null || pool.TotalWeight <= 0 || pool.Entries.Count == 0) // 敌人池有效性判定
        {
            return false; // 敌人池无效时失败
        }

        var roll = Random.Range(1, pool.TotalWeight + 1); // 随机权重点
        var cumulative = 0; // 权重累加
        for (int i = 0; i < pool.Entries.Count; i++) // 遍历条目
        {
            var entry = pool.Entries[i]; // 取出条目
            cumulative += entry.Weight; // 累加权重
            if (roll <= cumulative) // 命中判定
            {
                enemyId = entry.Id; // 命中敌人 Id
                return true; // 返回成功
            }
        }

        return false; // 未命中时失败
    }

    /// <summary>
    /// 尝试获取刷怪锚点。
    /// </summary>
    /// <param name="group">刷怪组运行时。</param>
    /// <param name="anchor">输出锚点位置。</param>
    private bool TryGetSpawnAnchor(SpawnGroupRuntime group, out Vector2 anchor) // 锚点获取入口
    {
        anchor = Vector2.zero; // 默认输出
        if (group == null || group.Row == null) // 参数判定
        {
            return false; // 无效参数时失败
        }

        var pointMode = (WavePointMode)group.Row.PointMode; // 解析刷新点模式
        if (pointMode == WavePointMode.PointId) // 命名点模式判定
        {
            if (group.PointIds == null || group.PointIds.Count == 0) // 刷新点列表判定
            {
                CY.LogError($"[WaveManager] 刷怪组无可用刷新点，GroupId={group.Row.GroupId}"); // 输出错误日志
                return false; // 无刷新点时失败
            }

            var index = Random.Range(0, group.PointIds.Count); // 随机点索引
            var pointId = group.PointIds[index]; // 获取点 Id
            if (!WaveSpawnPoint.TryGetRandomPoint(pointId, out anchor)) // 获取随机点
            {
                CY.LogError($"[WaveManager] 刷新点未注册或无效，PointId={pointId}"); // 输出错误日志
                return false; // 获取失败时返回 false
            }

            return true; // 获取成功返回 true
        }

        if (!WaveArea.TryGetRandomPoint(group.Row.AreaId, out anchor)) // 区域采样判定
        {
            CY.LogError($"[WaveManager] 区域无效或未注册，AreaId={group.Row.AreaId}"); // 输出错误日志
            return false; // 获取失败时返回 false
        }

        return true; // 获取成功返回 true
    }

    /// <summary>
    /// 计算阵型位置。
    /// </summary>
    /// <param name="row">刷怪组配置。</param>
    /// <param name="anchor">锚点位置。</param>
    /// <param name="index">当前索引。</param>
    /// <param name="count">总数量。</param>
    private Vector2 ComputeFormationPosition(WaveSpawnGroupRow row, Vector2 anchor, int index, int count) // 阵型计算入口
    {
        if (row == null) // 空配置判定
        {
            return anchor; // 空配置时回退锚点
        }

        var formation = (WaveFormation)row.Formation; // 解析阵型类型
        var distribution = (WaveDistribution)row.Distribution; // 解析分布方式
        var angleRad = row.DirectionAngle * Mathf.Deg2Rad; // 计算朝向弧度

        if (formation == WaveFormation.Point) // 单点阵型判定
        {
            return anchor; // 单点直接返回锚点
        }

        if (formation == WaveFormation.Circle) // 圆形阵型判定
        {
            var radius = row.Param1; // 读取半径
            if (radius <= 0f) // 半径判定
            {
                return anchor; // 半径无效时回退锚点
            }

            if (distribution == WaveDistribution.Uniform && count > 1) // 均匀分布判定
            {
                var angle = 360f * (index / (float)count); // 计算均匀角度
                var rad = angle * Mathf.Deg2Rad; // 转换为弧度
                var offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius; // 计算偏移
                return anchor + offset; // 返回位置
            }

            var randomOffset = Random.insideUnitCircle * radius; // 随机圆内偏移
            return anchor + randomOffset; // 返回位置
        }

        if (formation == WaveFormation.Line) // 直线阵型判定
        {
            var length = row.Param1; // 读取长度
            if (length <= 0f) // 长度判定
            {
                return anchor; // 长度无效时回退锚点
            }

            float t; // 线性位置偏移
            if (distribution == WaveDistribution.Uniform && count > 1) // 均匀分布判定
            {
                t = (index / (float)(count - 1) - 0.5f) * length; // 计算均匀偏移
            }
            else
            {
                t = Random.Range(-0.5f * length, 0.5f * length); // 计算随机偏移
            }
            var dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)); // 计算方向
            return anchor + dir * t; // 返回位置
        }

        if (formation == WaveFormation.Fan) // 扇形阵型判定
        {
            var radius = row.Param1; // 读取半径
            var angleRange = row.Param2; // 读取角度范围
            if (radius <= 0f) // 半径判定
            {
                return anchor; // 半径无效时回退锚点
            }

            var half = angleRange * 0.5f; // 计算半角
            float offsetAngle; // 角度偏移
            if (distribution == WaveDistribution.Uniform && count > 1) // 均匀分布判定
            {
                offsetAngle = -half + (index / (float)(count - 1)) * angleRange; // 计算均匀角度
            }
            else
            {
                offsetAngle = Random.Range(-half, half); // 计算随机角度
            }
            var finalAngle = (row.DirectionAngle + offsetAngle) * Mathf.Deg2Rad; // 计算最终角度
            var finalRadius = distribution == WaveDistribution.Uniform ? radius : Random.Range(0f, radius); // 计算半径
            var dir = new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle)); // 计算方向
            return anchor + dir * finalRadius; // 返回位置
        }

        if (formation == WaveFormation.Rect) // 矩形阵型判定
        {
            var width = row.Param1; // 读取宽度
            var height = row.Param2; // 读取高度
            if (width <= 0f || height <= 0f) // 宽高判定
            {
                return anchor; // 无效尺寸时回退锚点
            }

            if (distribution == WaveDistribution.Uniform && count > 1) // 均匀分布判定
            {
                var columns = Mathf.CeilToInt(Mathf.Sqrt(count)); // 计算列数
                var rows = Mathf.CeilToInt(count / (float)columns); // 计算行数
                var col = index % columns; // 计算列索引
                var rowIndex = index / columns; // 计算行索引
                var x = columns <= 1 ? 0f : (col / (float)(columns - 1) - 0.5f) * width; // 计算 X
                var y = rows <= 1 ? 0f : (rowIndex / (float)(rows - 1) - 0.5f) * height; // 计算 Y
                var local = new Vector2(x, y); // 计算局部偏移
                var rotated = Rotate(local, angleRad); // 旋转局部偏移
                return anchor + rotated; // 返回位置
            }

            var rx = Random.Range(-0.5f * width, 0.5f * width); // 随机 X
            var ry = Random.Range(-0.5f * height, 0.5f * height); // 随机 Y
            var randomLocal = new Vector2(rx, ry); // 生成局部随机
            var randomRotated = Rotate(randomLocal, angleRad); // 旋转随机偏移
            return anchor + randomRotated; // 返回位置
        }

        return anchor; // 兜底返回锚点
    }

    /// <summary>
    /// 二维旋转。
    /// </summary>
    /// <param name="value">待旋转向量。</param>
    /// <param name="rad">弧度。</param>
    private Vector2 Rotate(Vector2 value, float rad) // 旋转入口
    {
        var cos = Mathf.Cos(rad); // 计算 cos
        var sin = Mathf.Sin(rad); // 计算 sin
        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos); // 返回旋转向量
    }

    /// <summary>
    /// 获取刷新间隔（带兜底最小值）。
    /// </summary>
    /// <param name="min">最小间隔。</param>
    /// <param name="max">最大间隔。</param>
    private float GetInterval(float min, float max) // 间隔获取入口
    {
        var interval = GetRandomInterval(min, max); // 获取间隔
        if (interval <= 0f) // 间隔无效判定
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
        if (max < min) // 最大值判定
        {
            max = min; // 修正最大值
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
        if (max < min) // 最大值判定
        {
            max = min; // 修正最大值
        }

        if (max <= 0) // 数量无效判定
        {
            return 0; // 数量无效时返回 0
        }

        return Random.Range(min, max + 1); // 返回随机数量
    }

    /// <summary>
    /// 生成敌人并更新统计。
    /// </summary>
    /// <param name="track">轨道运行时。</param>
    /// <param name="enemyId">敌人 Id。</param>
    /// <param name="position">生成位置。</param>
    private void SpawnEnemy(TrackRuntime track, int enemyId, Vector2 position) // 敌人生成入口
    {
        if (track == null) // 轨道为空判定
        {
            return; // 轨道为空时退出
        }

        var unitManager = CY.Unit; // 获取单位管理器
        if (unitManager == null) // 管理器判定
        {
            CY.LogWarning("[WaveManager] UnitManager 未就绪，无法生成敌人。"); // 输出警告
            return; // 无管理器时退出
        }

        if (!unitManager.TryCreateEnemy(enemyId, position, out var enemy)) // 生成敌人判定
        {
            CY.LogWarning($"[WaveManager] 生成敌人失败，EnemyId={enemyId}"); // 输出失败日志
            return; // 生成失败时退出
        }

        track.SpawnedCount += 1; // 递增轨道刷怪数量
        if (_currentWave != null) // 波次存在判定
        {
            _currentWave.TotalSpawned += 1; // 累加波次刷怪数量
        }

        if (enemy != null && _currentWave != null) // 敌人有效判定
        {
            _enemyTrackMap[enemy] = track; // 写入敌人映射
            track.AliveCount += 1; // 递增轨道存活
            _currentWave.EnemyAliveCount += 1; // 递增波次存活
        }

        var evt = new WaveSpawnedEvent // 创建刷怪事件
        {
            WaveId = _currentWaveId, // 写入波次 Id
            IsAssault = false, // 写入奇袭标记
            SpawnGroupId = track.Row.SpawnGroupId, // 写入刷怪组 Id
            EnemyId = enemyId, // 写入敌人 Id
            Position = position // 写入位置
        };
        CY.Event.Post(ref evt); // 派发刷怪事件
    }

    /// <summary>
    /// 结束当前波次并派发事件。
    /// </summary>
    private void FinishWave() // 波次结束入口
    {
        var finishedWaveId = _currentWaveId; // 缓存结束波次 Id
        var finishedIsAssault = false; // 缓存奇袭标记（当前未使用奇袭）
        var finishedAutoAdvance = _currentWave != null && _currentWave.Plan != null && _currentWave.Plan.AutoAdvance != 0; // 缓存自动推进标记
        _completedWaveCount += 1; // 累加已完成波次数
        ResetRuntimeInternal(); // 清理运行时
        PostWaveFinished(finishedWaveId, finishedIsAssault, finishedAutoAdvance); // 派发波次结束事件
    }

    /// <summary>
    /// 判定波次结束条件。
    /// </summary>
    /// <param name="runtime">波次运行时。</param>
    /// <param name="allTracksFinished">是否全部轨道完成。</param>
    private bool CheckWaveEndCondition(WaveRuntime runtime, bool allTracksFinished) // 波次结束判定入口
    {
        if (runtime == null || runtime.Plan == null) // 空判定
        {
            return true; // 无波次时视为结束
        }

        var endType = (WaveTriggerType)runtime.Plan.EndType; // 解析结束类型
        if (endType == WaveTriggerType.AllTracksDone) // 全轨道完成判定
        {
            return allTracksFinished; // 返回全轨道状态
        }

        if (endType == WaveTriggerType.Time) // 时间结束判定
        {
            return runtime.ElapsedTime >= runtime.Plan.EndValue; // 时间达到阈值则结束
        }

        if (endType == WaveTriggerType.KillCount) // 击杀数结束判定
        {
            return runtime.TotalKilled >= runtime.Plan.EndValue; // 击杀数达到阈值则结束
        }

        if (endType == WaveTriggerType.AliveCount) // 存活数结束判定
        {
            return runtime.EnemyAliveCount <= runtime.Plan.EndValue; // 存活数小于等于阈值则结束
        }

        if (endType == WaveTriggerType.SpawnedCount) // 刷怪数结束判定
        {
            return runtime.TotalSpawned >= runtime.Plan.EndValue; // 刷怪数达到阈值则结束
        }

        if (endType == WaveTriggerType.Event) // 事件结束判定
        {
            return ConsumeEventTrigger(runtime.Plan.EndId); // 消费事件触发
        }

        if (endType == WaveTriggerType.Area) // 区域结束判定
        {
            var useExit = runtime.Plan.EndValue > 0.5f; // 退出触发判定
            return ConsumeAreaTrigger(runtime.Plan.EndId, useExit); // 消费区域触发
        }

        return false; // 未满足条件时不结束
    }

    /// <summary>
    /// 判定轨道开始条件。
    /// </summary>
    /// <param name="track">轨道运行时。</param>
    private bool CheckTrackStartCondition(TrackRuntime track) // 轨道开始判定入口
    {
        if (track == null || track.Row == null || _currentWave == null) // 空判定
        {
            return false; // 无效时返回 false
        }

        var startType = (WaveTriggerType)track.Row.StartType; // 解析开始类型
        if (startType == WaveTriggerType.Time) // 时间开始判定
        {
            return _currentWave.ElapsedTime >= track.Row.StartValue; // 时间达到阈值则开始
        }

        if (startType == WaveTriggerType.KillCount) // 击杀数开始判定
        {
            return _currentWave.TotalKilled >= track.Row.StartValue; // 击杀数达到阈值则开始
        }

        if (startType == WaveTriggerType.AliveCount) // 存活数开始判定
        {
            return _currentWave.EnemyAliveCount <= track.Row.StartValue; // 存活数小于等于阈值则开始
        }

        if (startType == WaveTriggerType.SpawnedCount) // 刷怪数开始判定
        {
            return _currentWave.TotalSpawned >= track.Row.StartValue; // 刷怪数达到阈值则开始
        }

        if (startType == WaveTriggerType.Event) // 事件开始判定
        {
            return ConsumeEventTrigger(track.Row.StartId); // 消费事件触发
        }

        if (startType == WaveTriggerType.Area) // 区域开始判定
        {
            var useExit = track.Row.StartValue > 0.5f; // 退出触发判定
            return ConsumeAreaTrigger(track.Row.StartId, useExit); // 消费区域触发
        }

        return false; // 未满足条件返回 false
    }

    /// <summary>
    /// 判定轨道结束条件。
    /// </summary>
    /// <param name="track">轨道运行时。</param>
    private bool CheckTrackEndCondition(TrackRuntime track) // 轨道结束判定入口
    {
        if (track == null || track.Row == null) // 空判定
        {
            return true; // 无效轨道视为结束
        }

        var endType = (WaveTriggerType)track.Row.EndType; // 解析结束类型
        if (endType == WaveTriggerType.Time) // 时间结束判定
        {
            return track.ElapsedTime >= track.Row.EndValue; // 轨道时间达到阈值则结束
        }

        if (endType == WaveTriggerType.KillCount) // 击杀数结束判定
        {
            return _currentWave != null && _currentWave.TotalKilled >= track.Row.EndValue; // 击杀数达到阈值则结束
        }

        if (endType == WaveTriggerType.AliveCount) // 存活数结束判定
        {
            return _currentWave != null && _currentWave.EnemyAliveCount <= track.Row.EndValue; // 存活数小于等于阈值则结束
        }

        if (endType == WaveTriggerType.SpawnedCount) // 刷怪数结束判定
        {
            return track.SpawnedCount >= track.Row.EndValue; // 刷怪数达到阈值则结束
        }

        if (endType == WaveTriggerType.Event) // 事件结束判定
        {
            return ConsumeEventTrigger(track.Row.EndId); // 消费事件触发
        }

        if (endType == WaveTriggerType.Area) // 区域结束判定
        {
            var useExit = track.Row.EndValue > 0.5f; // 退出触发判定
            return ConsumeAreaTrigger(track.Row.EndId, useExit); // 消费区域触发
        }

        return false; // 默认不结束
    }

    /// <summary>
    /// 消费一次事件触发。
    /// </summary>
    /// <param name="triggerId">触发 Id。</param>
    private bool ConsumeEventTrigger(string triggerId) // 事件触发消费入口
    {
        if (string.IsNullOrEmpty(triggerId)) // 触发 Id 判定
        {
            return false; // 触发 Id 为空时返回 false
        }

        if (_eventTriggerSet.Remove(triggerId)) // 移除触发判定
        {
            return true; // 消费成功返回 true
        }

        return false; // 未命中时返回 false
    }

    /// <summary>
    /// 消费一次区域触发。
    /// </summary>
    /// <param name="areaId">区域 Id。</param>
    /// <param name="useExit">是否使用退出触发。</param>
    private bool ConsumeAreaTrigger(string areaId, bool useExit) // 区域触发消费入口
    {
        if (string.IsNullOrEmpty(areaId)) // 区域 Id 判定
        {
            return false; // 区域 Id 为空时返回 false
        }

        var set = useExit ? _areaExitSet : _areaEnterSet; // 选择触发集合
        if (set.Remove(areaId)) // 移除触发判定
        {
            return true; // 消费成功返回 true
        }

        return false; // 未命中时返回 false
    }
}
