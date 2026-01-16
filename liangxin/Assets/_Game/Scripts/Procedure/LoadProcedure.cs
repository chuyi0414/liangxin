using CYFramework;
using CYFramework.Core.Procedure;
using UnityEngine;

[AutoRegisterProcedure(name:"Load",order:0)]
public class LoadProcedure : ProcedureBase
{
    private const string BattleDataTableName = "BattleData";
    private const string BattleDataJsonPath = "DataTable/Game/BattleData";
    private const string PlayerDataTableName = "Player";
    private const string PlayerDataCsvPath = "DataTable/Unit/Player/Player";
    private const string EmployeeDataTableName = "Employee"; // 员工数据表名
    private const string EmployeeDataCsvPath = "DataTable/Unit/Employee/Employee"; // 员工数据表路径
    private const string UnitStyleTableName = "UnitStyle"; // 单位风格表名
    private const string UnitStyleCsvPath = "DataTable/Unit/Employee/Style/UnitStyle"; // 单位风格表路径
    private const string BulletArrayTableName = "BulletArray"; // 子弹数组表名
    private const string BulletArrayCsvPath = "DataTable/Projectiles/BulletArray"; // 子弹数组表路径
    private const string EnemyDataTableName = "Enemy";
    private const string EnemyDataCsvPath = "DataTable/Unit/Enemy/Enemy";
    private const string WavePlanTableName = "WavePlan"; // 波次计划表名
    private const string WavePlanCsvPath = "DataTable/Wave/WavePlan"; // 波次计划表路径
    private const string WaveTrackTableName = "WaveTrack"; // 波次轨道表名
    private const string WaveTrackCsvPath = "DataTable/Wave/WaveTrack"; // 波次轨道表路径
    private const string WaveSpawnGroupTableName = "WaveSpawnGroup"; // 波次刷怪组表名
    private const string WaveSpawnGroupCsvPath = "DataTable/Wave/WaveSpawnGroup"; // 波次刷怪组表路径

    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        base.OnEnter(previousProcedure);

        CY.UI.Open<LoadUIPanel>();
        LoadBattleData();
        LoadPlayerData();
        LoadEmployeeData(); // 加载员工数据
        LoadUnitStyleData(); // 加载单位风格数据
        LoadBulletArrayData(); // 加载子弹数组数据
        LoadEnemyData();
        LoadWavePlanData(); // 加载波次计划表
        LoadWaveTrackData(); // 加载波次轨道表
        LoadWaveSpawnGroupData(); // 加载刷怪组表
    }

    protected override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

    }

    protected override void OnLeave(ProcedureBase nextProcedure)
    {
        base.OnLeave(nextProcedure);
        CY.UI.Close<LoadUIPanel>();
    }

    /// <summary>
    /// 加载战斗初始数据（JSON 单对象）。
    /// </summary>
    private void LoadBattleData()
    {
        if (CY.Data.HasDataTable(BattleDataTableName))
        {
            CY.Data.UnloadDataTable(BattleDataTableName);
        }

        var jsonAsset = CY.Resource.Load<TextAsset>(BattleDataJsonPath);
        if (jsonAsset == null)
        {
            CY.LogError($"[LoadProcedure] 加载战斗数据失败：{BattleDataJsonPath}");
            return;
        }

        CY.Data.LoadFromJsonObject<BattleData>(jsonAsset.text, BattleDataTableName, autoFixIdIfZero: true);
    }

    /// <summary>
    /// 加载玩家数据（CSV，默认使用第一行）。
    /// </summary>
    private void LoadPlayerData()
    {
        if (CY.Data.HasDataTable(PlayerDataTableName))
        {
            CY.Data.UnloadDataTable(PlayerDataTableName);
        }

        var csvAsset = CY.Resource.Load<TextAsset>(PlayerDataCsvPath);
        if (csvAsset == null)
        {
            CY.LogError($"[LoadProcedure] 加载玩家数据失败：{PlayerDataCsvPath}");
            return;
        }

        CY.Data.LoadFromCsv<PlayerUnitRow>(csvAsset.text, PlayerDataTableName);
    }

    /// <summary>
    /// 加载员工数据（CSV）。
    /// </summary>
    private void LoadEmployeeData() // 员工数据加载入口
    {
        if (CY.Data.HasDataTable(EmployeeDataTableName)) // 判断是否已加载
        {
            CY.Data.UnloadDataTable(EmployeeDataTableName); // 卸载旧表
        }

        var csvAsset = CY.Resource.Load<TextAsset>(EmployeeDataCsvPath); // 加载 CSV 资源
        if (csvAsset == null) // 资源为空判定
        {
            CY.LogError($"[LoadProcedure] 加载员工数据失败：{EmployeeDataCsvPath}"); // 输出错误日志
            return; // 资源为空时退出
        }

        CY.Data.LoadFromCsv<EmployeeUnitRow>(csvAsset.text, EmployeeDataTableName); // 解析员工数据表
    }

    /// <summary>
    /// 加载单位风格数据（CSV）。
    /// </summary>
    private void LoadUnitStyleData() // 单位风格数据加载入口
    {
        if (CY.Data.HasDataTable(UnitStyleTableName)) // 判断是否已加载
        {
            CY.Data.UnloadDataTable(UnitStyleTableName); // 卸载旧表
        }

        var csvAsset = CY.Resource.Load<TextAsset>(UnitStyleCsvPath); // 加载 CSV 资源
        if (csvAsset == null) // 资源为空判定
        {
            CY.LogError($"[LoadProcedure] 加载单位风格数据失败：{UnitStyleCsvPath}"); // 输出错误日志
            return; // 资源为空时退出
        }

        CY.Data.LoadFromCsv<UnitStyleRow>(csvAsset.text, UnitStyleTableName); // 解析单位风格数据表
    }

    /// <summary>
    /// 加载子弹数组数据（CSV，通用单位使用）。
    /// </summary>
    private void LoadBulletArrayData() // 子弹数组加载入口
    {
        if (CY.Data.HasDataTable(BulletArrayTableName))
        {
            CY.Data.UnloadDataTable(BulletArrayTableName); // 卸载旧表
        }

        var csvAsset = CY.Resource.Load<TextAsset>(BulletArrayCsvPath); // 加载 CSV 资源
        if (csvAsset == null)
        {
            CY.LogError($"[LoadProcedure] 加载子弹数组失败：{BulletArrayCsvPath}"); // 输出错误日志
            return; // 资源为空时退出
        }

        CY.Data.LoadFromCsv<BulletArrayRow>(csvAsset.text, BulletArrayTableName); // 解析子弹数组表
    }

    /// <summary>
    /// 加载敌人数据（CSV，支持任意 Id 查询）。
    /// </summary>
    private void LoadEnemyData()
    {
        if (CY.Data.HasDataTable(EnemyDataTableName))
        {
            CY.Data.UnloadDataTable(EnemyDataTableName);
        }

        var csvAsset = CY.Resource.Load<TextAsset>(EnemyDataCsvPath);
        if (csvAsset == null)
        {
            CY.LogError($"[LoadProcedure] 加载敌人数据失败：{EnemyDataCsvPath}");
            return;
        }

        CY.Data.LoadFromCsv<EnemyUnitRow>(csvAsset.text, EnemyDataTableName);
    }

    /// <summary>
    /// 加载波次计划数据（CSV）。
    /// </summary>
    private void LoadWavePlanData() // 波次计划表加载入口
    {
        if (CY.Data.HasDataTable(WavePlanTableName)) // 判断是否已加载
        {
            CY.Data.UnloadDataTable(WavePlanTableName); // 卸载旧表
        }

        var csvAsset = CY.Resource.Load<TextAsset>(WavePlanCsvPath); // 加载 CSV 资源
        if (csvAsset == null) // 资源为空判定
        {
            CY.LogError($"[LoadProcedure] 加载波次计划失败：{WavePlanCsvPath}"); // 输出错误日志
            return; // 资源为空时退出
        }

        CY.Data.LoadFromCsv<WavePlanRow>(csvAsset.text, WavePlanTableName); // 解析波次计划表
    }

    /// <summary>
    /// 加载波次轨道数据（CSV）。
    /// </summary>
    private void LoadWaveTrackData() // 波次轨道表加载入口
    {
        if (CY.Data.HasDataTable(WaveTrackTableName)) // 判断是否已加载
        {
            CY.Data.UnloadDataTable(WaveTrackTableName); // 卸载旧表
        }

        var csvAsset = CY.Resource.Load<TextAsset>(WaveTrackCsvPath); // 加载 CSV 资源
        if (csvAsset == null) // 资源为空判定
        {
            CY.LogError($"[LoadProcedure] 加载波次轨道失败：{WaveTrackCsvPath}"); // 输出错误日志
            return; // 资源为空时退出
        }

        CY.Data.LoadFromCsv<WaveTrackRow>(csvAsset.text, WaveTrackTableName); // 解析波次轨道表
    }

    /// <summary>
    /// 加载波次刷怪组数据（CSV）。
    /// </summary>
    private void LoadWaveSpawnGroupData() // 波次刷怪组表加载入口
    {
        if (CY.Data.HasDataTable(WaveSpawnGroupTableName)) // 判断是否已加载
        {
            CY.Data.UnloadDataTable(WaveSpawnGroupTableName); // 卸载旧表
        }

        var csvAsset = CY.Resource.Load<TextAsset>(WaveSpawnGroupCsvPath); // 加载 CSV 资源
        if (csvAsset == null) // 资源为空判定
        {
            CY.LogError($"[LoadProcedure] 加载刷怪组失败：{WaveSpawnGroupCsvPath}"); // 输出错误日志
            return; // 资源为空时退出
        }

        CY.Data.LoadFromCsv<WaveSpawnGroupRow>(csvAsset.text, WaveSpawnGroupTableName); // 解析刷怪组表
    }


}
