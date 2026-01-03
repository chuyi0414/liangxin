using CYFramework;
using CYFramework.Core.Entity;
using CYFramework.Core.Timer;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
using UnityEngine;

/// <summary>
/// LevelEntity 实体：负责在关卡显示时创建公司与老板实体。
/// </summary>
[EntityPrefab("Prefabs/Level/LevelEntity", "Level", "Scene")]
public sealed class LevelEntity : EntityBase
{
    /// <summary>是否在关卡显示时动态烘焙 NavMesh。</summary>
    [SerializeField] private bool _buildNavMeshOnShow = true;
    /// <summary>动态烘焙延迟（秒），用于等待碰撞体初始化完成。</summary>
    [SerializeField] private float _buildNavMeshDelay = 0.1f;

    private CompanyEntity _companyEntity;
    private PlayerEntity _playerEntity;
    private NavMeshSurface _navMeshSurface;
    private Timer _navMeshTimer;
    /// <summary>RootSources2d：显式指定 NavMesh 收集根，避免 DDOL 场景收集不到。</summary>
    private RootSources2d _rootSources2d;
    /// <summary>缓存 NavMesh 收集根列表，避免重复分配。</summary>
    private readonly System.Collections.Generic.List<GameObject> _rootSources =
        new System.Collections.Generic.List<GameObject>(16);

    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        _navMeshSurface = GetComponent<NavMeshSurface>();
        _rootSources2d = GetComponent<RootSources2d>();
        if (_rootSources2d == null)
        {
            _rootSources2d = gameObject.AddComponent<RootSources2d>();
        }
    }

    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);
        SpawnEntities();

        if (_buildNavMeshOnShow)
        {
            RequestRebuildNavMesh(_buildNavMeshDelay);
        }

        TryStartFirstWave(); // 关卡完成初始化后启动第一波
    }

    protected override void OnEntityHide()
    {
        CancelNavMeshBuild();
        CleanupEntities();
        base.OnEntityHide();
    }

    protected override void OnEntityRecycle()
    {
        CancelNavMeshBuild();
        CleanupEntities();
        base.OnEntityRecycle();
    }

    /// <summary>
    /// 生成公司与老板实体。
    /// </summary>
    private void SpawnEntities()
    {
        CleanupEntities();

        _companyEntity = CY.Entity.SpawnEntity<CompanyEntity>();

        var unitManager = CY.Unit;
        PlayerUnitRow row;
        if (unitManager != null && unitManager.TryGetDefaultPlayerRow(out row))
        {
            _playerEntity = CY.Entity.SpawnEntity<PlayerEntity>(row);
            if (_playerEntity != null)
            {
                unitManager.SetPlayer(_playerEntity);
            }
        }
        else
        {
            CY.LogWarning("[LevelEntity] 玩家数据未准备好，无法创建老板实体。");
        }
    }

    /// <summary>
    /// 回收公司与老板实体，防止多实例冲突。
    /// </summary>
    private void CleanupEntities()
    {
        if (_playerEntity != null)
        {
            CY.Entity.RecycleEntity(_playerEntity);
            var unitManager = CY.Unit;
            if (unitManager != null)
            {
                unitManager.SetPlayer(null);
            }

            _playerEntity = null;
        }

        if (_companyEntity != null)
        {
            CY.Entity.RecycleEntity(_companyEntity);
            _companyEntity = null;
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
            CY.LogWarning("[LevelEntity] WaveManager 未就绪，无法启动第一波。"); // 输出警告
            return; // 管理器为空时退出
        }

        if (!waveManager.TryStartWave(1))
        {
            CY.LogWarning("[LevelEntity] 启动第一波失败，可能已在运行。"); // 输出失败提示
        }
    }

    /// <summary>
    /// 请求动态烘焙 NavMesh（适用于场景运行时有大规模布局变化的情况）。
    /// </summary>
    /// <param name="delay">延迟秒数（小于 0 则使用默认值）。</param>
    public void RequestRebuildNavMesh(float delay = -1f)
    {
        if (_navMeshSurface == null)
        {
            CY.LogWarning("[LevelEntity] 未找到 NavMeshSurface，无法动态烘焙。");
            return;
        }

        CancelNavMeshBuild();

        var finalDelay = delay < 0f ? _buildNavMeshDelay : delay;
        _navMeshTimer = CY.Timer.Delay(finalDelay, BuildNavMeshInternal);
    }

    /// <summary>
    /// 取消已排队的 NavMesh 烘焙。
    /// </summary>
    private void CancelNavMeshBuild()
    {
        if (_navMeshTimer != null)
        {
            _navMeshTimer.Stop();
            _navMeshTimer = null;
        }
    }

    /// <summary>
    /// 执行 NavMesh 动态烘焙（注意性能开销）。
    /// </summary>
    private void BuildNavMeshInternal()
    {
        if (_navMeshSurface == null)
        {
            return;
        }

        // 运行时烘焙前强制同步 2D 物理变换，避免 Physics Colliders 模式收集不到几何体。
        PrepareNavMeshRootSources();
        Physics2D.SyncTransforms();
        _navMeshSurface.BuildNavMeshAsync();
        CY.LogInfo("[LevelEntity] NavMesh 动态烘焙已触发（异步）。");
    }

    /// <summary>
    /// 准备 NavMesh 的收集根列表，确保 DDOL 下的导航源也能被收集。
    /// </summary>
    private void PrepareNavMeshRootSources()
    {
        if (_rootSources2d == null)
        {
            return;
        }

        _rootSources.Clear();

        var modifiers = NavMeshModifier.activeModifiers;
        for (int i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            if (modifier == null || !modifier.isActiveAndEnabled)
            {
                continue;
            }

            _rootSources.Add(modifier.gameObject);
        }

        if (_rootSources.Count == 0)
        {
            CY.LogWarning("[LevelEntity] 未找到任何 NavMeshModifier，NavMesh 可能无法生成。");
        }

        _rootSources2d.RootSources = _rootSources;
    }
}
