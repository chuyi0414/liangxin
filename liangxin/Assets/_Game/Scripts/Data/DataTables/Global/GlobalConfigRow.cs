using CYFramework;
using CYFramework.Core.DataTable;
using UnityEngine;

/// <summary>
/// 全局配置表行数据 (Key-Value)
/// 对应 Assets/_Game/Resources/DataTables/Global/GlobalConfig.csv
/// </summary>
public class GlobalConfigRow : IDataRow
{
    public int Id => Key != null ? Key.GetHashCode() : 0;

    public string Key;
    public int ValueInt;
    public float ValueFloat;
    public string ValueString;
    public string Description;

    public void ParseRow(string[] dataRow)
    {
        // 假设 CSV 列顺序: Key, ValueInt, ValueFloat, ValueString, Description
        if (dataRow.Length > 0) Key = dataRow[0];
        if (dataRow.Length > 1) int.TryParse(dataRow[1], out ValueInt);
        if (dataRow.Length > 2) float.TryParse(dataRow[2], out ValueFloat);
        if (dataRow.Length > 3) ValueString = dataRow[3];
        if (dataRow.Length > 4) Description = dataRow[4];
    }
}
