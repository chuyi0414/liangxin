using CYFramework;
using CYFramework.Infrastructure;
using UnityEngine;

/// <summary>
/// 测试用管理器：示例结构体骨架，便于后续扩展逻辑。
/// </summary>
public sealed class TestManager : MonoBehaviour, IInitializable, IUpdateable, IDisposableEx
{
    [Header("生命周期配置")]
    [Tooltip("勾选后将在切场景时保留该对象。")]
    [SerializeField] private bool _dontDestroyOnLoad = true;

    private bool _registered;
    private bool _initialized;
    private bool _disposed;
    private readonly Vector2 _testSpawnPosition = new Vector2(10f, 0f);
    private const int TestEnemyId = 1;
    [Header("波次调试配置")]
    [Tooltip("手动触发的波次 Id。")]
    [SerializeField] private int _testWaveId = 1; // 测试波次 Id

    private const KeyCode StartWaveKey = KeyCode.N; // 波次触发按键
    private const KeyCode PauseWaveKey = KeyCode.P; // 暂停切换按键

    /// <summary>初始化顺序（数值越小越早执行）。</summary>
    public int InitOrder => 900;
    /// <summary>释放顺序（数值越大越晚释放）。</summary>
    public int DisposeOrder => -900;

    private void Awake()
    {
        // 场景内只允许存在一个实例，重复挂载时自动销毁后进入者。
        if (ServiceLocator.TryGet<TestManager>(out var existing) && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        if (_dontDestroyOnLoad && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        ServiceLocator.RegisterInstance(this);
        _registered = true;
    }

    private void OnDestroy()
    {
        if (_registered)
        {
            Dispose();
            ServiceLocator.Unregister<TestManager>();
            _registered = false;
        }
    }

    /// <summary>
    /// 初始化逻辑：当前仅打印日志，后续可扩展测试数据加载等能力。
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        CY.LogInfo("[TestManager] 已初始化（测试用骨架）。");
    }

    /// <summary>
    /// 释放逻辑：清理占用资源/引用。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _initialized = false;
    }

    public int UpdateOrder => 900;

    public void OnUpdate(float deltaTime)
    {
        if (!_initialized || _disposed)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            var unitManager = CY.Unit;
            if (unitManager == null)
            {
                CY.LogWarning("[TestManager] UnitManager 未就绪，无法创建敌人。");
                return;
            }

            EnemyEntity enemy;
            if (unitManager.TryCreateEnemy(TestEnemyId, _testSpawnPosition, out enemy))
            {
                CY.LogInfo($"[TestManager] 已生成测试敌人（Id={TestEnemyId}，位置={_testSpawnPosition}）。");
            }
            else
            {
                CY.LogWarning("[TestManager] 创建测试敌人失败，请检查数据表或实体配置。");
            }
        }

        if (Input.GetKeyDown(StartWaveKey)) // 触发波次测试
        {
            var waveManager = CY.Wave; // 获取波次管理器
            if (waveManager == null) // 管理器为空判定
            {
                CY.LogWarning("[TestManager] WaveManager 未就绪，无法触发波次。"); // 输出警告
                return; // 直接退出
            }

            if (waveManager.TryStartWave(_testWaveId)) // 尝试触发波次
            {
                CY.LogInfo($"[TestManager] 已触发测试波次（Id={_testWaveId}）。"); // 输出成功日志
            }
            else
            {
                CY.LogWarning($"[TestManager] 触发测试波次失败（Id={_testWaveId}）。"); // 输出失败日志
            }
        }

        if (Input.GetKeyDown(PauseWaveKey)) // 切换暂停测试
        {
            var waveManager = CY.Wave; // 获取波次管理器
            if (waveManager == null) // 管理器为空判定
            {
                CY.LogWarning("[TestManager] WaveManager 未就绪，无法切换暂停。"); // 输出警告
                return; // 直接退出
            }

            var paused = !waveManager.IsPaused; // 计算目标暂停状态
            waveManager.SetPaused(paused); // 设置暂停
            CY.LogInfo($"[TestManager] 波次暂停状态切换为：{paused}。"); // 输出日志
        }
    }
}
