using CYFramework;
using CYFramework.Core.Procedure;
using UnityEngine;

[AutoRegisterProcedure(name:"Load",order:0)]
public class LoadProcedure : ProcedureBase
{
    private const string BattleDataTableName = "BattleData";
    private const string BattleDataJsonPath = "DataTable/Game/BattleData";

    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        base.OnEnter(previousProcedure);

        CY.UI.Open<LoadUIPanel>();
        LoadBattleData();
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
}
