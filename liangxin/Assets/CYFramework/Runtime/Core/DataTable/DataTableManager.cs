// ============================================================================
// CYFramework - 数据表管理器
// 管理游戏配置数据（怪物、技能、关卡等配置表）
// ============================================================================

using System;
using System.Collections.Generic;
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
