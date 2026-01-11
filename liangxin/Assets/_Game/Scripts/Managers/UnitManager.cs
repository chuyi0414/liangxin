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
    /// <summary>员工 CSV 数据表名。</summary>
    private const string EmployeeTableName = "Employee";
    /// <summary>单位风格 CSV 数据表名。</summary>
    private const string UnitStyleTableName = "UnitStyle";
    /// <summary>子弹数组 CSV 数据表名。</summary>
    private const string BulletArrayTableName = "BulletArray";
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
    /// <summary>员工数据缓存表（Id -> Row）。</summary>
    private readonly Dictionary<int, EmployeeUnitRow> _employeeRowMap = new Dictionary<int, EmployeeUnitRow>(32);
    /// <summary>是否已缓存员工数据表。</summary>
    private bool _hasEmployeeRows;
    /// <summary>单位风格数据缓存表（Id -> Row）。</summary>
    private readonly Dictionary<int, UnitStyleRow> _unitStyleRowMap = new Dictionary<int, UnitStyleRow>(16);
    /// <summary>是否已缓存单位风格数据表。</summary>
    private bool _hasUnitStyleRows;
    /// <summary>子弹数组数据缓存表（Id -> Row）。</summary>
    private readonly Dictionary<int, BulletArrayRow> _bulletArrayRowMap = new Dictionary<int, BulletArrayRow>(32);
    /// <summary>是否已缓存子弹数组数据表。</summary>
    private bool _hasBulletArrayRows;

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

    /// <summary>
    /// 获取员工数据（若未缓存会尝试读取，失败返回 false）。
    /// </summary>
    /// <param name="employeeId">员工配置 Id。</param>
    /// <param name="row">输出员工数据行。</param>
    public bool TryGetEmployeeRow(int employeeId, out EmployeeUnitRow row) // 员工数据查询入口
    {
        row = null; // 默认输出为空
        if (employeeId <= 0) // Id 无效判定
        {
            return false; // Id 无效时直接返回
        }

        if (!_hasEmployeeRows) // 缓存缺失判定
        {
            TryCacheEmployeeRows(); // 未缓存时尝试读取
        }

        if (!_hasEmployeeRows) // 缓存失败判定
        {
            return false; // 缓存失败时返回 false
        }

        return _employeeRowMap.TryGetValue(employeeId, out row); // 返回查询结果
    }

    /// <summary>
    /// 获取单位风格数据（若未缓存会尝试读取，失败返回 false）。
    /// </summary>
    /// <param name="styleId">风格配置 Id。</param>
    /// <param name="row">输出风格数据行。</param>
    public bool TryGetUnitStyleRow(int styleId, out UnitStyleRow row) // 单位风格查询入口
    {
        row = null; // 默认输出为空
        if (styleId <= 0) // Id 无效判定
        {
            return false; // Id 无效时直接返回
        }

        if (!_hasUnitStyleRows) // 缓存缺失判定
        {
            TryCacheUnitStyleRows(); // 未缓存时尝试读取
        }

        if (!_hasUnitStyleRows) // 缓存失败判定
        {
            return false; // 缓存失败时返回 false
        }

        return _unitStyleRowMap.TryGetValue(styleId, out row); // 返回查询结果
    }

    /// <summary>
    /// 获取子弹数组数据（若未缓存会尝试读取，失败返回 false）。
    /// </summary>
    /// <param name="bulletArrayId">子弹数组 Id。</param>
    /// <param name="row">输出子弹数组数据行。</param>
    public bool TryGetBulletArrayRow(int bulletArrayId, out BulletArrayRow row) // 子弹数组查询入口
    {
        row = null; // 默认输出为空
        if (bulletArrayId <= 0)
        {
            return false; // Id 无效时直接返回
        }

        if (!_hasBulletArrayRows)
        {
            TryCacheBulletArrayRows(); // 未缓存时尝试读取
        }

        if (!_hasBulletArrayRows)
        {
            return false; // 缓存失败时返回 false
        }

        return _bulletArrayRowMap.TryGetValue(bulletArrayId, out row); // 返回查询结果
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
        TryCacheEmployeeRows(); // 缓存员工数据表
        TryCacheUnitStyleRows(); // 缓存单位风格数据表
        TryCacheBulletArrayRows(); // 缓存子弹数组数据表
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
        _employeeRowMap.Clear(); // 清空员工数据缓存
        _hasEmployeeRows = false; // 重置员工数据缓存标记
        _unitStyleRowMap.Clear(); // 清空单位风格缓存
        _hasUnitStyleRows = false; // 重置单位风格缓存标记
        _bulletArrayRowMap.Clear(); // 清空子弹数组缓存
        _hasBulletArrayRows = false; // 重置子弹数组缓存标记
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

        if (row != null && !string.IsNullOrEmpty(row.PrefabPath)) // 预制体路径存在判定
        {
            enemy = CY.Entity.SpawnEntity<EnemyEntity>(row.Code, row.PrefabPath, "Enemys", row); // 使用 CSV 的预制体路径生成敌人实体
        }
        else // 路径缺失分支
        {
            enemy = CY.Entity.SpawnEntity<EnemyEntity>(row); // 回退为脚本 [EntityPrefab] 默认路径生成敌人实体
        }
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
    /// 根据配置 Id 创建员工实体（支持任意 Id）。
    /// </summary>
    /// <param name="employeeId">数据表 Id。</param>
    /// <param name="spawnPosition">生成位置（世界坐标，XY 平面）。</param>
    /// <param name="employee">输出生成的员工单位（支持不同员工脚本）。</param>
    /// <returns>是否创建成功。</returns>
    public bool TryCreateEmployee(int employeeId, Vector2 spawnPosition, out UnitEntity employee) // 员工创建入口
    {
        employee = null; // 默认输出为空
        if (employeeId <= 0) // Id 非法判定
        {
            CY.LogWarning("[UnitManager] 员工 Id 非法，创建失败。"); // 输出非法 Id 警告
            return false; // 非法 Id 时返回失败
        }

        if (!_hasEmployeeRows) // 未缓存员工数据表判定
        {
            TryCacheEmployeeRows(); // 尝试缓存员工数据表
        }

        if (!_hasEmployeeRows || !_employeeRowMap.TryGetValue(employeeId, out var row)) // 缓存失败或未找到行判定
        {
            CY.LogWarning($"[UnitManager] 未找到员工配置，Id={employeeId}"); // 输出未找到配置日志
            return false; // 未找到配置时返回失败
        }

        var preShowData = new EmployeePreShowData // 组装员工预显示出生点数据（激活前设置位置）
        {
            HasPosition = true, // 标记出生点有效
            Position = new Vector3(spawnPosition.x, spawnPosition.y, 0f) // 写入 XY 出生点（Z 在应用时保留）
        };

        if (row != null && !string.IsNullOrEmpty(row.PrefabPath)) // 预制体路径存在判定
        {
            var spawned = CY.Entity.SpawnEntity<CYFramework.Core.Entity.IEntity, EmployeePreShowData>(row.Code, row.PrefabPath, "Employees", ref preShowData, row); // 使用 CSV 预制体路径生成员工实体（不依赖具体员工脚本类型）
            employee = spawned as UnitEntity; // 转换为单位基类（员工脚本需继承 UnitEntity）
        }
        else // 路径缺失分支
        {
            var spawned = CY.Entity.SpawnEntity<EmployeeEntity, EmployeePreShowData>(row.Code, ref preShowData, row); // 回退为 EmployeeEntity 默认预制体生成（预显示设置出生点）
            employee = spawned; // EmployeeEntity 继承 UnitEntity，可直接赋值
        }

        if (employee == null) // 生成失败判定
        {
            CY.LogError($"[UnitManager] 员工实体生成失败，Id={employeeId}"); // 输出生成失败错误日志
            return false; // 生成失败时返回失败
        }

        // 注意：员工实体在本框架中会延迟到下一帧 OnShow 才真正进入“可见/可交互”状态。
        // 若此处立刻加入 Employees 列表，敌人 AI 可能在员工显示前就将其当作目标并攻击，导致“刚创建就飘字/掉血”的错觉。
        // 最优做法：由 EmployeeEntity 在 OnEntityShow 时加入列表，在 Hide/Recycle 时移除，确保列表只包含可见单位。
        return true; // 返回创建成功
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

    /// <summary>
    /// 缓存员工数据表（按 Id 存储行，便于任意 Id 查询）。
    /// </summary>
    private void TryCacheEmployeeRows() // 员工数据缓存入口
    {
        _employeeRowMap.Clear(); // 清空旧缓存
        _hasEmployeeRows = false; // 重置缓存标记

        if (!CY.Data.HasDataTable(EmployeeTableName)) // 判断数据表是否已加载
        {
            CY.LogWarning("员工数据表未加载，无法读取员工配置。"); // 输出警告日志
            return; // 数据表未加载时退出
        }

        var table = CY.Data.GetDataTable<EmployeeUnitRow>(EmployeeTableName); // 获取员工数据表
        if (table == null) // 数据表为空判定
        {
            CY.LogWarning("员工数据表为空，无法读取员工配置。"); // 输出警告日志
            return; // 数据表为空时退出
        }

        var rows = table.GetAllRows(); // 获取所有数据行
        if (rows == null || rows.Count == 0) // 无有效行判定
        {
            CY.LogWarning("员工数据表无有效行，无法读取员工配置。"); // 输出警告日志
            return; // 无有效行时退出
        }

        for (int i = 0; i < rows.Count; i++) // 遍历数据行
        {
            var row = rows[i]; // 获取当前数据行
            if (row == null) // 空行判定
            {
                continue; // 空行时跳过
            }

            _employeeRowMap[row.Id] = row; // 写入缓存字典
        }

        _hasEmployeeRows = true; // 标记缓存完成
    }

    /// <summary>
    /// 缓存单位风格数据表（按 Id 存储行，便于任意 Id 查询）。
    /// </summary>
    private void TryCacheUnitStyleRows() // 单位风格缓存入口
    {
        _unitStyleRowMap.Clear(); // 清空旧缓存
        _hasUnitStyleRows = false; // 重置缓存标记

        if (!CY.Data.HasDataTable(UnitStyleTableName)) // 判断数据表是否已加载
        {
            CY.LogWarning("单位风格数据表未加载，无法读取风格配置。"); // 输出警告日志
            return; // 数据表未加载时退出
        }

        var table = CY.Data.GetDataTable<UnitStyleRow>(UnitStyleTableName); // 获取单位风格数据表
        if (table == null) // 数据表为空判定
        {
            CY.LogWarning("单位风格数据表为空，无法读取风格配置。"); // 输出警告日志
            return; // 数据表为空时退出
        }

        var rows = table.GetAllRows(); // 获取所有数据行
        if (rows == null || rows.Count == 0) // 无有效行判定
        {
            CY.LogWarning("单位风格数据表无有效行，无法读取风格配置。"); // 输出警告日志
            return; // 无有效行时退出
        }

        for (int i = 0; i < rows.Count; i++) // 遍历数据行
        {
            var row = rows[i]; // 获取当前数据行
            if (row == null) // 空行判定
            {
                continue; // 空行时跳过
            }

            _unitStyleRowMap[row.Id] = row; // 写入缓存字典
        }

        _hasUnitStyleRows = true; // 标记缓存完成
    }

    /// <summary>
    /// 缓存子弹数组数据表（按 Id 存储行，便于任意 Id 查询）。
    /// </summary>
    private void TryCacheBulletArrayRows() // 子弹数组缓存入口
    {
        _bulletArrayRowMap.Clear(); // 清空旧缓存
        _hasBulletArrayRows = false; // 重置缓存标记

        if (!CY.Data.HasDataTable(BulletArrayTableName))
        {
            CY.LogWarning("子弹数组数据表未加载，无法读取子弹数组配置。"); // 输出警告日志
            return; // 数据表未加载时退出
        }

        var table = CY.Data.GetDataTable<BulletArrayRow>(BulletArrayTableName); // 获取子弹数组数据表
        if (table == null)
        {
            CY.LogWarning("子弹数组数据表为空，无法读取子弹数组配置。"); // 输出警告日志
            return; // 数据表为空时退出
        }

        var rows = table.GetAllRows(); // 获取所有数据行
        if (rows == null || rows.Count == 0)
        {
            CY.LogWarning("子弹数组数据表无有效行，无法读取子弹数组配置。"); // 输出警告日志
            return; // 无有效行时退出
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; // 获取当前数据行
            if (row == null)
            {
                continue; // 空行时跳过
            }

            _bulletArrayRowMap[row.Id] = row; // 写入缓存字典
        }

        _hasBulletArrayRows = true; // 标记缓存完成
    }
}
