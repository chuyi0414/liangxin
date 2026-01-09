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
    private const string SpawnTypeTableName = "SpawnType"; // 生成类型表名
    private const string SpawnTypeCsvPath = "DataTable/Wave/SpawnType"; // 生成类型路径
    private const string AssaultSpawnTypeTableName = "AssaultSpawnType"; // 奇袭生成类型表名
    private const string AssaultSpawnTypeCsvPath = "DataTable/Wave/AssaultSpawnType"; // 奇袭生成类型路径

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
        LoadSpawnTypeData(); // 加载生成类型
        LoadAssaultSpawnTypeData(); // 加载奇袭生成类型
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
    /// 加载生成类型数据（CSV）。
    /// </summary>
    private void LoadSpawnTypeData() // 加载生成类型表
    {
        if (CY.Data.HasDataTable(SpawnTypeTableName)) // 判断是否已加载
        {
            CY.Data.UnloadDataTable(SpawnTypeTableName); // 卸载旧表
        }

        var csvAsset = CY.Resource.Load<TextAsset>(SpawnTypeCsvPath); // 加载 CSV 资源
        if (csvAsset == null) // 资源为空判定
        {
            CY.LogError($"[LoadProcedure] 加载生成类型失败：{SpawnTypeCsvPath}"); // 输出错误日志
            return; // 直接退出
        }

        CY.Data.LoadFromCsv<SpawnTypeRow>(csvAsset.text, SpawnTypeTableName); // 解析生成类型表
    }

    /// <summary>
    /// 加载奇袭生成类型数据（CSV）。
    /// </summary>
    private void LoadAssaultSpawnTypeData() // 加载奇袭生成类型表
    {
        if (CY.Data.HasDataTable(AssaultSpawnTypeTableName)) // 判断是否已加载
        {
            CY.Data.UnloadDataTable(AssaultSpawnTypeTableName); // 卸载旧表
        }

        var csvAsset = CY.Resource.Load<TextAsset>(AssaultSpawnTypeCsvPath); // 加载 CSV 资源
        if (csvAsset == null) // 资源为空判定
        {
            CY.LogWarning($"[LoadProcedure] 加载奇袭生成类型失败：{AssaultSpawnTypeCsvPath}"); // 输出警告日志
            return; // 允许奇袭表缺失
        }

        CY.Data.LoadFromCsv<AssaultSpawnTypeRow>(csvAsset.text, AssaultSpawnTypeTableName); // 解析奇袭生成类型表
    }


}
