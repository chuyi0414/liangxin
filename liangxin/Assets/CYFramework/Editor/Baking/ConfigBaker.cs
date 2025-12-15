// ============================================================================
// CYFramework 2.2 - 配置烘焙工具
// 文档位置：3.1.1 配置烘焙管线 (Config Baking Pipeline)
// 功能：将 ScriptableObject 烘焙为二进制 BlobAsset
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CYFramework.Editor.Baking
{
    /// <summary>
    /// 配置烘焙工具
    /// 文档：
    /// - OOP 目标：直接拷贝/打包原始 SO
    /// - DOTS 目标：自动将 SO 数据"烘焙"为二进制 BlobAsset
    /// </summary>
    public class ConfigBaker : EditorWindow
    {
        private string _sourceFolder = "Assets/Resources/Config";
        private string _outputFolder = "Assets/StreamingAssets/BakedConfig";
        private bool _compressData = true;
        private Vector2 _scrollPos;
        private List<BakeResult> _results = new();
        
        [MenuItem("CYFramework/配置烘焙工具")]
        public static void ShowWindow()
        {
            var window = GetWindow<ConfigBaker>("配置烘焙");
            window.minSize = new Vector2(500, 400);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("CYFramework 配置烘焙工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "将 ScriptableObject 配置烘焙为二进制格式，用于 Release 构建。\n" +
                "• Editor/Development: 直读 SO，无需烘焙\n" +
                "• Release: 读取烘焙后的二进制数据",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // 路径设置
            EditorGUILayout.LabelField("路径设置", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("源目录:", GUILayout.Width(60));
            _sourceFolder = EditorGUILayout.TextField(_sourceFolder);
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择配置目录", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    _sourceFolder = "Assets" + path.Replace(Application.dataPath, "");
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("输出目录:", GUILayout.Width(60));
            _outputFolder = EditorGUILayout.TextField(_outputFolder);
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择输出目录", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    _outputFolder = "Assets" + path.Replace(Application.dataPath, "");
                }
            }
            EditorGUILayout.EndHorizontal();
            
            _compressData = EditorGUILayout.Toggle("压缩数据", _compressData);
            
            EditorGUILayout.Space(10);
            
            // 操作按钮
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("🔥 烘焙所有配置", GUILayout.Height(30)))
            {
                BakeAllConfigs();
            }
            
            if (GUILayout.Button("🗑️ 清空输出", GUILayout.Height(30)))
            {
                ClearOutput();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // 结果列表
            EditorGUILayout.LabelField($"烘焙结果 ({_results.Count})", EditorStyles.boldLabel);
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            foreach (var result in _results)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                
                var icon = result.Success ? "✅" : "❌";
                EditorGUILayout.LabelField(icon, GUILayout.Width(20));
                EditorGUILayout.LabelField(result.SourcePath, GUILayout.Width(250));
                EditorGUILayout.LabelField($"{result.OriginalSize / 1024f:F1}KB → {result.BakedSize / 1024f:F1}KB");
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        /// <summary>
        /// 烘焙所有配置
        /// </summary>
        private void BakeAllConfigs()
        {
            _results.Clear();
            
            if (!Directory.Exists(_sourceFolder))
            {
                EditorUtility.DisplayDialog("错误", $"源目录不存在: {_sourceFolder}", "确定");
                return;
            }
            
            // 确保输出目录存在
            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }
            
            // 查找所有 ScriptableObject
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { _sourceFolder });
            int total = guids.Length;
            int processed = 0;
            
            try
            {
                foreach (var guid in guids)
                {
                    processed++;
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    
                    EditorUtility.DisplayProgressBar("烘焙配置...", assetPath, (float)processed / total);
                    
                    BakeConfig(assetPath);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            
            AssetDatabase.Refresh();
            
            int successCount = _results.FindAll(r => r.Success).Count;
            UnityEngine.Debug.Log($"[ConfigBaker] 烘焙完成: {successCount}/{total}");
        }
        
        /// <summary>
        /// 烘焙单个配置
        /// </summary>
        private void BakeConfig(string assetPath)
        {
            var result = new BakeResult { SourcePath = assetPath };
            
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (asset == null)
                {
                    result.Success = false;
                    _results.Add(result);
                    return;
                }
                
                // 序列化为 JSON
                string json = JsonUtility.ToJson(asset, true);
                byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
                result.OriginalSize = data.Length;
                
                // 可选压缩
                if (_compressData)
                {
                    data = CompressData(data);
                }
                result.BakedSize = data.Length;
                
                // 输出路径
                string relativePath = assetPath.Replace(_sourceFolder, "").TrimStart('/');
                string outputPath = Path.Combine(_outputFolder, relativePath);
                outputPath = Path.ChangeExtension(outputPath, ".bytes");
                
                // 确保目录存在
                string dir = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                
                // 写入文件
                File.WriteAllBytes(outputPath, data);
                
                result.Success = true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ConfigBaker] 烘焙失败: {assetPath}\n{ex}");
                result.Success = false;
            }
            
            _results.Add(result);
        }
        
        /// <summary>
        /// 压缩数据（简单实现）
        /// </summary>
        private byte[] CompressData(byte[] data)
        {
            // 使用 GZip 压缩
            using var output = new MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
        
        /// <summary>
        /// 清空输出目录
        /// </summary>
        private void ClearOutput()
        {
            if (!Directory.Exists(_outputFolder)) return;
            
            if (EditorUtility.DisplayDialog("确认", $"确定要清空输出目录吗?\n{_outputFolder}", "确定", "取消"))
            {
                Directory.Delete(_outputFolder, true);
                Directory.CreateDirectory(_outputFolder);
                AssetDatabase.Refresh();
                
                _results.Clear();
                UnityEngine.Debug.Log("[ConfigBaker] 输出目录已清空");
            }
        }
        
        private class BakeResult
        {
            public string SourcePath;
            public bool Success;
            public int OriginalSize;
            public int BakedSize;
        }
    }
}
