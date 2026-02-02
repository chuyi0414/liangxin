using GameFramework.DataTable;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 关卡游戏管理类
/// </summary>
public class GameManager : GameFrameworkComponent
{
    private IDataTable<DRProjectile> _dRProjectiles;
    /// <summary>
    /// 子弹数据表
    /// </summary>
    public IDataTable<DRProjectile> DRProjectiles
    {
        get
        {
            return _dRProjectiles;
        }
        set
        {
            _dRProjectiles = value;
        }
    }

    private IDataTable<DRProtagonist> _dRProtagonists;
    /// <summary>
    /// 主角数据表
    /// </summary>
    public IDataTable<DRProtagonist> DRProtagonists
    {
        get
        {
            return _dRProtagonists;
        }
        set
        {
            _dRProtagonists = value;
        }
    }

    private IDataTable<DREnemy> _drEnemys;
    public IDataTable<DREnemy> DREnemies
    {
        get
        {
            return _drEnemys;
        }
        set
        {
            _drEnemys = value;
        }
    }

    private IDataTable<DRBattleData> _dRBattleDatas;
    /// <summary>
    /// 战斗数据表
    /// </summary>
    public IDataTable<DRBattleData> DRBattleDatas
    {
        get
        {
            return _dRBattleDatas;
        }
        set
        {
            _dRBattleDatas = value;
        }
    }
    /// <summary>
    /// 主角
    /// </summary>
    public ProtagonistEntity protagonistEntity;
    /// <summary>
    /// 公司
    /// </summary>
    public CompanyEntity companyEntity;
    /// <summary>
    /// 关键单位列表（主角 + 员工）
    /// </summary>
    private List<UnitBaseEntity> _keyUnits = new List<UnitBaseEntity>();
    /// <summary>
    /// 单位分帧更新管理器（负责空间网格等）
    /// </summary>
    public UnitBatchUpdateManager UnitBatchUpdateManager { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        // 获取场景中已有的 UnitBatchUpdateManager
        UnitBatchUpdateManager = GetComponent<UnitBatchUpdateManager>();

        // 如果没有，就动态创建一个挂到自己身上
        if (UnitBatchUpdateManager == null)
        {
            UnitBatchUpdateManager = gameObject.AddComponent<UnitBatchUpdateManager>();
        }
    }
    
    /// <summary>
    /// 在指定地方创建敌人
    /// </summary>
    public void TryCreationEnemy(string id,Vector3 v3)
    {
        DREnemy dREnemy = DREnemies.GetDataRow(1);
        GameEntry.Entity.ShowEntity<EnemyEntity_JW>(
            GameEntry.EntityIdPool.Acquire(),
            dREnemy.PrefabPath,
            "Enemy",
            new object[]
            {
                v3,
                dREnemy
            });
    }

    /// <summary>
    /// 在指定位置创建员工
    /// </summary>
    /// <param name="prefabPath">员工预制体路径</param>
    /// <param name="pos">生成位置</param>
    public void CreateEmployee(string prefabPath, Vector3 pos)
    {
        GameEntry.Entity.ShowEntity<EmployeeEntity>(
            GameEntry.EntityIdPool.Acquire(),
            prefabPath,
            "Employee",
            new object[]
            {
                pos
            });
    }

    /// <summary>
    /// 注册关键单位（主角或员工）
    /// </summary>
    /// <param name="unit">需要注册的关键单位实体</param>
    public void RegisterKeyUnit(UnitBaseEntity unit)
    {
        if (unit == null) return;
        if (_keyUnits.Contains(unit)) return;
        _keyUnits.Add(unit);
    }
    /// <summary>
    /// 注销关键单位（主角或员工）
    /// </summary>
    /// <param name="unit">需要注销的关键单位实体</param>
    public void UnregisterKeyUnit(UnitBaseEntity unit)
    {
        if (unit == null) return;
        _keyUnits.Remove(unit);
    }
    /// <summary>
    /// 获取到最近关键单位的距离
    /// </summary>
    /// <param name="position">要计算的世界坐标</param>
    /// <returns>最近关键单位距离；没有关键单位则返回 float.MaxValue</returns>
    public float GetDistanceToNearestKeyUnit(Vector3 position)
    {
        if (_keyUnits.Count == 0) return float.MaxValue;

        float minDistance = float.MaxValue;
        for (int i = 0; i < _keyUnits.Count; i++)
        {
            UnitBaseEntity unit = _keyUnits[i];
            if (unit == null || !unit.gameObject.activeSelf) continue;

            float distance = Vector3.Distance(position, unit.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        return minDistance;
    }
    /// <summary>
    /// 获取最近的关键单位
    /// </summary>
    /// <param name="position">要计算的世界坐标</param>
    /// <returns>最近关键单位；没有则返回 null</returns>
    public UnitBaseEntity GetNearestKeyUnit(Vector3 position)
    {
        if (_keyUnits.Count == 0) return null;

        UnitBaseEntity nearest = null;
        float minDistance = float.MaxValue;
        for (int i = 0; i < _keyUnits.Count; i++)
        {
            UnitBaseEntity unit = _keyUnits[i];
            if (unit == null || !unit.gameObject.activeSelf) continue;

            float distance = Vector3.Distance(position, unit.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = unit;
            }
        }

        return nearest;
    }
}

/// <summary>
/// 敌人AI统一配置（纯 C# 静态配置类）
/// </summary>
public static class EnemyAIConfig
{
    /// <summary>可视范围默认距离</summary>
    public static float VisualScopeDistance = 6f;
    /// <summary>攻击范围默认距离</summary>
    public static float AttackRangeDistance = 2f;

    /// <summary>AI 等级 - Full 距离阈值</summary>
    public static float LevelFullDistance = 40f;
    /// <summary>AI 等级 - LowFrequency 距离阈值</summary>
    public static float LevelLowDistance = 70f;
    /// <summary>AI 等级 - Simplified 距离阈值</summary>
    public static float LevelSimplifiedDistance = 100f;

    /// <summary>路径重算间隔（Full）</summary>
    public static float PathIntervalFull = 0.5f;
    /// <summary>路径重算间隔（LowFrequency）</summary>
    public static float PathIntervalLow = 1f;
    /// <summary>路径重算间隔（Simplified）</summary>
    public static float PathIntervalSimplified = 1.8f;
    /// <summary>路径重算间隔（Minimal）</summary>
    public static float PathIntervalMinimal = 3f;

    /// <summary>空间查询间隔</summary>
    public static float SpatialQueryInterval = 0.15f;
    /// <summary>是否在 Full 等级也使用空间查询</summary>
    public static bool UseSpatialQueryInFull = true;

    /// <summary>空间网格单元大小</summary>
    public static float GridCellSize = 6f;
    /// <summary>分帧更新每帧单位数量</summary>
    public static int UnitsUpdatePerFrame = 25;

    /// <summary>每帧最大寻路重算次数</summary>
    public static int FlowFieldMaxPerFrame = 10;

    /// <summary>动态障碍最小移动距离</summary>
    public static float DynamicObstacleMinMove = 0.2f;
    /// <summary>动态障碍更新间隔</summary>
    public static float DynamicObstacleUpdateInterval = 0.5f;

    /// <summary>A* 网格节点大小</summary>
    public static float GridNodeSize = 0.5f;
}
