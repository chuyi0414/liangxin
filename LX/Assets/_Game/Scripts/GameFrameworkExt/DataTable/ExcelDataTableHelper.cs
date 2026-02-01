using ExcelDataReader;
using GameFramework;
using GameFramework.DataTable;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace UnityGameFramework.Runtime
{
    /// <summary>
    /// Excel 数据表辅助器：支持从 .bytes（内容为 .xlsx）解析并加载数据表。
    /// </summary>
    public sealed class ExcelDataTableHelper : DataTableHelperBase
    {
        /// <summary>
        /// Zip 文件头标识（xlsx 本质为 zip）。
        /// </summary>
        private static readonly byte[] ZipHeader = { 0x50, 0x4B };

        /// <summary>
        /// OLE 文件头标识（xls 旧格式）。
        /// </summary>
        private static readonly byte[] OleHeader = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

        /// <summary>
        /// 是否已注册代码页编码提供器（用于兼容旧版 xls）。
        /// </summary>
        private static bool s_EncodingProviderRegistered = false;

        /// <summary>
        /// 资源组件引用，用于卸载资源。
        /// </summary>
        private ResourceComponent _resourceComponent = null;

        /// <summary>
        /// 读取数据表资源（TextAsset）。
        /// </summary>
        /// <param name="dataTable">数据表。</param>
        /// <param name="dataTableAssetName">数据表资源名称。</param>
        /// <param name="dataTableAsset">数据表资源。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否读取成功。</returns>
        public override bool ReadData(DataTableBase dataTable, string dataTableAssetName, object dataTableAsset, object userData)
        {
            TextAsset dataTableTextAsset = dataTableAsset as TextAsset;
            if (dataTableTextAsset == null)
            {
                Log.Warning("Data table asset '{0}' is invalid.", dataTableAssetName);
                return false;
            }

            byte[] bytes = dataTableTextAsset.bytes;
            if (IsExcelBytes(bytes, 0, bytes.Length))
            {
                return ParseExcelBytes(dataTable, bytes, 0, bytes.Length, userData);
            }

            return ParseTextData(dataTable, dataTableTextAsset.text, userData);
        }

        /// <summary>
        /// 读取数据表二进制流（不经过资源加载）。
        /// </summary>
        /// <param name="dataTable">数据表。</param>
        /// <param name="dataTableAssetName">数据表资源名称。</param>
        /// <param name="dataTableBytes">数据表二进制流。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否读取成功。</returns>
        public override bool ReadData(DataTableBase dataTable, string dataTableAssetName, byte[] dataTableBytes, int startIndex, int length, object userData)
        {
            if (IsExcelBytes(dataTableBytes, startIndex, length))
            {
                return ParseExcelBytes(dataTable, dataTableBytes, startIndex, length, userData);
            }

            string text = Utility.Converter.GetString(dataTableBytes, startIndex, length);
            return ParseTextData(dataTable, text, userData);
        }

        /// <summary>
        /// 解析文本数据表字符串。
        /// </summary>
        /// <param name="dataTable">数据表。</param>
        /// <param name="dataTableString">数据表字符串。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public override bool ParseData(DataTableBase dataTable, string dataTableString, object userData)
        {
            return ParseTextData(dataTable, dataTableString, userData);
        }

        /// <summary>
        /// 解析数据表二进制流（支持 Excel）。
        /// </summary>
        /// <param name="dataTable">数据表。</param>
        /// <param name="dataTableBytes">数据表二进制流。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public override bool ParseData(DataTableBase dataTable, byte[] dataTableBytes, int startIndex, int length, object userData)
        {
            if (IsExcelBytes(dataTableBytes, startIndex, length))
            {
                return ParseExcelBytes(dataTable, dataTableBytes, startIndex, length, userData);
            }

            string text = Utility.Converter.GetString(dataTableBytes, startIndex, length);
            return ParseTextData(dataTable, text, userData);
        }

        /// <summary>
        /// 释放数据表资源。
        /// </summary>
        /// <param name="dataTable">数据表。</param>
        /// <param name="dataTableAsset">数据表资源。</param>
        public override void ReleaseDataAsset(DataTableBase dataTable, object dataTableAsset)
        {
            _resourceComponent.UnloadAsset(dataTableAsset);
        }

        /// <summary>
        /// 组件启动时初始化资源组件引用。
        /// </summary>
        private void Start()
        {
            _resourceComponent = GameEntry.GetComponent<ResourceComponent>();
            if (_resourceComponent == null)
            {
                Log.Fatal("Resource component is invalid.");
            }
        }

        /// <summary>
        /// 解析文本数据表内容并逐行写入数据表。
        /// </summary>
        /// <param name="dataTable">数据表。</param>
        /// <param name="dataTableString">数据表字符串。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        private bool ParseTextData(DataTableBase dataTable, string dataTableString, object userData)
        {
            try
            {
                int position = 0;
                string dataRowString = null;
                while ((dataRowString = dataTableString.ReadLine(ref position)) != null)
                {
                    if (string.IsNullOrWhiteSpace(dataRowString))
                    {
                        continue;
                    }

                    if (dataRowString[0] == '#')
                    {
                        continue;
                    }

                    if (!dataTable.AddDataRow(dataRowString, userData))
                    {
                        Log.Warning("Can not parse data row string '{0}'.", dataRowString);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("Can not parse data table string with exception '{0}'.", exception);
                return false;
            }
        }

        /// <summary>
        /// 解析 Excel 二进制流并逐行写入数据表。
        /// </summary>
        /// <param name="dataTable">数据表。</param>
        /// <param name="dataTableBytes">Excel 二进制流。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        private bool ParseExcelBytes(DataTableBase dataTable, byte[] dataTableBytes, int startIndex, int length, object userData)
        {
            try
            {
                EnsureEncodingProvider();

                SheetSelection selection = TryGetSheetSelection(userData);
                if (TryParseExcelSheet(dataTable, dataTableBytes, startIndex, length, userData, selection, out bool sheetFound))
                {
                    return true;
                }

                if (selection.HasSelection && !sheetFound)
                {
                    Log.Warning("Excel sheet selection not found, fallback to first sheet.");
                    return TryParseExcelSheet(dataTable, dataTableBytes, startIndex, length, userData, SheetSelection.DefaultFirst, out _);
                }

                return false;
            }
            catch (Exception exception)
            {
                Log.Warning("Can not parse excel bytes with exception '{0}'.", exception);
                return false;
            }
        }

        /// <summary>
        /// 解析指定 Sheet 的 Excel 数据。
        /// </summary>
        /// <param name="dataTable">数据表。</param>
        /// <param name="dataTableBytes">Excel 二进制流。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <param name="selection">目标 Sheet 选择。</param>
        /// <param name="sheetFound">是否找到目标 Sheet。</param>
        /// <returns>是否解析成功。</returns>
        private bool TryParseExcelSheet(DataTableBase dataTable, byte[] dataTableBytes, int startIndex, int length, object userData, SheetSelection selection, out bool sheetFound)
        {
            sheetFound = false;

            using (MemoryStream memoryStream = new MemoryStream(dataTableBytes, startIndex, length, false))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(memoryStream))
            {
                if (selection.HasSelection)
                {
                    if (selection.SelectionType == SheetSelectionType.ByName)
                    {
                        do
                        {
                            if (string.Equals(reader.Name, selection.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                sheetFound = true;
                                break;
                            }
                        }
                        while (reader.NextResult());
                    }
                    else if (selection.SelectionType == SheetSelectionType.ByIndex)
                    {
                        int targetIndex = Mathf.Max(0, selection.Index);
                        int currentIndex = 0;
                        sheetFound = true;
                        while (currentIndex < targetIndex)
                        {
                            if (!reader.NextResult())
                            {
                                sheetFound = false;
                                break;
                            }

                            currentIndex++;
                        }
                    }

                    if (!sheetFound)
                    {
                        return false;
                    }
                }
                else
                {
                    sheetFound = true;
                }

                return ParseExcelReaderRows(dataTable, reader, userData);
            }
        }

        /// <summary>
        /// 将当前 Reader 的行数据写入数据表。
        /// </summary>
        /// <param name="dataTable">数据表。</param>
        /// <param name="reader">Excel 读取器。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        private bool ParseExcelReaderRows(DataTableBase dataTable, IExcelDataReader reader, object userData)
        {
            int fieldCount = reader.FieldCount;
            if (fieldCount <= 0)
            {
                return false;
            }

            StringBuilder rowBuilder = new StringBuilder(256);
            while (reader.Read())
            {
                if (IsRowEmpty(reader, fieldCount))
                {
                    continue;
                }

                if (IsCommentRow(reader, fieldCount))
                {
                    continue;
                }

                rowBuilder.Length = 0;
                for (int i = 0; i < fieldCount; i++)
                {
                    if (i > 0)
                    {
                        rowBuilder.Append('\t');
                    }

                    string cellString = GetCellString(reader, i);
                    rowBuilder.Append(cellString);
                }

                if (!dataTable.AddDataRow(rowBuilder.ToString(), userData))
                {
                    Log.Warning("Can not parse excel row '{0}'.", rowBuilder.ToString());
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取 Excel 单元格字符串（已做基础清洗）。
        /// </summary>
        /// <param name="reader">Excel 读取器。</param>
        /// <param name="columnIndex">列索引。</param>
        /// <returns>单元格字符串。</returns>
        private string GetCellString(IExcelDataReader reader, int columnIndex)
        {
            object value = reader.GetValue(columnIndex);
            if (value == null)
            {
                return string.Empty;
            }

            string text;
            if (value is string stringValue)
            {
                text = stringValue;
            }
            else if (value is DateTime dateTimeValue)
            {
                text = dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
            else if (value is bool boolValue)
            {
                text = boolValue ? "1" : "0";
            }
            else if (value is IFormattable formattableValue)
            {
                text = formattableValue.ToString(null, CultureInfo.InvariantCulture);
            }
            else
            {
                text = value.ToString();
            }

            return NormalizeCellString(text);
        }

        /// <summary>
        /// 判断当前行是否为空行。
        /// </summary>
        /// <param name="reader">Excel 读取器。</param>
        /// <param name="fieldCount">列数。</param>
        /// <returns>是否为空行。</returns>
        private bool IsRowEmpty(IExcelDataReader reader, int fieldCount)
        {
            for (int i = 0; i < fieldCount; i++)
            {
                string cell = GetCellString(reader, i);
                if (!string.IsNullOrWhiteSpace(cell))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 判断当前行是否为注释行（首个非空单元格以 # 开头）。
        /// </summary>
        /// <param name="reader">Excel 读取器。</param>
        /// <param name="fieldCount">列数。</param>
        /// <returns>是否为注释行。</returns>
        private bool IsCommentRow(IExcelDataReader reader, int fieldCount)
        {
            for (int i = 0; i < fieldCount; i++)
            {
                string cell = GetCellString(reader, i);
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                return cell.StartsWith("#", StringComparison.Ordinal);
            }

            return false;
        }

        /// <summary>
        /// 规范化单元格字符串，避免破坏行分隔。
        /// </summary>
        /// <param name="value">原始字符串。</param>
        /// <returns>规范化后的字符串。</returns>
        private string NormalizeCellString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string normalized = value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
            return normalized.Trim();
        }

        /// <summary>
        /// 判断二进制流是否为 Excel 文件。
        /// </summary>
        /// <param name="bytes">二进制流。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">长度。</param>
        /// <returns>是否为 Excel。</returns>
        private bool IsExcelBytes(byte[] bytes, int startIndex, int length)
        {
            if (bytes == null || length <= 0 || startIndex < 0 || startIndex + length > bytes.Length)
            {
                return false;
            }

            if (length >= ZipHeader.Length &&
                bytes[startIndex] == ZipHeader[0] &&
                bytes[startIndex + 1] == ZipHeader[1])
            {
                return true;
            }

            if (length >= OleHeader.Length)
            {
                for (int i = 0; i < OleHeader.Length; i++)
                {
                    if (bytes[startIndex + i] != OleHeader[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 从 userData 中提取 Sheet 选择参数（使用 userData[2]）。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>Sheet 选择结果。</returns>
        private SheetSelection TryGetSheetSelection(object userData)
        {
            object[] userDataArray = userData as object[];
            if (userDataArray != null && userDataArray.Length >= 3)
            {
                object selectionValue = userDataArray[2];
                if (selectionValue is string nameValue && !string.IsNullOrWhiteSpace(nameValue))
                {
                    return SheetSelection.ByName(nameValue);
                }

                if (selectionValue is int indexValue)
                {
                    return SheetSelection.ByIndex(indexValue);
                }

                if (selectionValue is float floatIndex)
                {
                    return SheetSelection.ByIndex((int)floatIndex);
                }

                if (selectionValue is double doubleIndex)
                {
                    return SheetSelection.ByIndex((int)doubleIndex);
                }
            }

            return SheetSelection.DefaultFirst;
        }

        /// <summary>
        /// Sheet 选择类型。
        /// </summary>
        private enum SheetSelectionType
        {
            /// <summary>
            /// 未指定（默认第一个）。
            /// </summary>
            None,
            /// <summary>
            /// 按名称选择。
            /// </summary>
            ByName,
            /// <summary>
            /// 按索引选择。
            /// </summary>
            ByIndex
        }

        /// <summary>
        /// Sheet 选择描述。
        /// </summary>
        private readonly struct SheetSelection
        {
            /// <summary>
            /// 选择类型。
            /// </summary>
            public readonly SheetSelectionType SelectionType;
            /// <summary>
            /// Sheet 名称（按名称选择时使用）。
            /// </summary>
            public readonly string Name;
            /// <summary>
            /// Sheet 索引（按索引选择时使用，0 为第一个）。
            /// </summary>
            public readonly int Index;

            /// <summary>
            /// 是否有显式选择。
            /// </summary>
            public bool HasSelection => SelectionType != SheetSelectionType.None;

            /// <summary>
            /// 默认第一个 Sheet。
            /// </summary>
            public static SheetSelection DefaultFirst => new SheetSelection(SheetSelectionType.None, null, 0);

            /// <summary>
            /// 创建按名称选择的 Sheet。
            /// </summary>
            /// <param name="name">Sheet 名称。</param>
            /// <returns>Sheet 选择结果。</returns>
            public static SheetSelection ByName(string name)
            {
                return new SheetSelection(SheetSelectionType.ByName, name, 0);
            }

            /// <summary>
            /// 创建按索引选择的 Sheet。
            /// </summary>
            /// <param name="index">Sheet 索引（0 为第一个）。</param>
            /// <returns>Sheet 选择结果。</returns>
            public static SheetSelection ByIndex(int index)
            {
                return new SheetSelection(SheetSelectionType.ByIndex, null, index);
            }

            private SheetSelection(SheetSelectionType type, string name, int index)
            {
                SelectionType = type;
                Name = name;
                Index = index;
            }
        }

        /// <summary>
        /// 注册代码页编码提供器（兼容旧版 xls），使用反射避免缺少程序集时报错。
        /// </summary>
        private void EnsureEncodingProvider()
        {
            if (s_EncodingProviderRegistered)
            {
                return;
            }

            try
            {
                Type providerType = Type.GetType("System.Text.CodePagesEncodingProvider, System.Text.Encoding.CodePages");
                if (providerType != null)
                {
                    object instance = providerType.GetProperty("Instance")?.GetValue(null, null);
                    if (instance != null)
                    {
                        Encoding.RegisterProvider((EncodingProvider)instance);
                    }
                }
            }
            catch (Exception)
            {
                // 忽略重复注册或不支持的异常
            }
            finally
            {
                s_EncodingProviderRegistered = true;
            }
        }
    }
}
