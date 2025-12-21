// ============================================================================
// CYFramework - 数据表管理器
// 管理游戏配置数据（怪物、技能、关卡等配置表）
// ============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Core.DataTable
{
    /// <summary>
    /// 数据行接口
    /// </summary>
    public interface IDataRow
    {
        int Id { get; }
        void ParseRow(string[] values);
    }
    
    /// <summary>
    /// 数据表接口
    /// </summary>
    public interface IDataTable
    {
        string Name { get; }
        Type RowType { get; }
        int Count { get; }
        void Clear();
    }
    
    /// <summary>
    /// 泛型数据表
    /// </summary>
    public class DataTable<T> : IDataTable where T : class, IDataRow, new()
    {
        public string Name { get; }
        public Type RowType => typeof(T);
        public int Count => _dataRows.Count;
        
        private readonly Dictionary<int, T> _dataRows = new();
        private readonly List<T> _dataRowList = new();
        
        public DataTable(string name)
        {
            Name = name;
        }
        
        /// <summary>
        /// 添加数据行
        /// </summary>
        public void AddRow(T row)
        {
            if (_dataRows.ContainsKey(row.Id))
            {
                CYLog.Warning($"[DataTable] 重复的行 ID: {Name}[{row.Id}]");
                return;
            }
            
            _dataRows[row.Id] = row;
            _dataRowList.Add(row);
        }
        
        /// <summary>
        /// 获取数据行
        /// </summary>
        public T GetRow(int id)
        {
            return _dataRows.TryGetValue(id, out var row) ? row : null;
        }
        
        /// <summary>
        /// 获取数据行（条件查询）
        /// </summary>
        public T GetRow(Predicate<T> predicate)
        {
            foreach (var row in _dataRowList)
            {
                if (predicate(row))
                {
                    return row;
                }
            }
            return null;
        }
        
        /// <summary>
        /// 获取所有数据行
        /// </summary>
        public IReadOnlyList<T> GetAllRows()
        {
            return _dataRowList;
        }
        
        /// <summary>
        /// 获取所有数据行（条件查询）
        /// 注意：每次调用会创建新 List，高频场景请使用 GetRowsNonAlloc
        /// </summary>
        public List<T> GetRows(Predicate<T> predicate)
        {
            var result = new List<T>();
            foreach (var row in _dataRowList)
            {
                if (predicate(row))
                {
                    result.Add(row);
                }
            }
            return result;
        }
        
        /// <summary>
        /// 获取所有数据行（零 GC 版本，复用调用方传入的 List）
        /// </summary>
        public void GetRowsNonAlloc(Predicate<T> predicate, List<T> result)
        {
            result.Clear();
            foreach (var row in _dataRowList)
            {
                if (predicate(row))
                {
                    result.Add(row);
                }
            }
        }
        
        /// <summary>
        /// 是否存在
        /// </summary>
        public bool HasRow(int id)
        {
            return _dataRows.ContainsKey(id);
        }
        
        /// <summary>
        /// 是否存在
        /// </summary>
        public bool HasRow(Predicate<T> predicate)
        {
            foreach (var row in _dataRowList)
            {
                if (predicate(row))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 清空
        /// </summary>
        public void Clear()
        {
            _dataRows.Clear();
            _dataRowList.Clear();
        }
    }
    
    /// <summary>
    /// 数据表管理器
    /// </summary>
    public class DataTableManager : IDisposableEx
    {
        public int DisposeOrder => 0;
        
        private readonly Dictionary<string, IDataTable> _dataTables = new();
        
        /// <summary>
        /// 创建数据表
        /// </summary>
        public DataTable<T> CreateDataTable<T>(string name = null) where T : class, IDataRow, new()
        {
            name ??= typeof(T).Name;
            
            if (_dataTables.ContainsKey(name))
            {
                CYLog.Warning($"[DataTableManager] 数据表已存在: {name}");
                return GetDataTable<T>(name);
            }
            
            var dataTable = new DataTable<T>(name);
            _dataTables[name] = dataTable;
            
            CYLog.Debug($"[DataTableManager] 创建数据表: {name}");
            return dataTable;
        }
        
        /// <summary>
        /// 获取数据表
        /// </summary>
        public DataTable<T> GetDataTable<T>(string name = null) where T : class, IDataRow, new()
        {
            name ??= typeof(T).Name;
            
            if (_dataTables.TryGetValue(name, out var dataTable))
            {
                return dataTable as DataTable<T>;
            }
            
            return null;
        }
        
        /// <summary>
        /// 是否存在数据表
        /// </summary>
        public bool HasDataTable(string name)
        {
            return _dataTables.ContainsKey(name);
        }
        
        /// <summary>
        /// 从 CSV 文本加载数据表
        /// </summary>
        public DataTable<T> LoadFromCsv<T>(string csvText, string name = null, char separator = ',') where T : class, IDataRow, new()
        {
            var dataTable = CreateDataTable<T>(name);
            
            var lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            // 跳过表头（第一行）
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                {
                    continue; // 跳过空行和注释
                }
                
                var values = ParseCsvLine(line, separator);
                
                try
                {
                    var row = new T();
                    row.ParseRow(values);
                    dataTable.AddRow(row);
                }
                catch (Exception ex)
                {
                    CYLog.Error($"[DataTableManager] 解析数据行失败: {name}[{i}] - {ex.Message}");
                }
            }
            
            CYLog.Info($"[DataTableManager] 加载数据表: {name}, 行数: {dataTable.Count}");
            return dataTable;
        }

        /// <summary>
        /// 从 JSON 文本加载数据表（要求外层为 rows/Rows 数组包装）。
        /// </summary>
        /// <remarks>
        /// JsonUtility 不支持根数组，因此 JSON 必须为：{ "rows": [ { ... }, ... ] }
        /// T 需可被 JsonUtility 反序列化（建议加 [Serializable]，字段使用 public 或 [SerializeField]）。
        /// </remarks>
        public DataTable<T> LoadFromJson<T>(string jsonText, string name = null) where T : class, IDataRow, new()
        {
            var dataTable = CreateDataTable<T>(name);

            if (string.IsNullOrEmpty(jsonText))
            {
                CYLog.Warning($"[DataTableManager] JSON 为空，加载失败: {name ?? typeof(T).Name}");
                return dataTable;
            }

            JsonTableWrapper<T> wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<JsonTableWrapper<T>>(jsonText);
            }
            catch (Exception ex)
            {
                CYLog.Error($"[DataTableManager] 解析 JSON 失败: {name ?? typeof(T).Name} - {ex.Message}");
                return dataTable;
            }

            var rows = wrapper?.rows ?? wrapper?.Rows;
            if (rows == null || rows.Count == 0)
            {
                CYLog.Warning($"[DataTableManager] JSON rows 为空: {name ?? typeof(T).Name}");
                return dataTable;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    continue;
                }
                dataTable.AddRow(row);
            }

            CYLog.Info($"[DataTableManager] 加载数据表(JSON): {name ?? typeof(T).Name}, 行数: {dataTable.Count}");
            return dataTable;
        }

        /// <summary>
        /// 从 JSON 单对象加载数据表（不需要 rows 包装）。
        /// </summary>
        /// <remarks>
        /// JsonUtility 不支持根数组，但支持单对象：{ "Id": 1, ... }。
        /// T 需可被 JsonUtility 反序列化（建议加 [Serializable]，字段使用 public 或 [SerializeField]）。
        /// </remarks>
        public DataTable<T> LoadFromJsonObject<T>(string jsonText, string name = null) where T : class, IDataRow, new()
        {
            return LoadFromJsonObject<T>(jsonText, name, autoFixIdIfZero: false);
        }

        /// <summary>
        /// 从 JSON 单对象加载数据表（不需要 rows 包装，可选自动补 Id）。
        /// </summary>
        /// <remarks>
        /// autoFixIdIfZero=true 时会尝试通过反射写入 Id（仅发生在加载阶段）。
        /// </remarks>
        public DataTable<T> LoadFromJsonObject<T>(string jsonText, string name, bool autoFixIdIfZero) where T : class, IDataRow, new()
        {
            var dataTable = CreateDataTable<T>(name);

            if (string.IsNullOrEmpty(jsonText))
            {
                CYLog.Warning($"[DataTableManager] JSON 为空，加载失败: {name ?? typeof(T).Name}");
                return dataTable;
            }

            T row;
            try
            {
                row = JsonUtility.FromJson<T>(jsonText);
            }
            catch (Exception ex)
            {
                CYLog.Error($"[DataTableManager] 解析 JSON 单对象失败: {name ?? typeof(T).Name} - {ex.Message}");
                return dataTable;
            }

            if (row == null)
            {
                CYLog.Warning($"[DataTableManager] JSON 单对象为空: {name ?? typeof(T).Name}");
                return dataTable;
            }

            if (row.Id == 0)
            {
                if (autoFixIdIfZero)
                {
                    if (!TrySetRowId(row, 1))
                    {
                        CYLog.Warning($"[DataTableManager] JSON 单对象 Id=0，且自动补 Id 失败: {name ?? typeof(T).Name}");
                    }
                }
                else
                {
                    CYLog.Warning($"[DataTableManager] JSON 单对象 Id=0，建议手动设置唯一 Id: {name ?? typeof(T).Name}");
                }
            }

            dataTable.AddRow(row);
            CYLog.Info($"[DataTableManager] 加载数据表(JSON 单对象): {name ?? typeof(T).Name}, 行数: {dataTable.Count}");
            return dataTable;
        }
        
        /// <summary>
        /// 从 ScriptableObject 加载数据表
        /// </summary>
        public DataTable<T> LoadFromScriptableObject<T, TSO>(TSO so, Func<TSO, IEnumerable<T>> rowsGetter, string name = null) 
            where T : class, IDataRow, new()
            where TSO : ScriptableObject
        {
            var dataTable = CreateDataTable<T>(name);
            
            foreach (var row in rowsGetter(so))
            {
                dataTable.AddRow(row);
            }
            
            CYLog.Info($"[DataTableManager] 加载数据表: {name ?? typeof(T).Name}, 行数: {dataTable.Count}");
            return dataTable;
        }
        
        /// <summary>
        /// 卸载数据表
        /// </summary>
        public void UnloadDataTable(string name)
        {
            if (_dataTables.TryGetValue(name, out var dataTable))
            {
                dataTable.Clear();
                _dataTables.Remove(name);
                CYLog.Debug($"[DataTableManager] 卸载数据表: {name}");
            }
        }
        
        /// <summary>
        /// 卸载所有数据表
        /// </summary>
        public void UnloadAllDataTables()
        {
            foreach (var dataTable in _dataTables.Values)
            {
                dataTable.Clear();
            }
            _dataTables.Clear();
            CYLog.Debug("[DataTableManager] 卸载所有数据表");
        }
        
        private readonly StringBuilder _csvParseBuffer = new StringBuilder(256);
        private readonly List<string> _csvParseResult = new List<string>(32);

        [Serializable]
        private class JsonTableWrapper<T>
        {
            public List<T> rows;
            public List<T> Rows;
        }

        /// <summary>
        /// 尝试写入行 Id（仅在加载阶段使用反射，避免运行时开销）。
        /// </summary>
        private static bool TrySetRowId<T>(T row, int id) where T : class
        {
            var type = row.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var field = type.GetField("Id", flags) ?? type.GetField("id", flags);
            if (field != null && field.FieldType == typeof(int))
            {
                field.SetValue(row, id);
                return true;
            }

            var prop = type.GetProperty("Id", flags) ?? type.GetProperty("id", flags);
            if (prop != null && prop.PropertyType == typeof(int) && prop.CanWrite)
            {
                prop.SetValue(row, id);
                return true;
            }

            // 兼容自动属性的后备字段
            field = type.GetField("<Id>k__BackingField", flags) ?? type.GetField("<id>k__BackingField", flags);
            if (field != null && field.FieldType == typeof(int))
            {
                field.SetValue(row, id);
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// 解析 CSV 行（使用 StringBuilder 降低 GC）
        /// </summary>
        private string[] ParseCsvLine(string line, char separator)
        {
            _csvParseResult.Clear();
            _csvParseBuffer.Clear();
            var inQuotes = false;
            
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == separator && !inQuotes)
                {
                    _csvParseResult.Add(_csvParseBuffer.ToString().Trim());
                    _csvParseBuffer.Clear();
                }
                else
                {
                    _csvParseBuffer.Append(c);
                }
            }
            
            _csvParseResult.Add(_csvParseBuffer.ToString().Trim());
            return _csvParseResult.ToArray();
        }
        
        // IDisposableEx
        public void Dispose()
        {
            UnloadAllDataTables();
            CYLog.Debug("[DataTableManager] 已销毁");
        }
    }
}
