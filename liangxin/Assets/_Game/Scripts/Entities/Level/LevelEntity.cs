// 引用 CYFramework 命名空间，使用框架统一入口
using CYFramework; // CYFramework 入口引用
// 引用基础设施命名空间，使用 ServiceLocator
using CYFramework.Infrastructure; // ServiceLocator 引用
// 引用实体系统命名空间，使用 EntityBase 等类型
using CYFramework.Core.Entity; // 实体系统类型引用
// 引用计时器命名空间，使用 Timer 类型
using CYFramework.Core.Timer; // 计时器类型引用
// 引用 NavMeshPlus 组件命名空间，使用 NavMeshSurface
using NavMeshPlus.Components; // NavMeshSurface 组件引用
// 引用 NavMeshPlus 扩展命名空间，使用 RootSources2d/Modifier
using NavMeshPlus.Extensions; // NavMeshPlus 扩展引用
// 引用 UnityEngine 命名空间，使用 Transform/Vector2 等类型
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// LevelEntity 实体：负责在关卡显示时创建公司与老板实体。
/// </summary>
[EntityPrefab("Prefabs/Level/LevelEntity", "Level", "Scene")] // 绑定实体预制体信息
public sealed class LevelEntity : EntityBase // 关卡实体定义
{
    /// <summary>是否在关卡显示时动态烘焙 NavMesh。</summary>
    [SerializeField] private bool _buildNavMeshOnShow = true; // 动态烘焙开关
    /// <summary>动态烘焙延迟（秒），用于等待碰撞体初始化完成。</summary>
    [SerializeField] private float _buildNavMeshDelay = 0.1f; // 动态烘焙延迟秒数

    /// <summary>NavMeshGround 根节点（用于 NavMesh 收集）。</summary>
    [SerializeField] private Transform _navMeshGroundRoot; // NavMesh 收集根节点
    /// <summary>怪物刷新点根节点（仅做层级组织）。</summary>
    [SerializeField] private Transform _enemySpawnPointsRoot; // 刷新点根节点引用
    /// <summary>公司出生点。</summary>
    [SerializeField] private Transform _companySpawnPoint; // 公司出生点引用
    /// <summary>玩家出生点。</summary>
    [SerializeField] private Transform _playerSpawnPoint; // 玩家出生点引用

    /// <summary>公司实体缓存。</summary>
    private CompanyEntity _companyEntity; // 公司实体引用
    /// <summary>玩家实体缓存。</summary>
    private PlayerEntity _playerEntity; // 玩家实体引用
    /// <summary>是否已请求下一帧设置相机跟随。</summary>
    private bool _pendingFollowPlayer; // 相机跟随请求标记
    /// <summary>默认 NavMeshSurface（供敌人/玩家使用）。</summary>
    [SerializeField] private NavMeshSurface _defaultNavMeshSurface; // 默认 NavMeshSurface 引用（推荐在预制体拖拽）
    /// <summary>员工 NavMeshSurface（供员工使用）。</summary>
    [SerializeField] private NavMeshSurface _employeeNavMeshSurface; // 员工 NavMeshSurface 引用（推荐在预制体拖拽）
    /// <summary>是否启用员工专用 NavMeshSurface（默认关闭，保留未来多方案扩展能力）。</summary>
    [SerializeField] private bool _enableEmployeeNavMeshSurface = false; // 员工专用 NavMeshSurface 启用开关
    /// <summary>NavMesh 烘焙计时器。</summary>
    private Timer _navMeshTimer; // NavMesh 计时器
    /// <summary>员工 NavMesh 更新间隔（秒），用于让员工之间动态障碍实时生效。</summary>
    [SerializeField] private float _employeeNavMeshUpdateInterval = 0.1f; // 员工 NavMesh 更新间隔秒数
    /// <summary>员工 NavMesh 更新循环计时器。</summary>
    private Timer _employeeNavMeshUpdateTimer; // 员工 NavMesh 更新计时器
    /// <summary>默认 NavMesh 异步烘焙句柄，用于避免重复触发。</summary>
    private AsyncOperation _defaultNavMeshBuildOperation; // 默认 NavMesh 烘焙异步句柄
    /// <summary>员工 NavMesh 异步烘焙句柄，用于避免更新与烘焙并发。</summary>
    private AsyncOperation _employeeNavMeshBuildOperation; // 员工 NavMesh 烘焙异步句柄
    /// <summary>员工 NavMesh 异步更新句柄，用于避免 0.1s 更新堆叠。</summary>
    private AsyncOperation _employeeNavMeshUpdateOperation; // 员工 NavMesh 更新异步句柄
    /// <summary>默认导航 RootSources2d 缓存（与默认 NavMeshSurface 同节点）。</summary>
    private RootSources2d _defaultRootSources2d; // 默认 RootSources2d 缓存
    /// <summary>员工导航 RootSources2d 缓存（与员工 NavMeshSurface 同节点）。</summary>
    private RootSources2d _employeeRootSources2d; // 员工 RootSources2d 缓存
    /// <summary>默认导航收集根列表缓存，避免重复分配。</summary>
    private readonly System.Collections.Generic.List<GameObject> _defaultRootSources = // 默认收集根缓存列表字段
        new System.Collections.Generic.List<GameObject>(16); // 默认收集根列表初始化容量
    /// <summary>员工导航收集根列表缓存，避免重复分配。</summary>
    private readonly System.Collections.Generic.List<GameObject> _employeeRootSources = // 员工收集根缓存列表字段
        new System.Collections.Generic.List<GameObject>(16); // 员工收集根列表初始化容量

    /// <summary>
    /// 实体初始化：缓存组件引用。
    /// </summary>
    /// <param name="userData">初始化传入的数据。</param>
    protected override void OnEntityInit(object userData) // 实体初始化入口
    {
        base.OnEntityInit(userData); // 调用父类初始化
        CacheNavMeshSurfaces(); // 缓存两套 NavMeshSurface 与 RootSources2d
    }

    /// <summary>
    /// 实体显示：创建关卡实体并启动第一波。
    /// </summary>
    /// <param name="userData">显示时传入的数据。</param>
    protected override void OnEntityShow(object userData) // 实体显示入口
    {
        base.OnEntityShow(userData); // 调用父类显示
        transform.position = Vector3.zero; // 重置关卡实体位置到原点
        ValidateLevelReferences(); // 校验关卡引用
        SpawnEntities(); // 创建关卡实体

        if (_buildNavMeshOnShow)
        {
            RequestRebuildNavMesh(_buildNavMeshDelay); // 请求动态烘焙 NavMesh
        }

        if (_enableEmployeeNavMeshSurface) // 员工专用导航启用判定
        {
            StartEmployeeNavMeshUpdateLoop(); // 启动员工 NavMesh 更新循环（用于动态障碍方案）
        }

        if (_enemySpawnPointsRoot != null)
        {
            WaveSpawnPoint.RefreshAll(_enemySpawnPointsRoot); // 强制刷新怪物刷新点注册
        }

        TryStartFirstWave(); // 关卡完成初始化后启动第一波
    }

    /// <summary>
    /// 实体隐藏：清理计时器与实体。
    /// </summary>
    protected override void OnEntityHide() // 实体隐藏入口
    {
        CancelNavMeshBuild(); // 取消 NavMesh 烘焙
        StopEmployeeNavMeshUpdateLoop(); // 停止员工 NavMesh 更新循环
        CleanupEntities(); // 回收关卡实体
        base.OnEntityHide(); // 调用父类隐藏
    }

    /// <summary>
    /// 实体回收：清理计时器与实体。
    /// </summary>
    protected override void OnEntityRecycle() // 实体回收入口
    {
        CancelNavMeshBuild(); // 取消 NavMesh 烘焙
        StopEmployeeNavMeshUpdateLoop(); // 停止员工 NavMesh 更新循环
        CleanupEntities(); // 回收关卡实体
        base.OnEntityRecycle(); // 调用父类回收
    }

    /// <summary>
    /// 校验关卡节点引用，便于编辑器配置检查。
    /// </summary>
    private void ValidateLevelReferences() // 校验入口
    {
        if (_navMeshGroundRoot == null)
        {
            CY.LogWarning("[LevelEntity] NavMeshGroundRoot 未配置，NavMesh 收集可能异常。"); // 输出 NavMesh 根缺失提示
        }

        if (_enemySpawnPointsRoot == null)
        {
            CY.LogWarning("[LevelEntity] EnemySpawnPointsRoot 未配置，波次刷新点需挂在该节点下。"); // 输出刷新点根缺失提示
        }

        if (_companySpawnPoint == null)
        {
            CY.LogWarning("[LevelEntity] CompanySpawnPoint 未配置，公司出生位置将保持原位。"); // 输出公司出生点缺失提示
        }

        if (_playerSpawnPoint == null)
        {
            CY.LogWarning("[LevelEntity] PlayerSpawnPoint 未配置，玩家出生位置将保持原位。"); // 输出玩家出生点缺失提示
        }
    }

    /// <summary>
    /// 生成公司与老板实体。
    /// </summary>
    private void SpawnEntities() // 创建关卡实体入口
    {
        CleanupEntities(); // 先回收旧实体

        _companyEntity = CY.Entity.SpawnEntity<CompanyEntity>(); // 生成公司实体
        ApplySpawnPosition(_companyEntity, _companySpawnPoint); // 应用公司出生点位置

        var unitManager = CY.Unit; // 获取单位管理器
        PlayerUnitRow row; // 玩家数据行声明
        if (unitManager != null && unitManager.TryGetDefaultPlayerRow(out row))
        {
            var playerPreShowData = new PlayerPreShowData(); // 创建玩家预显示数据
            if (_playerSpawnPoint != null)
            {
                playerPreShowData.HasPosition = true; // 标记预显示位置有效
                playerPreShowData.Position = _playerSpawnPoint.position; // 写入玩家出生点位置
            }
            else
            {
                playerPreShowData.HasPosition = true; // 出生点缺失时仍提供位置
                playerPreShowData.Position = Vector3.zero; // 回退到原点避免对象池远距离
            }

            if (!string.IsNullOrEmpty(row.PrefabPath)) // 预制体路径存在判定
            {
                _playerEntity = CY.Entity.SpawnEntity<PlayerEntity, PlayerPreShowData>(row.Code, row.PrefabPath, "Players", ref playerPreShowData, row); // 使用 CSV 的预制体路径生成玩家实体
            }
            else // 路径缺失分支
            {
                _playerEntity = CY.Entity.SpawnEntity<PlayerEntity, PlayerPreShowData>(ref playerPreShowData, row); // 回退为脚本 [EntityPrefab] 默认路径生成玩家实体
            }
            if (_playerEntity != null)
            {
                unitManager.SetPlayer(_playerEntity); // 设置当前玩家引用
                RequestFollowPlayerNextFrame(); // 下一帧设置相机跟随，避免对象池远距离位置抖动
            }
        }
        else
        {
            CY.LogWarning("[LevelEntity] 玩家数据未准备好，无法创建老板实体。"); // 输出玩家数据缺失日志
        }
    }

    /// <summary>
    /// 应用出生点位置到实体（优先刚体移动）。
    /// </summary>
    /// <param name="entity">目标实体。</param>
    /// <param name="spawnPoint">出生点 Transform。</param>
    private void ApplySpawnPosition(EntityBase entity, Transform spawnPoint) // 出生点应用入口
    {
        if (entity == null)
        {
            return; // 实体为空时直接退出
        }

        if (spawnPoint == null)
        {
            return; // 出生点为空时直接退出
        }

        var targetPosition = spawnPoint.position; // 读取出生点世界坐标
        var targetPosition2D = new Vector2(targetPosition.x, targetPosition.y); // 转换为 2D 坐标
        var rigidbody2D = entity.GetComponent<Rigidbody2D>(); // 获取实体刚体组件
        if (rigidbody2D != null)
        {
            rigidbody2D.position = targetPosition2D; // 直接设置刚体位置
            rigidbody2D.velocity = Vector2.zero; // 清空刚体线速度
            rigidbody2D.angularVelocity = 0f; // 清空刚体角速度
            return; // 使用刚体时提前结束
        }

        var entityTransform = entity.transform; // 获取实体 Transform
        entityTransform.position = new Vector3(targetPosition.x, targetPosition.y, entityTransform.position.z); // 设置实体坐标并保留原 Z
    }

    /// <summary>
    /// 回收公司与老板实体，防止多实例冲突。
    /// </summary>
    private void CleanupEntities() // 回收关卡实体入口
    {
        _pendingFollowPlayer = false; // 清理等待跟随标记
        if (_playerEntity != null)
        {
            var unitManager = CY.Unit; // 获取单位管理器
            if (unitManager != null)
            {
                unitManager.SetPlayer(null); // 清理玩家引用
            }

            if (ServiceLocator.TryGet<CameraManager>(out var cameraManager))
            {
                cameraManager.ClearFollowTarget(); // 清理相机跟随目标
            }

            CY.Entity.RecycleEntity(_playerEntity); // 回收玩家实体
            _playerEntity = null; // 清空玩家引用
        }

        if (_companyEntity != null)
        {
            CY.Entity.RecycleEntity(_companyEntity); // 回收公司实体
            _companyEntity = null; // 清空公司引用
        }
    }

    /// <summary>
    /// 请求下一帧设置相机跟随，确保玩家位置已应用。
    /// </summary>
    private void RequestFollowPlayerNextFrame() // 下一帧跟随请求入口
    {
        if (_pendingFollowPlayer)
        {
            return; // 已请求时直接退出
        }

        _pendingFollowPlayer = true; // 标记已请求
        CY.Timer.NextFrame(ApplyFollowPlayer); // 下一帧执行跟随
    }

    /// <summary>
    /// 执行相机跟随（下一帧回调）。
    /// </summary>
    private void ApplyFollowPlayer() // 跟随执行入口
    {
        _pendingFollowPlayer = false; // 清理请求标记
        if (_playerEntity == null)
        {
            return; // 玩家已被回收则退出
        }

        if (ServiceLocator.TryGet<CameraManager>(out var cameraManager))
        {
            cameraManager.SetFollowTarget(_playerEntity.transform, true); // 设置相机跟随并立即对齐
        }
        else
        {
            CY.LogWarning("[LevelEntity] CameraManager 未注册，无法设置相机跟随。"); // 输出相机管理器缺失提示
        }
    }

    /// <summary>
    /// 启动第一波（由关卡初始化完成后触发）。
    /// </summary>
    private void TryStartFirstWave() // 第一波启动入口
    {
        var waveManager = CY.Wave; // 获取波次管理器
        if (waveManager == null)
        {
            CY.LogWarning("[LevelEntity] WaveManager 未就绪，无法启动第一波。"); // 输出管理器缺失警告
            return; // 管理器为空时退出
        }

        if (!waveManager.TryStartWave(1))
        {
            CY.LogWarning("[LevelEntity] 启动第一波失败，可能已在运行。"); // 输出启动失败提示
        }
    }

    /// <summary>
    /// 请求动态烘焙 NavMesh（适用于场景运行时有大规模布局变化的情况）。
    /// </summary>
    /// <param name="delay">延迟秒数（小于 0 则使用默认值）。</param>
    public void RequestRebuildNavMesh(float delay = -1f) // 动态烘焙请求入口
    {
        if (_defaultNavMeshSurface == null && (!_enableEmployeeNavMeshSurface || _employeeNavMeshSurface == null))
        {
            CY.LogWarning("[LevelEntity] 未找到可用 NavMeshSurface（Default 或启用的 Employee），无法动态烘焙。"); // 输出组件缺失警告
            return; // 组件缺失时退出
        }

        CancelNavMeshBuild(); // 取消上一轮烘焙计时

        var finalDelay = delay < 0f ? _buildNavMeshDelay : delay; // 计算最终延迟
        _navMeshTimer = CY.Timer.Delay(finalDelay, BuildNavMeshInternal); // 使用计时器触发烘焙
    }

    /// <summary>
    /// 取消已排队的 NavMesh 烘焙。
    /// </summary>
    private void CancelNavMeshBuild() // 取消烘焙入口
    {
        if (_navMeshTimer != null)
        {
            _navMeshTimer.Stop(); // 停止计时器
            _navMeshTimer = null; // 清理计时器引用
        }

        _defaultNavMeshBuildOperation = null; // 清理默认烘焙句柄引用
        _employeeNavMeshBuildOperation = null; // 清理员工烘焙句柄引用
    }

    /// <summary>
    /// 执行 NavMesh 动态烘焙（注意性能开销）。
    /// </summary>
    private void BuildNavMeshInternal() // 烘焙执行入口
    {
        if (_defaultNavMeshSurface == null && (!_enableEmployeeNavMeshSurface || _employeeNavMeshSurface == null))
        {
            return; // 组件为空时直接退出
        }

        PrepareNavMeshRootSources(_defaultRootSources2d, _defaultRootSources); // 准备默认导航收集根
        if (_enableEmployeeNavMeshSurface) // 员工专用导航启用判定
        {
            PrepareNavMeshRootSources(_employeeRootSources2d, _employeeRootSources); // 准备员工导航收集根
        }
        Physics2D.SyncTransforms(); // 同步 2D 物理变换
        if (_defaultNavMeshSurface != null)
        {
            _defaultNavMeshBuildOperation = _defaultNavMeshSurface.BuildNavMeshAsync(); // 异步触发默认 NavMesh 烘焙并缓存句柄
        }

        if (_enableEmployeeNavMeshSurface && _employeeNavMeshSurface != null)
        {
            _employeeNavMeshBuildOperation = _employeeNavMeshSurface.BuildNavMeshAsync(); // 异步触发员工 NavMesh 烘焙并缓存句柄
        }

        if (_enableEmployeeNavMeshSurface && _employeeNavMeshSurface != null)
        {
            CY.LogInfo("[LevelEntity] NavMesh 动态烘焙已触发（Default + Employee，异步）。"); // 输出烘焙日志
        }
        else
        {
            CY.LogInfo("[LevelEntity] NavMesh 动态烘焙已触发（Default，异步）。"); // 输出烘焙日志
        }
    }

    /// <summary>
    /// 启动员工 NavMesh 更新循环（0.1 秒），用于让员工之间动态障碍实时影响寻路。
    /// </summary>
    private void StartEmployeeNavMeshUpdateLoop() // 员工 NavMesh 更新循环启动入口
    {
        StopEmployeeNavMeshUpdateLoop(); // 先停止旧循环避免重复

        if (!_enableEmployeeNavMeshSurface) // 员工专用导航未启用判定
        {
            return; // 未启用时不启动循环
        }

        if (_employeeNavMeshSurface == null)
        {
            return; // 员工 Surface 缺失时不启动循环
        }

        if (_employeeNavMeshUpdateInterval <= 0f)
        {
            return; // 更新间隔非法时不启动循环
        }

        _employeeNavMeshUpdateTimer = CY.Timer.Loop(_employeeNavMeshUpdateInterval, UpdateEmployeeNavMeshOnce); // 启动循环计时器（不捕获闭包）
    }

    /// <summary>
    /// 停止员工 NavMesh 更新循环。
    /// </summary>
    private void StopEmployeeNavMeshUpdateLoop() // 员工 NavMesh 更新循环停止入口
    {
        if (_employeeNavMeshUpdateTimer != null)
        {
            _employeeNavMeshUpdateTimer.Stop(); // 停止员工更新计时器
            _employeeNavMeshUpdateTimer = null; // 清理计时器引用
        }

        _employeeNavMeshUpdateOperation = null; // 清理更新句柄引用
    }

    /// <summary>
    /// 单次更新员工 NavMesh（异步），用于把员工的 NavMeshModifierVolume（动态障碍）写入员工 NavMesh。
    /// </summary>
    private void UpdateEmployeeNavMeshOnce() // 员工 NavMesh 单次更新入口
    {
        if (_employeeNavMeshSurface == null)
        {
            return; // 员工 Surface 缺失时退出
        }

        if (_employeeNavMeshSurface.navMeshData == null)
        {
            return; // NavMeshData 尚未生成时退出（通常发生在首次烘焙之前）
        }

        if (_employeeNavMeshBuildOperation != null && !_employeeNavMeshBuildOperation.isDone)
        {
            return; // 员工烘焙未完成时跳过本次更新，避免并发
        }

        if (_employeeNavMeshUpdateOperation != null && !_employeeNavMeshUpdateOperation.isDone)
        {
            return; // 上一次更新未完成时跳过，避免 0.1s 堆叠
        }

        PrepareNavMeshRootSources(_employeeRootSources2d, _employeeRootSources); // 确保员工导航收集根正确
        Physics2D.SyncTransforms(); // 同步 2D 物理变换（移动的员工障碍需要先同步）
        _employeeNavMeshUpdateOperation = _employeeNavMeshSurface.UpdateNavMesh(_employeeNavMeshSurface.navMeshData); // 异步更新员工 NavMesh 数据
    }

    /// <summary>
    /// 准备 NavMesh 的收集根列表，确保 DDOL 下的导航源也能被收集。
    /// </summary>
    /// <param name="rootSources2d">目标 RootSources2d。</param>
    /// <param name="rootSources">可复用的收集根列表缓存。</param>
    private void PrepareNavMeshRootSources(RootSources2d rootSources2d, System.Collections.Generic.List<GameObject> rootSources) // 收集根准备入口
    {
        if (rootSources2d == null)
        {
            return; // RootSources2d 缺失时退出
        }

        rootSources.Clear(); // 清理旧收集根

        if (_navMeshGroundRoot != null)
        {
            rootSources.Add(_navMeshGroundRoot.gameObject); // 添加 NavMeshGround 根节点
        }
        else
        {
            var modifiers = NavMeshModifier.activeModifiers; // 获取当前激活的 NavMeshModifier 列表
            for (int i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i]; // 获取当前 Modifier
                if (modifier == null || !modifier.isActiveAndEnabled)
                {
                    continue; // 忽略无效或未启用的 Modifier
                }

                rootSources.Add(modifier.gameObject); // 添加 Modifier 所在节点
            }
        }

        if (rootSources.Count == 0)
        {
            CY.LogWarning("[LevelEntity] 未找到任何 NavMesh 收集根，NavMesh 可能无法生成。"); // 输出收集根缺失警告
        }

        rootSources2d.RootSources = rootSources; // 写入 RootSources2d 收集根
    }

    /// <summary>
    /// 缓存两套 NavMeshSurface 与 RootSources2d，避免运行时反复查找。
    /// </summary>
    private void CacheNavMeshSurfaces() // 导航组件缓存入口
    {
        if (_defaultNavMeshSurface == null || _employeeNavMeshSurface == null)
        {
            var surfaces = GetComponentsInChildren<NavMeshSurface>(true); // 获取子层级全部 NavMeshSurface
            for (int i = 0; i < surfaces.Length; i++)
            {
                var surface = surfaces[i]; // 获取当前 Surface
                if (surface == null)
                {
                    continue; // Surface 为空时跳过
                }

                var surfaceName = surface.gameObject.name; // 读取节点名称
                if (_defaultNavMeshSurface == null && surfaceName == "DefaultNavMeshSurface")
                {
                    _defaultNavMeshSurface = surface; // 缓存默认导航 Surface
                    continue; // 命中后跳过后续判断
                }

                if (_employeeNavMeshSurface == null && surfaceName == "EmployeeNavMeshSurface")
                {
                    _employeeNavMeshSurface = surface; // 缓存员工导航 Surface
                }
            }
        }

        _defaultRootSources2d = _defaultNavMeshSurface != null ? _defaultNavMeshSurface.GetComponent<RootSources2d>() : null; // 缓存默认 RootSources2d
        _employeeRootSources2d = _employeeNavMeshSurface != null ? _employeeNavMeshSurface.GetComponent<RootSources2d>() : null; // 缓存员工 RootSources2d

        if (_defaultNavMeshSurface == null)
        {
            CY.LogWarning("[LevelEntity] DefaultNavMeshSurface 未配置，默认导航可能无法生成。"); // 输出默认 Surface 缺失警告
        }

        if (_enableEmployeeNavMeshSurface && _employeeNavMeshSurface == null)
        {
            CY.LogWarning("[LevelEntity] EmployeeNavMeshSurface 未配置，员工导航可能无法生成。"); // 输出员工 Surface 缺失警告
        }

        if (_defaultNavMeshSurface != null && _defaultRootSources2d == null)
        {
            CY.LogWarning("[LevelEntity] DefaultNavMeshSurface 未挂 RootSources2d，DDOL 场景可能收集不到导航源。"); // 输出默认 RootSources2d 缺失警告
        }

        if (_enableEmployeeNavMeshSurface && _employeeNavMeshSurface != null && _employeeRootSources2d == null)
        {
            CY.LogWarning("[LevelEntity] EmployeeNavMeshSurface 未挂 RootSources2d，DDOL 场景可能收集不到导航源。"); // 输出员工 RootSources2d 缺失警告
        }
    }
}
