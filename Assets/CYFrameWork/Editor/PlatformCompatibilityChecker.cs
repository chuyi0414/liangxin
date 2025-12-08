// ============================================================================
// CYFramework 2.2 - 平台兼容性检查器
// 自动检测代码中可能存在的平台兼容性问题
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CYFramework.Editor
{
    /// <summary>
    /// 平台兼容性检查器
    /// 自动扫描代码，检测 WebGL/微信小游戏不支持的 API
    /// </summary>
    public class PlatformCompatibilityChecker : EditorWindow
    {
        // 不兼容的 API 列表（来自文档 §6.1）
        private static readonly List<IncompatibleAPI> _incompatibleAPIs = new()
        {
            // System.IO
            new IncompatibleAPI
            {
                Pattern = @"\bSystem\.IO\.",
                Name = "System.IO",
                Description = "WebGL/微信不支持文件系统操作",
                Solution = "使用 wx.getFileSystemManager 或 IStorageAdapter",
                Severity = Severity.Error
            },
            new IncompatibleAPI
            {
                Pattern = @"\bFile\.(Read|Write|Exists|Delete|Create|Open|Copy|Move)",
                Name = "File 操作",
                Description = "WebGL/微信不支持 System.IO.File",
                Solution = "使用 IFileSystem 平台适配器",
                Severity = Severity.Error
            },
            new IncompatibleAPI
            {
                Pattern = @"\bDirectory\.(Create|Delete|Exists|GetFiles|GetDirectories)",
                Name = "Directory 操作",
                Description = "WebGL/微信不支持 System.IO.Directory",
                Solution = "使用 IFileSystem 平台适配器",
                Severity = Severity.Error
            },
            
            // System.Net
            new IncompatibleAPI
            {
                Pattern = @"\bSystem\.Net\.Sockets\.",
                Name = "System.Net.Sockets",
                Description = "WebGL/微信不支持原生 Socket",
                Solution = "仅使用 HTTP + WebSocket",
                Severity = Severity.Error
            },
            new IncompatibleAPI
            {
                Pattern = @"\bTcpClient\b|\bTcpListener\b|\bUdpClient\b",
                Name = "TCP/UDP",
                Description = "WebGL/微信不支持 TCP/UDP",
                Solution = "使用 UnityWebRequest 或 WebSocket",
                Severity = Severity.Error
            },
            
            // 多线程
            new IncompatibleAPI
            {
                Pattern = @"\bnew\s+Thread\s*\(",
                Name = "Thread",
                Description = "WebGL/微信运行在单线程环境",
                Solution = "使用协程或 async/await（单线程模拟）",
                Severity = Severity.Error
            },
            new IncompatibleAPI
            {
                Pattern = @"\bThreadPool\.",
                Name = "ThreadPool",
                Description = "WebGL/微信不支持线程池",
                Solution = "使用协程分帧处理",
                Severity = Severity.Error
            },
            new IncompatibleAPI
            {
                Pattern = @"\bTask\.Run\s*\(",
                Name = "Task.Run",
                Description = "WebGL/微信不支持真并行",
                Solution = "使用 async/await 但不会真正并行",
                Severity = Severity.Warning
            },
            
            // DOTS
            new IncompatibleAPI
            {
                Pattern = @"\bNativeArray<",
                Name = "NativeArray",
                Description = "WebGL/微信不支持 Native 容器",
                Solution = "使用普通数组 T[] + 对象池，或添加 #if !UNITY_WEBGL",
                Severity = Severity.Error
            },
            new IncompatibleAPI
            {
                Pattern = @"\bNativeQueue<|\bNativeList<|\bNativeHashMap<",
                Name = "Native 容器",
                Description = "WebGL/微信不支持 Native 容器",
                Solution = "使用普通集合类 + 对象池",
                Severity = Severity.Error
            },
            new IncompatibleAPI
            {
                Pattern = @"\bIJobEntity\b|\bIJob\b|\bIJobParallelFor\b",
                Name = "Job System",
                Description = "WebGL/微信不支持 Job System",
                Solution = "使用纯 C# for 循环 + 分帧处理，或添加 #if !UNITY_WEBGL",
                Severity = Severity.Error
            },
            
            // AppDomain
            new IncompatibleAPI
            {
                Pattern = @"\bAppDomain\.",
                Name = "AppDomain",
                Description = "WebGL/微信不支持 AppDomain",
                Solution = "使用 Application.logMessageReceived，或添加 #if !UNITY_WEBGL",
                Severity = Severity.Error
            },
            
            // 动态加载
            new IncompatibleAPI
            {
                Pattern = @"\bAssembly\.Load",
                Name = "Assembly.Load",
                Description = "WebGL/微信不支持动态加载程序集",
                Solution = "不支持 HybridCLR，只能资源热更",
                Severity = Severity.Error
            },
            
            // 反射（警告级别）
            new IncompatibleAPI
            {
                Pattern = @"\bType\.GetType\s*\(",
                Name = "Type.GetType(string)",
                Description = "WebGL IL2CPP 下部分反射可能失效",
                Solution = "确保类型被正确保留，或使用 link.xml",
                Severity = Severity.Warning
            },
        };
        
        // 检查结果
        private List<CompatibilityIssue> _issues = new();
        private Vector2 _scrollPosition;
        private bool _showErrors = true;
        private bool _showWarnings = true;
        private string _searchPath = "Assets/CYFramework/Runtime";
        
        [MenuItem("CYFramework/平台兼容性检查器")]
        public static void ShowWindow()
        {
            var window = GetWindow<PlatformCompatibilityChecker>("平台兼容性检查");
            window.minSize = new Vector2(600, 400);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("CYFramework 平台兼容性检查器", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("自动检测 WebGL/微信小游戏不支持的 API 调用", MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // 搜索路径
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("扫描路径:", GUILayout.Width(60));
            _searchPath = EditorGUILayout.TextField(_searchPath);
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择扫描目录", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    _searchPath = "Assets" + path.Replace(Application.dataPath, "");
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // 检查按钮
            if (GUILayout.Button("🔍 开始检查", GUILayout.Height(30)))
            {
                RunCheck();
            }
            
            EditorGUILayout.Space(10);
            
            // 过滤器
            EditorGUILayout.BeginHorizontal();
            _showErrors = EditorGUILayout.ToggleLeft($"❌ 错误 ({_issues.Count(i => i.Severity == Severity.Error)})", _showErrors, GUILayout.Width(100));
            _showWarnings = EditorGUILayout.ToggleLeft($"⚠️ 警告 ({_issues.Count(i => i.Severity == Severity.Warning)})", _showWarnings, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // 结果列表
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            foreach (var issue in _issues)
            {
                if (issue.Severity == Severity.Error && !_showErrors) continue;
                if (issue.Severity == Severity.Warning && !_showWarnings) continue;
                
                DrawIssue(issue);
            }
            
            if (_issues.Count == 0)
            {
                EditorGUILayout.HelpBox("点击「开始检查」扫描代码", MessageType.None);
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawIssue(CompatibilityIssue issue)
        {
            var bgColor = issue.Severity == Severity.Error 
                ? new Color(1f, 0.3f, 0.3f, 0.2f) 
                : new Color(1f, 0.8f, 0.3f, 0.2f);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // 标题行
            EditorGUILayout.BeginHorizontal();
            var icon = issue.Severity == Severity.Error ? "❌" : "⚠️";
            EditorGUILayout.LabelField($"{icon} [{issue.APIName}]", EditorStyles.boldLabel, GUILayout.Width(200));
            
            // 跳转按钮
            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(issue.FilePath);
                if (asset != null)
                {
                    AssetDatabase.OpenAsset(asset, issue.LineNumber);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // 文件路径
            EditorGUILayout.LabelField($"📄 {issue.FilePath}:{issue.LineNumber}", EditorStyles.miniLabel);
            
            // 问题描述
            EditorGUILayout.LabelField($"问题: {issue.Description}");
            
            // 代码片段
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("代码:", GUILayout.Width(40));
            EditorGUILayout.SelectableLabel(issue.CodeSnippet.Trim(), EditorStyles.textField, GUILayout.Height(20));
            EditorGUILayout.EndHorizontal();
            
            // 解决方案
            EditorGUILayout.LabelField($"💡 解决方案: {issue.Solution}", EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        private void RunCheck()
        {
            _issues.Clear();
            
            if (!Directory.Exists(_searchPath))
            {
                EditorUtility.DisplayDialog("错误", $"路径不存在: {_searchPath}", "确定");
                return;
            }
            
            var csFiles = Directory.GetFiles(_searchPath, "*.cs", SearchOption.AllDirectories);
            int totalFiles = csFiles.Length;
            int processedFiles = 0;
            
            try
            {
                foreach (var filePath in csFiles)
                {
                    processedFiles++;
                    EditorUtility.DisplayProgressBar("扫描中...", filePath, (float)processedFiles / totalFiles);
                    
                    CheckFile(filePath);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            
            // 按严重程度排序
            _issues = _issues.OrderByDescending(i => i.Severity).ThenBy(i => i.FilePath).ToList();
            
            // 显示结果
            int errors = _issues.Count(i => i.Severity == Severity.Error);
            int warnings = _issues.Count(i => i.Severity == Severity.Warning);
            
            if (errors > 0 || warnings > 0)
            {
                UnityEngine.Debug.LogWarning($"[平台兼容性检查] 发现 {errors} 个错误, {warnings} 个警告");
            }
            else
            {
                UnityEngine.Debug.Log("[平台兼容性检查] ✅ 未发现兼容性问题");
            }
        }
        
        private void CheckFile(string filePath)
        {
            // 跳过 Editor 目录
            if (filePath.Contains("/Editor/") || filePath.Contains("\\Editor\\")) return;
            
            string content = File.ReadAllText(filePath);
            string[] lines = content.Split('\n');
            
            // 检查是否已有平台条件编译
            bool hasWebGLGuard = content.Contains("#if !UNITY_WEBGL") || 
                                 content.Contains("#if UNITY_WEBGL") ||
                                 content.Contains("#if CY_WECHAT");
            
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineNumber = i + 1;
                
                // 跳过注释行
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("//") || trimmedLine.StartsWith("/*") || trimmedLine.StartsWith("*"))
                    continue;
                
                // 检查是否在条件编译块内
                if (IsInsidePlatformGuard(lines, i))
                    continue;
                
                foreach (var api in _incompatibleAPIs)
                {
                    if (Regex.IsMatch(line, api.Pattern))
                    {
                        _issues.Add(new CompatibilityIssue
                        {
                            FilePath = filePath.Replace("\\", "/"),
                            LineNumber = lineNumber,
                            APIName = api.Name,
                            Description = api.Description,
                            Solution = api.Solution,
                            CodeSnippet = line,
                            Severity = api.Severity
                        });
                    }
                }
            }
        }
        
        /// <summary>
        /// 检查当前行是否在平台条件编译块内
        /// </summary>
        private bool IsInsidePlatformGuard(string[] lines, int currentLine)
        {
            int depth = 0;
            bool inWebGLBlock = false;
            
            for (int i = 0; i <= currentLine; i++)
            {
                string line = lines[i].Trim();
                
                if (line.StartsWith("#if"))
                {
                    if (line.Contains("!UNITY_WEBGL") || line.Contains("!CY_WECHAT") ||
                        line.Contains("UNITY_STANDALONE") || line.Contains("UNITY_ANDROID") ||
                        line.Contains("UNITY_IOS") || line.Contains("CY_PC"))
                    {
                        inWebGLBlock = true;
                    }
                    depth++;
                }
                else if (line.StartsWith("#endif"))
                {
                    depth--;
                    if (depth == 0) inWebGLBlock = false;
                }
                else if (line.StartsWith("#else") || line.StartsWith("#elif"))
                {
                    inWebGLBlock = !inWebGLBlock;
                }
            }
            
            return inWebGLBlock;
        }
    }
    
    public enum Severity
    {
        Warning,
        Error
    }
    
    public class IncompatibleAPI
    {
        public string Pattern;
        public string Name;
        public string Description;
        public string Solution;
        public Severity Severity;
    }
    
    public class CompatibilityIssue
    {
        public string FilePath;
        public int LineNumber;
        public string APIName;
        public string Description;
        public string Solution;
        public string CodeSnippet;
        public Severity Severity;
    }
}
