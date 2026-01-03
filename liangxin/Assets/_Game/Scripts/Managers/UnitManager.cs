using System.Collections.Generic;
using CYFramework;
using CYFramework.Core.DataTable;
using CYFramework.Infrastructure;
using UnityEngine;

/// <summary>
/// 单位管理器：集中维护老板、员工、敌人的运行列表。
/// 说明：此处只做引用管理，不包含具体战斗逻辑。
/// </summary>
public sealed class UnitManager : MonoBehaviour, IInitializable, IDisposableEx
{
    /// <summary>玩家 CSV 数据表名。</summary>
    private const string PlayerTableName = "Player";
    /// <summary>敌人 CSV 数据表名。</summary>
    private const string EnemyTableName = "Enemy";

    /// <summary>是否在切场景时保留该对象。</summary>
    [SerializeField] private bool _dontDestroyOnLoad = true;

    /// <summary>老板（玩家单位）引用。</summary>
    [SerializeField] private PlayerEntity _player;

    /// <summary>员工单位列表（运行时维护）。</summary>
    private readonly List<UnitEntity> _employees = new List<UnitEntity>(32);

    /// <summary>敌人单位列表（运行时维护）。</summary>
    private readonly List<UnitEntity> _enemies = new List<UnitEntity>(64);

    /// <summary>默认玩家数据（来自 CSV 第一行）。</summary>
    private PlayerUnitRow _defaultPlayerRow;

    /// <summary>是否已缓存默认玩家数据。</summary>
    private bool _hasDefaultPlayerRow;

    /// <summary>敌人数据缓存表（Id -> Row）。</summary>
    private readonly Dictionary<int, EnemyUnitRow> _enemyRowMap = new Dictionary<int, EnemyUnitRow>(64);
    /// <summary>是否已缓存敌人数据表。</summary>
    private bool _hasEnemyRows;

    /// <summary>是否已注册到 ServiceLocator。</summary>
    private bool _registered;

    /// <summary>是否已释放。</summary>
    private bool _disposed;

    /// <summary>初始化顺序（数值小的先执行）。</summary>
    public int InitOrder => 120;

    /// <summary>释放顺序（数值大的先释放）。</summary>
    public int DisposeOrder => -120;

    /// <summary>老板（只读）。</summary>
    public PlayerEntity Player => _player;

    /// <summary>员工列表（只读）。</summary>
    public IReadOnlyList<UnitEntity> Employees => _employees;

    /// <summary>敌人列表（只读）。</summary>
    public IReadOnlyList<UnitEntity> Enemies => _enemies;

    /// <summary>是否存在默认玩家数据。</summary>
    public bool HasDefaultPlayerRow => _hasDefaultPlayerRow;

    /// <summary>默认玩家数据（只读）。</summary>
    public PlayerUnitRow DefaultPlayerRow
    {
        get
        {
            if (!_hasDefaultPlayerRow)
            {
                TryCacheDefaultPlayerRow();
            }

            return _defaultPlayerRow;
        }
    }

    /// <summary>
    /// 获取默认玩家数据（若未缓存会尝试读取，失败返回 false）。
    /// </summary>
    public bool TryGetDefaultPlayerRow(out PlayerUnitRow row)
    {
        if (!_hasDefaultPlayerRow)
        {
            TryCacheDefaultPlayerRow();
        }

        row = _defaultPlayerRow;
        return _hasDefaultPlayerRow && row != null;
    }

    private void Awake()
    {
        // 场景可能重复挂载，使用 ServiceLocator 保证单例并避免重复注册。
        if (ServiceLocator.TryGet<UnitManager>(out var existing) && existing != this)
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
        // 只注销自身注册，避免误删其它实例。
        if (_registered)
        {
            Dispose();
            ServiceLocator.Unregister<UnitManager>();
            _registered = false;
        }
    }

    /// <summary>
    /// 初始化（由 ServiceLocator 驱动，只会执行一次）。
    /// </summary>
    public void Initialize()
    {
        TryCacheDefaultPlayerRow();
        TryCacheEnemyRows();
    }

    /// <summary>
    /// 释放清理（清空列表引用）。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _player = null;
        _employees.Clear();
        _enemies.Clear();
        _enemyRowMap.Clear();
        _hasEnemyRows = false;
        _defaultPlayerRow = null;
        _hasDefaultPlayerRow = false;
    }

    /// <summary>
    /// 设置老板引用（允许替换）。
    /// </summary>
    public void SetPlayer(PlayerEntity player)
    {
        _player = player;
    }

    /// <summary>
    /// 添加员工（重复添加会被忽略）。
    /// </summary>
    public bool AddEmployee(UnitEntity employee)
    {
        if (employee == null)
        {
            return false;
        }

        for (int i = 0; i < _employees.Count; i++)
        {
            if (_employees[i] == employee)
            {
                return false;
            }
        }

        _employees.Add(employee);
        return true;
    }

    /// <summary>
    /// 移除员工（不存在则返回 false）。
    /// </summary>
    public bool RemoveEmployee(UnitEntity employee)
    {
        if (employee == null)
        {
            return false;
        }

        for (int i = 0; i < _employees.Count; i++)
        {
            if (_employees[i] == employee)
            {
                _employees.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 添加敌人（重复添加会被忽略）。
    /// </summary>
    public bool AddEnemy(UnitEntity enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            if (_enemies[i] == enemy)
            {
                return false;
            }
        }

        _enemies.Add(enemy);
        return true;
    }

    /// <summary>
    /// 移除敌人（不存在则返回 false）。
    /// </summary>
    public bool RemoveEnemy(UnitEntity enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            if (_enemies[i] == enemy)
            {
                _enemies.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 清空员工列表（不销毁实体，仅移除引用）。
    /// </summary>
    public void ClearEmployees()
    {
        _employees.Clear();
    }

    /// <summary>
    /// 清空敌人列表（不销毁实体，仅移除引用）。
    /// </summary>
    public void ClearEnemies()
    {
        _enemies.Clear();
    }

    /// <summary>
    /// 清空所有引用（包含老板/员工/敌人）。
    /// </summary>
    public void ClearAll()
    {
        _player = null;
        _employees.Clear();
        _enemies.Clear();
    }

    /// <summary>
    /// 根据配置 Id 创建敌人实体（支持任意 Id）。
    /// </summary>
    /// <param name="enemyId">数据表 Id。</param>
    /// <param name="spawnPosition">生成位置（世界坐标，XY 平面）。</param>
    /// <param name="enemy">输出生成的实体。</param>
    public bool TryCreateEnemy(int enemyId, Vector2 spawnPosition, out EnemyEntity enemy)
    {
        enemy = null;
        if (enemyId <= 0)
        {
            CY.LogWarning("[UnitManager] 敌人 Id 非法，创建失败。");
            return false;
        }

        if (!_hasEnemyRows)
        {
            TryCacheEnemyRows();
        }

        if (!_hasEnemyRows || !_enemyRowMap.TryGetValue(enemyId, out var row))
        {
            CY.LogWarning($"[UnitManager] 未找到敌人配置，Id={enemyId}");
            return false;
        }

        enemy = CY.Entity.SpawnEntity<EnemyEntity>(row);
        if (enemy == null)
        {
            CY.LogError($"[UnitManager] 敌人实体生成失败，Id={enemyId}");
            return false;
        }

        var targetPos = enemy.transform.position;
        targetPos.x = spawnPosition.x;
        targetPos.y = spawnPosition.y;
        enemy.transform.position = targetPos;

        AddEnemy(enemy);
        return true;
    }

    /// <summary>
    /// 从已加载的数据表中缓存第一行玩家数据（不负责加载 CSV）。
    /// </summary>
    private void TryCacheDefaultPlayerRow()
    {
        if (!CY.Data.HasDataTable(PlayerTableName))
        {
            CY.LogWarning("玩家数据表未加载，无法读取默认数据。");
            return;
        }

        var table = CY.Data.GetDataTable<PlayerUnitRow>(PlayerTableName);
        if (table == null)
        {
            CY.LogWarning("玩家数据表为空，无法读取默认数据。");
            return;
        }

        var rows = table.GetAllRows();
        if (rows == null || rows.Count == 0)
        {
            CY.LogWarning("玩家数据表无有效行，无法读取默认数据。");
            return;
        }

        _defaultPlayerRow = rows[0];
        _hasDefaultPlayerRow = true;
    }

    /// <summary>
    /// 缓存敌人数据表（按 Id 存储行，便于任意 Id 查询）。
    /// </summary>
    private void TryCacheEnemyRows()
    {
        _enemyRowMap.Clear();
        _hasEnemyRows = false;

        if (!CY.Data.HasDataTable(EnemyTableName))
        {
            CY.LogWarning("敌人数据表未加载，无法创建敌人。");
            return;
        }

        var table = CY.Data.GetDataTable<EnemyUnitRow>(EnemyTableName);
        if (table == null)
        {
            CY.LogWarning("敌人数据表为空，无法创建敌人。");
            return;
        }

        var rows = table.GetAllRows();
        if (rows == null || rows.Count == 0)
        {
            CY.LogWarning("敌人数据表无有效行，无法创建敌人。");
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null)
            {
                continue;
            }

            _enemyRowMap[row.Id] = row;
        }

        _hasEnemyRows = true;
    }
}
